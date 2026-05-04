using Photino.NET;
using RPC.Host.Web;
using System.Text;

namespace RPC.Host;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var server = new GameServer(port: 19421);
        server.Start();
        
        // Determine if we're in dev or production mode
        var isDev = args.Contains("--dev");
        
        var window = new PhotinoWindow()
            .SetTitle("The Reach")
            .SetSize(1280, 720)
            .Center()
            .SetResizable(true);

        // Inject server port into the page
        window.RegisterWebMessageReceivedHandler((sender, message) =>
        {
            if (message == "getServerPort")
            {
                ((PhotinoWindow)sender).SendWebMessage($"serverPort:{server.Port}");
            }
        });

        // Load the app
        if (isDev)
        {
            // In dev mode, load a wrapper page that sets SERVER_PORT then loads Vite
            var devHtml = GetDevHtml(server.Port);
            window.LoadRawString(devHtml);
        }
        else
        {
            // In production, load the built frontend from the backend
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
