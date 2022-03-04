using PasswordManager.Authorization.Responses;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Authorization.Providers
{
    public abstract class BaseAuthProvider
    {
        protected string ClientId { get; set; }
        protected string ClientSecret { get; set; }

        public async Task AuthorizeAsync(CancellationToken cancellationToken)
        {
            var redirectUri = BuildRedirectUri();
            var authorizationUri = BuildAuthorizationUri(redirectUri);
            using var listener = StartListener(redirectUri);
            OpenBrowser(authorizationUri);
            var response = await GetResponseFromListener(listener, cancellationToken);
            if (string.IsNullOrWhiteSpace(response?.Code))
            {
                throw new Exception("Code was empty!");
            }
            await RetrieveToken(response.Code, redirectUri);
        }

        private HttpListener StartListener(string redirectUri)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);
            listener.Start();
            return listener;
        }

        private void OpenBrowser(string uri)
        {
            uri = System.Text.RegularExpressions.Regex.Replace(uri, @"(\\*)" + "\"", @"$1$1\" + "\"");
            uri = System.Text.RegularExpressions.Regex.Replace(uri, @"(\\+)$", @"$1$1");
            Process.Start(new ProcessStartInfo("cmd", $"/c start \"\" \"{uri}\"") { CreateNoWindow = true });
        }

        private async Task<AuthorizationCodeResponseUrl> GetResponseFromListener(HttpListener listener, CancellationToken ct)
        {
            HttpListenerContext context;
            // Set up cancellation. HttpListener.GetContextAsync() doesn't accept a cancellation token,
            // the HttpListener needs to be stopped which immediately aborts the GetContextAsync() call.
            using (ct.Register(listener.Stop))
            {
                // Wait to get the authorization code response.
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception) when (ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
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
            var bytes = Encoding.UTF8.GetBytes(BuildClosePageResponse());
            context.Response.ContentLength64 = bytes.Length;
            context.Response.SendChunked = false;
            context.Response.KeepAlive = false;
            var output = context.Response.OutputStream;
            await output.WriteAsync(bytes, ct).ConfigureAwait(false);
            await output.FlushAsync(ct).ConfigureAwait(false);
            output.Close();
            context.Response.Close();

            // Create a new response URL with a dictionary that contains all the response query parameters.
            return new AuthorizationCodeResponseUrl(coll.AllKeys.ToDictionary(k => k, k => coll[k]));
        }

        private async Task<string> RetrieveToken(string code, string redirectUri)
        {
            string result;

            using (var client = new HttpClient())
            {
                var tokenEndpointUri = BuildTokenEndpointUri();
                client.BaseAddress = new Uri(tokenEndpointUri);
                var postData = BuildRequestForToken(code, redirectUri);
                var request = new HttpRequestMessage(HttpMethod.Post, string.Empty)
                {
                    Content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded")
                };
                var response = await client.SendAsync(request);
                using (var content = response.Content)
                {
                    var json = content.ReadAsStringAsync().Result;
                    result = json;//JsonSerializer.Deserialize<AuthResponse>(json);
                }
            }
            return result;
        }

        protected virtual string BuildClosePageResponse()
        {
            return "Authorization success, you can return to application";
        }

        protected abstract string BuildRedirectUri();
        protected abstract string BuildAuthorizationUri(string redirectUri);
        protected abstract string BuildTokenEndpointUri();
        protected abstract string BuildRefreshAccessTokenUri();
        protected abstract string BuildRequestForToken(string code, string redirectUri);
    }
}
