using Newtonsoft.Json;
using SinglePass.WPF.Authorization.Helpers;
using SinglePass.WPF.Authorization.Responses;
using System;
using System.Collections.Specialized;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.WPF.Authorization.Brokers
{
    public interface IOAuthProvider
    {
        public Task<OAuthInfo> AuthorizeAsync(CancellationToken cancellationToken);
        public Task<OAuthInfo> RefreshTokenAsync(OAuthInfo oAuthInfo, CancellationToken cancellationToken);
        public Task RevokeTokenAsync(OAuthInfo oAuthInfo, CancellationToken cancellationToken);
    }

    public abstract class OAuthProvider : IOAuthProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public abstract string ClientId { get; }
        public abstract string ClientSecret { get; }
        public abstract string AuthEndpoint { get; }
        public abstract string TokenEndpoint { get; }

        public OAuthProvider(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Performs OAuth 2.0 authorization.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>New instance of <see cref="OAuthInfo"/>.</returns>
        /// <exception cref="Exception"></exception>
        public async Task<OAuthInfo> AuthorizeAsync(CancellationToken cancellationToken)
        {
            var redirectUri = GetRedirectUri();
            var response = await GetAuthCode(redirectUri, cancellationToken);

            if (string.IsNullOrWhiteSpace(response?.Code))
            {
                var exceptionSb = new StringBuilder();
                exceptionSb.AppendLine("Code was empty!");
                if (response?.State != null)
                {
                    exceptionSb.Append("State: ");
                    exceptionSb.AppendLine(response.State);
                }

                if (response?.Error != null)
                {
                    exceptionSb.Append("Error: ");
                    exceptionSb.AppendLine(response.Error);
                }

                if (response?.ErrorDescription != null)
                {
                    exceptionSb.Append("Description: ");
                    exceptionSb.AppendLine(response.ErrorDescription);
                }

                throw new Exception(exceptionSb.ToString());
            }

            var tokenResponse = await GetTokenResponse(response.Code, redirectUri);
            return new OAuthInfo()
            {
                AccessToken = tokenResponse.AccessToken,
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                CreationTime = DateTimeOffset.Now,
                ExpiresIn = tokenResponse.ExpiresIn ?? 3599,
                RedirectUri = redirectUri,
                RefreshToken = tokenResponse.RefreshToken,
                TokenType = tokenResponse.TokenType,
            };
        }

        /// <summary>
        /// Performs refresh token.
        /// </summary>
        /// <param name="oAuthInfo">Existant instance of <see cref="OAuthInfo"/>.</param>
        /// <returns>New refreshed instance of <see cref="OAuthInfo"/>.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public virtual async Task<OAuthInfo> RefreshTokenAsync(OAuthInfo oAuthInfo, CancellationToken cancellationToken)
        {
            if (oAuthInfo is null)
                throw new ArgumentNullException(nameof(oAuthInfo));

            var nvc = OAuthHelper.GetHttpQSCollection();
            nvc[OAuthHelper.GrantType] = OAuthHelper.RefreshToken;
            nvc[OAuthHelper.ClientId] = oAuthInfo.ClientId;
            nvc[OAuthHelper.ClientSecret] = oAuthInfo.ClientSecret;
            nvc[OAuthHelper.RedirectUri] = oAuthInfo.RedirectUri;
            nvc[OAuthHelper.RefreshToken] = oAuthInfo.RefreshToken;

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
            {
                Content = new StringContent(nvc.ToString(), Encoding.UTF8, OAuthHelper.ApplicationXWWW)
            };
            var httpClient = new HttpClient();
            var response = await httpClient.SendAsync(httpRequestMessage, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseText);
            return new OAuthInfo()
            {
                AccessToken = tokenResponse.AccessToken,
                ClientId = oAuthInfo.ClientId,
                ClientSecret = oAuthInfo.ClientSecret,
                CreationTime = DateTimeOffset.Now,
                ExpiresIn = tokenResponse.ExpiresIn ?? 3599,
                RedirectUri = oAuthInfo.RedirectUri,
                RefreshToken = tokenResponse.RefreshToken,
                TokenType = tokenResponse.TokenType,
            };
        }

        public async Task RevokeTokenAsync(OAuthInfo oAuthInfo, CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var nvc = OAuthHelper.GetHttpQSCollection();
            FillRevokeTokenContent(nvc);
            var tokenRevokeEndpoint = BuildTokenRevokeEndpoint(oAuthInfo);
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, tokenRevokeEndpoint)
            {
                Content = new StringContent(nvc.ToString(), Encoding.UTF8, OAuthHelper.ApplicationXWWW)
            };
            var response = await httpClient.SendAsync(httpRequestMessage);
            using var content = response.Content;
            var json = await content.ReadAsStringAsync(cancellationToken);
        }

        private async Task<AuthorizationCodeResponseUrl> GetAuthCode(string redirectUri, CancellationToken cancellationToken)
        {
            using (var listener = OAuthHelper.StartListener(redirectUri))
            {
                var authorizationUri = CreateUrlRequestCode(redirectUri);
                OAuthHelper.OpenBrowser(authorizationUri);
                return await OAuthHelper.GetResponseFromListener(listener, GetResponseString(), cancellationToken);
            }
        }

        private async Task<TokenResponse> GetTokenResponse(string code, string redirectUri)
        {
            var http = new HttpClient();
            var tokenRequest = CreateRequestMessageToken(code, redirectUri);
            var response = await http.SendAsync(tokenRequest);
            var responseText = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TokenResponse>(responseText);
        }

        /// <summary>
        /// Creates Url request code that will be used for authorization in Browser.
        /// </summary>
        /// <returns></returns>
        private string CreateUrlRequestCode(string redirectUri)
        {
            var nvc = OAuthHelper.GetHttpQSCollection();
            nvc[OAuthHelper.ClientId] = ClientId;
            nvc[OAuthHelper.ResponseType] = OAuthHelper.Code;
            nvc[OAuthHelper.RedirectUri] = redirectUri;
            FillAuthUriQuery(nvc);
            var authUri = new Uri(AuthEndpoint);
            var uriBuilder = new UriBuilder(authUri.Scheme, authUri.Host, authUri.Port, authUri.LocalPath, $"?{nvc}");
            return uriBuilder.Uri.AbsoluteUri;
        }

        /// <summary>
        /// Creates HttpRequestMessage that will be used to receive response to obtain access and refresh token.
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private HttpRequestMessage CreateRequestMessageToken(string code, string redirectUri)
        {
            var nvc = OAuthHelper.GetHttpQSCollection();
            nvc[OAuthHelper.Code] = code;
            nvc[OAuthHelper.ClientId] = ClientId;
            nvc[OAuthHelper.ClientSecret] = ClientSecret;
            nvc[OAuthHelper.GrantType] = OAuthHelper.AuthorizationCode;
            nvc[OAuthHelper.RedirectUri] = redirectUri;
            FillTokenContent(nvc);
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
            {
                Content = new StringContent(nvc.ToString(), Encoding.UTF8, OAuthHelper.ApplicationXWWW)
            };
            return httpRequestMessage;
        }

        /// <summary>
        /// Setting up <see cref="NameValueCollection"/> to generate authorization Uri.
        /// </summary>
        /// <param name="nvc"><see cref="NameValueCollection"/> instance</param>
        protected virtual void FillAuthUriQuery(NameValueCollection nvc) { }

        /// <summary>
        /// Setting up <see cref="NameValueCollection"/> to generate HTTP.POST string content (Body).
        /// </summary>
        /// <param name="nvc"><see cref="NameValueCollection"/> instance</param>
        protected virtual void FillTokenContent(NameValueCollection nvc) { }

        protected virtual void FillRevokeTokenContent(NameValueCollection nvc) { }

        /// <summary>
        /// Gets new RedirectUri for authorization.
        /// </summary>
        protected abstract string GetRedirectUri();

        /// <summary>
        /// String that transfered to default user browser on success authorization.
        /// </summary>
        /// <returns></returns>
        protected abstract string GetResponseString();

        protected abstract string BuildTokenRevokeEndpoint(OAuthInfo oAuthInfo);
    }
}
