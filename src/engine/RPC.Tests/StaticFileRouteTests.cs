using System.Net;
using System.Net.Sockets;
using RPC.Host.Web;

namespace RPC.Tests;

/// <summary>
/// Pins the static-file route: what it serves, what it refuses, and what it must never reach.
///
/// The route had no coverage at all — the existing transport tests only assert that paths the
/// route does not own fall through to a 404, which never enters the handler. So neither the
/// directory-escape guard nor the single-page-app fallback was exercised by anything except the
/// browser suite.
/// </summary>
public class StaticFileRouteTests : IDisposable
{
    private readonly GameServer _server;
    private readonly HttpClient _http = new();

    public StaticFileRouteTests()
    {
        _server = new GameServer(port: GetFreePort(), loadSave: false);
        _server.Start();
    }

    public void Dispose()
    {
        _server.Stop();
        _http.Dispose();
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private Task<HttpResponseMessage> Get(string path) =>
        _http.GetAsync($"http://localhost:{_server.Port}{path}");

    [Theory]
    [InlineData("/app")]
    [InlineData("/app/")]
    public async Task App_Route_Serves_The_Client_Shell(string path)
    {
        var response = await Get(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<html", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A client route names a screen, not a file. The shell has to come back so the client can
    /// resolve the route itself, which is what makes a deep link or a reload land somewhere other
    /// than the entry screen.
    /// </summary>
    [Fact]
    public async Task Unknown_App_Route_Serves_The_Shell_So_The_Client_Can_Route_It()
    {
        var response = await Get("/app/some/client/route");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// A missing build artefact is the opposite case, and answering it with the shell is what this
    /// pins against: a 200 carrying HTML where a script was requested surfaces in the browser as a
    /// MIME or parse error, several layers away from the actual cause — a page referencing a build
    /// that is no longer on disk. The 404 says that directly.
    /// </summary>
    [Theory]
    [InlineData("/assets/index-DOESNOTEXIST.js")]
    [InlineData("/assets/missing.css")]
    public async Task Missing_Build_Artefact_Is_A_404_Not_The_Shell(string path)
    {
        var response = await Get(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("<html", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Nothing outside the client's build directory may be served, however the path is spelled —
    /// with plain traversal segments, with those segments percent-encoded so the URL parser does
    /// not fold them away, or by a sibling directory whose name merely starts with the same
    /// characters. The status may legitimately differ between these (the request can be rejected
    /// as unowned, refused by the escape guard, or resolve to no such file); what must never
    /// differ is that no file from outside the directory comes back.
    /// </summary>
    [Theory]
    [InlineData("/app/../../../../../../etc/passwd")]
    [InlineData("/assets/../../../../../../etc/passwd")]
    [InlineData("/app/%2e%2e%2f%2e%2e%2f%2e%2e%2f%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd")]
    [InlineData("/assets/%2e%2e%2f%2e%2e%2f%2e%2e%2f%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd")]
    [InlineData("/app/....//....//....//etc/passwd")]
    public async Task No_Path_Escapes_The_Client_Directory(string path)
    {
        var response = await Get(path);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("root:", body, StringComparison.Ordinal);
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.OK,
            $"Unexpected status {(int)response.StatusCode} for {path}");
        // An OK is only acceptable when it is the client's own shell, never a file from outside.
        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains("<html", body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
