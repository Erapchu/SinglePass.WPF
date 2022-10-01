using SinglePass.WPF.Authorization.Responses;
using System.Collections.Specialized;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace SinglePass.WPF.Authorization.Helpers
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

        public static async Task<AuthorizationCodeResponseUrl> GetResponseFromListener(
            HttpListener listener,
            string responseHtmlText,
            CancellationToken cancellationToken = default)
        {
            HttpListenerContext context;
            // Set up cancellation. HttpListener.GetContextAsync() doesn't accept a cancellation token,
            // the HttpListener needs to be stopped which immediately aborts the GetContextAsync() call.
            using (cancellationToken.Register(listener.Stop))
            {
                // Wait to get the authorization code response.
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // Next line will never be reached because cancellation will always have been requested in this catch block.
                    // But it's required to satisfy compiler.
                    throw new InvalidOperationException();
                }
                catch
                {
                    throw;
                }
            }
            NameValueCollection coll = context.Request.QueryString;

            // Write a "close" response.
            var bytes = Encoding.UTF8.GetBytes(responseHtmlText);
            context.Response.ContentLength64 = bytes.Length;
            context.Response.SendChunked = false;
            context.Response.KeepAlive = false;
            var output = context.Response.OutputStream;
            await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            output.Close();
            context.Response.Close();

            // Create a new response URL with a dictionary that contains all the response query parameters.
            return new AuthorizationCodeResponseUrl(coll.AllKeys.ToDictionary(k => k, k => coll[k]));
        }
    }
}
