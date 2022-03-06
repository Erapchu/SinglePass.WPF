using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace PasswordManager.Authorization.Helpers
{
    internal static class OAuthHelper
    {
        public static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        public static HttpListener StartListener(string redirectUri)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);
            listener.Start();
            return listener;
        }

        public static void OpenBrowser(string uri)
        {
            uri = System.Text.RegularExpressions.Regex.Replace(uri, @"(\\*)" + "\"", @"$1$1\" + "\"");
            uri = System.Text.RegularExpressions.Regex.Replace(uri, @"(\\+)$", @"$1$1");
            Process.Start(new ProcessStartInfo("cmd", $"/c start \"\" \"{uri}\"") { CreateNoWindow = true });
        }
    }
}
