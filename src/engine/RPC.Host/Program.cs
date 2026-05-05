using Photino.NET;
using RPC.Host.Web;

namespace RPC.Host;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var server = new GameServer(port: 19421);
        server.Start();
        
        var isDev = args.Contains("--dev");
        var isHeadless = args.Contains("--headless");
        
        if (isHeadless)
        {
            Console.WriteLine($"Server running on http://localhost:{server.Port}/");
            var mre = new System.Threading.ManualResetEvent(false);
            System.Runtime.Loader.AssemblyLoadContext.Default.Unloading += _ => mre.Set();
            Console.CancelKeyPress += (_, _) => mre.Set();
            mre.WaitOne();
            server.Stop();
            return;
        }
        
        var window = new PhotinoWindow()
            .SetTitle("The Reach")
            .SetSize(1280, 720)
            .Center()
            .SetResizable(true);

        window.RegisterWebMessageReceivedHandler((sender, message) =>
        {
            if (message == "getServerPort")
            {
                ((PhotinoWindow)sender).SendWebMessage($"serverPort:{server.Port}");
            }
        });

        if (isDev)
        {
            var devHtml = GetDevHtml(server.Port);
            window.LoadRawString(devHtml);
        }
        else
        {
            window.Load(new Uri($"http://localhost:{server.Port}/app"));
        }

        window.WaitForClose();
        server.Stop();
    }

    static string GetDevHtml(int serverPort)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>The Reach - Dev</title>
    <script>
        window.SERVER_PORT = {serverPort};
    </script>
</head>
<body>
    <iframe src=""http://localhost:5173"" style=""width:100vw;height:100vh;border:none;"" />
</body>
</html>";
    }
}
