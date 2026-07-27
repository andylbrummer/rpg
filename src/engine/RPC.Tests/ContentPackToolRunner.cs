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
/// The tool is instead built by MSBuild along with this test project — a ProjectReference that
/// takes no assembly reference, since the tests invoke the compiler as a process — and simply
/// invoked here. That keeps the guarantee that these tests exercise the compiler as it exists in
/// the tree rather than a stale artifact, without putting a compile in the middle of a fully
/// parallel test run, where contention stretched a ~2s build into ~15s of suite wall-clock.
/// </para>
/// </summary>
internal static class ContentPackToolRunner
{
    private static readonly Lazy<string> Assembly = new(LocateBuiltTool, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Ceiling on one compiler invocation. Compiling the real content directory takes well under a
    /// second; a run that reaches this has hung, and failing with the captured output beats a test
    /// that never returns.
    /// </summary>
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(2);


    public readonly record struct ToolRun(int ExitCode, string Output)
    {
        /// <summary>Assertion-friendly rendering: a bare exit code says nothing about what broke.</summary>
        public override string ToString() => $"content-pack exited {ExitCode}. Output:\n{Output}";
    }

    public static ToolRun Run(string contentDir, string outputDir)
        => Execute($"\"{Assembly.Value}\" \"{contentDir}\" \"{outputDir}\"", RunTimeout);

    /// <summary>
    /// The path the build recorded for the tool it produced. Missing means the ProjectReference in
    /// RPC.Tests.csproj was removed or the tool failed to build, both of which must be reported
    /// rather than worked around — silently compiling one here is what this replaced.
    /// </summary>
    private static string LocateBuiltTool()
    {
        var recorded = typeof(ContentPackToolRunner).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "ContentPackToolAssembly")?.Value;

        if (string.IsNullOrEmpty(recorded))
            throw new InvalidOperationException(
                "The build did not record where the content-pack tool was written. "
                + "RPC.Tests.csproj must reference tools/content-pack and set the ContentPackToolAssembly metadata.");

        if (!File.Exists(recorded))
            throw new FileNotFoundException($"The content-pack tool was not built at {recorded}.", recorded);

        return recorded;
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
