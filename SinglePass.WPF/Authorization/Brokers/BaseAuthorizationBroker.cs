using SinglePass.WPF.Authorization.Helpers;
using SinglePass.WPF.Authorization.TokenHolders;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.WPF.Authorization.Brokers
{
    public abstract class BaseAuthorizationBroker : IAuthorizationBroker
    {
        private readonly IHttpClientFactory _httpClientFactory;

        protected string RedirectUri { get; set; }
        public ITokenHolder TokenHolder { get; }

        public BaseAuthorizationBroker(IHttpClientFactory httpClientFactory, ITokenHolder tokenHolder)
        {
            _httpClientFactory = httpClientFactory;
            TokenHolder = tokenHolder;
        }

        public async Task AuthorizeAsync(CancellationToken cancellationToken)
        {
            BuildRedirectUri();
            var authorizationUri = BuildAuthorizationUri();
            using var listener = OAuthHelper.StartListener(RedirectUri);
            OAuthHelper.OpenBrowser(authorizationUri);
            var response = await OAuthHelper.GetResponseFromListener(listener, BuildClosePageResponse(), cancellationToken);
            if (string.IsNullOrWhiteSpace(response?.Code))
            {
                throw new Exception("Code was empty!");
            }
            var tokenResponse = await RetrieveToken(response.Code, cancellationToken);
            await TokenHolder.SetAndSaveToken(tokenResponse, cancellationToken);
        }

        public async Task RefreshAccessToken(CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            var refreshTokenEndpointUri = BuildRefreshTokenEndpointUri();
            var postData = BuildRefreshTokenRequest();
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
            var revokeTokenEndpointUri = BuildTokenRevokeEndpointUri();
            var stringContent = new StringContent(string.Empty, Encoding.UTF8, "application/x-www-form-urlencoded");
            var response = await client.PostAsync(revokeTokenEndpointUri, stringContent, cancellationToken);
            using var content = response.Content;
            var json = await content.ReadAsStringAsync(cancellationToken);
        }

        private async Task<string> RetrieveToken(string code, CancellationToken cancellationToken)
        {
            string result;

            var client = _httpClientFactory.CreateClient();
            var tokenEndpointUri = BuildTokenEndpointUri();
            var postData = BuildTokenRequest(code);
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

        protected abstract void BuildRedirectUri();
        protected abstract string BuildAuthorizationUri();
        protected abstract string BuildTokenEndpointUri();
        protected abstract string BuildTokenRequest(string code);
        protected abstract string BuildRefreshTokenEndpointUri();
        protected abstract string BuildRefreshTokenRequest();
        protected abstract string BuildTokenRevokeEndpointUri();
    }
}
