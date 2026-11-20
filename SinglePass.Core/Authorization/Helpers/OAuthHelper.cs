using SinglePass.Core.Authorization.Responses;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SinglePass.Core.Authorization.Helpers
{
    internal static class OAuthHelper
    {
        internal static string CreateRandomUrlToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

        internal static string CreatePkceChallenge(string verifier) =>
            Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        internal static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

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
            try
            {
                listener.Prefixes.Add(redirectUri);
                listener.Start();
                return listener;
            }
            catch
            {
                listener.Close();
                throw;
            }
        }

        public static void OpenBrowser(Uri uri)
        {
            // Only hand HTTPS URLs to the shell so nothing else can be launched through it.
            if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException("Only HTTPS authorization URLs can be opened.", nameof(uri));

            using var _ = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
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

        /// <summary>
        /// Get HttpQSCollection instance. Important difference between <see cref="HttpUtility"/>.ParseQueryString("") and new <see cref="NameValueCollection"/>:
        /// only the <see cref="HttpUtility"/> result will override ToString() to produce a proper Query String.
        /// </summary>
        /// <returns>New instance of <see cref="NameValueCollection"/>.</returns>
        public static NameValueCollection GetHttpQSCollection()
        {
            return HttpUtility.ParseQueryString(string.Empty);
        }
    }
}
