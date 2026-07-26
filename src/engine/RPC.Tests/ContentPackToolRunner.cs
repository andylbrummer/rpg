using System.Diagnostics;

namespace RPC.Tests;

/// <summary>
/// Runs the content-pack compiler for the tests that exercise it end to end.
///
/// <para>
/// Every caller used to shell out to <c>dotnet run --project tools/content-pack</c>. That builds
/// the tool as a side effect of running it, so two such tests scheduled in parallel — xUnit runs
/// separate test classes concurrently — raced on the same obj/bin output and one of them failed
/// its build, surfacing as a bare non-zero exit code. It also paid the build cost per call, which
/// was most of those tests' runtime.
/// </para>
///
/// <para>
/// The tool is instead built exactly once per test run and afterwards invoked as a plain
/// assembly. Building rather than reusing whatever happens to sit in bin/ also means these tests
/// exercise the compiler as it exists in the tree, not a stale artifact from an earlier session.
/// </para>
/// </summary>
internal static class ContentPackToolRunner
{
    private static readonly Lazy<string> Assembly = new(BuildOnce, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Ceiling on one compiler invocation. Compiling the real content directory takes well under a
    /// second; a run that reaches this has hung, and failing with the captured output beats a test
    /// that never returns.
    /// </summary>
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(5);

    public readonly record struct ToolRun(int ExitCode, string Output)
    {
        /// <summary>Assertion-friendly rendering: a bare exit code says nothing about what broke.</summary>
        public override string ToString() => $"content-pack exited {ExitCode}. Output:\n{Output}";
    }

    public static ToolRun Run(string contentDir, string outputDir)
        => Execute($"\"{Assembly.Value}\" \"{contentDir}\" \"{outputDir}\"", RunTimeout);

    private static string ProjectDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "tools", "content-pack");
            if (File.Exists(Path.Combine(candidate, "content-pack.csproj"))) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate tools/content-pack above {AppContext.BaseDirectory}");
    }

    private static string BuildOnce()
    {
        var projectDir = ProjectDirectory();
        var result = Execute($"build \"{Path.Combine(projectDir, "content-pack.csproj")}\" -c Debug", BuildTimeout);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Could not build the content-pack tool. {result}");
        }

        var assembly = Path.Combine(projectDir, "bin", "Debug", "net9.0", "content-pack.dll");
        if (!File.Exists(assembly))
        {
            throw new FileNotFoundException($"content-pack built but its assembly is missing at {assembly}");
        }
        return assembly;
    }

    /// <summary>
    /// Runs dotnet and returns its exit code with stdout and stderr combined.
    /// <para>
    /// Both streams are drained concurrently with the wait. Reading them after WaitForExit — as
    /// every call site did — deadlocks whenever the child outruns the pipe buffer: the child
    /// blocks writing, the parent blocks waiting, and neither ever moves. The compiler's output on
    /// a validation failure is exactly the case that grows.
    /// </para>
    /// </summary>
    private static ToolRun Execute(string arguments, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new TimeoutException($"`dotnet {arguments}` did not finish within {timeout.TotalSeconds:0}s.");
        }

        // The exit code can be observed before the redirected streams have been fully drained;
        // this overload waits for that drain to complete.
        process.WaitForExit();
        return new ToolRun(process.ExitCode, stdout.Result + stderr.Result);
    }
}
