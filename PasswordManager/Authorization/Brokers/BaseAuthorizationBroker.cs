using PasswordManager.Authorization.Helpers;
using PasswordManager.Authorization.Interfaces;
using PasswordManager.Authorization.Responses;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Authorization.Brokers
{
    public abstract class BaseAuthorizationBroker : IAuthorizationBroker
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public ITokenHolder TokenHolder { get; }

        public BaseAuthorizationBroker(IHttpClientFactory httpClientFactory, ITokenHolder tokenHolder)
        {
            _httpClientFactory = httpClientFactory;
            TokenHolder = tokenHolder;
        }

        public async Task AuthorizeAsync(CancellationToken cancellationToken)
        {
            var redirectUri = BuildRedirectUri();
            var authorizationUri = BuildAuthorizationUri(redirectUri);
            using var listener = OAuthHelper.StartListener(redirectUri);
            OAuthHelper.OpenBrowser(authorizationUri);
            var response = await GetResponseFromListener(listener, cancellationToken);
            if (string.IsNullOrWhiteSpace(response?.Code))
            {
                throw new Exception("Code was empty!");
            }
            var tokenResponse = await RetrieveToken(response.Code, redirectUri, cancellationToken);
            await TokenHolder.SetAndSaveToken(tokenResponse, cancellationToken);
        }

        public async Task RefreshAccessToken(CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            var refreshTokenEndpointUri = BuildRefreshAccessTokenEndpointUri();
            var postData = BuildRequestForRefreshToken();
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(refreshTokenEndpointUri))
            {
                Content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded")
            };
            var response = await client.SendAsync(request, cancellationToken);
            using var content = response.Content;
            var json = await content.ReadAsStringAsync(cancellationToken);
            await TokenHolder.SetAndSaveToken(json, cancellationToken);
        }

        public async Task RevokeToken(CancellationToken cancellationToken)
        {
            await TokenHolder.RemoveToken();
            var client = _httpClientFactory.CreateClient();
            var revokeTokenEndpointUri = BuildRevokeTokenEndpointUri();
            var stringContent = new StringContent(string.Empty, Encoding.UTF8, "application/x-www-form-urlencoded");
            var response = await client.PostAsync(revokeTokenEndpointUri, stringContent, cancellationToken);
            using var content = response.Content;
            var json = await content.ReadAsStringAsync(cancellationToken);
        }

        private async Task<AuthorizationCodeResponseUrl> GetResponseFromListener(HttpListener listener, CancellationToken cancellationToken)
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
            var bytes = Encoding.UTF8.GetBytes(BuildClosePageResponse());
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

        private async Task<string> RetrieveToken(string code, string redirectUri, CancellationToken cancellationToken)
        {
            string result;

            var client = _httpClientFactory.CreateClient();
            var tokenEndpointUri = BuildTokenEndpointUri();
            var postData = BuildRequestForToken(code, redirectUri);
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(tokenEndpointUri))
            {
                Content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded")
            };
            var response = await client.SendAsync(request, cancellationToken);
            using var content = response.Content;
            var json = await content.ReadAsStringAsync(cancellationToken);
            result = json;//JsonSerializer.Deserialize<AuthResponse>(json);

            return result;
        }

        protected virtual string BuildClosePageResponse()
        {
            return "Authorization success, you can return to application";
        }

        protected abstract string BuildRedirectUri();
        protected abstract string BuildAuthorizationUri(string redirectUri);
        protected abstract string BuildTokenEndpointUri();
        protected abstract string BuildRequestForToken(string code, string redirectUri);
        protected abstract string BuildRefreshAccessTokenEndpointUri();
        protected abstract string BuildRequestForRefreshToken();
        protected abstract string BuildRevokeTokenEndpointUri();
    }
}
