using Newtonsoft.Json;
using SinglePass.Core.Authorization.Helpers;
using SinglePass.Core.Authorization.Responses;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.Core.Authorization.Brokers
{
    public interface IOAuthProvider
    {
        bool IsConfigured { get; }
        Task<OAuthInfo> AuthorizeAsync(CancellationToken cancellationToken);
        Task<OAuthInfo> RefreshTokenAsync(OAuthInfo oAuthInfo, CancellationToken cancellationToken);
        Task RevokeTokenAsync(OAuthInfo oAuthInfo, CancellationToken cancellationToken);
    }

    /// <summary>
    /// OAuth 2.0 authorization code flow with PKCE and a loopback redirect for a desktop (public) client.
    /// Providers only describe what differs: endpoints, credentials, redirect URI, extra query
    /// parameters and token revocation.
    /// </summary>
    public abstract class OAuthProvider : IOAuthProvider
    {
        private static readonly TimeSpan AuthorizationTimeout = TimeSpan.FromMinutes(5);
        private readonly IHttpClientFactory _httpClientFactory;

        protected OAuthProvider(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public abstract string ClientId { get; }
        public abstract string ClientSecret { get; }
        protected abstract string AuthEndpoint { get; }
        protected abstract string TokenEndpoint { get; }
        protected abstract string Scopes { get; }

        /// <summary>Whether the provider rejects token requests without a client secret.</summary>
        protected virtual bool RequiresClientSecret => false;

        /// <summary>Whether the provider rotates refresh tokens, so every token response must carry a new one.</summary>
        protected virtual bool RequiresRefreshTokenInResponse => false;

        /// <summary>Whether the code exchange must repeat the redirect URI (RFC 6749 §4.1.3).</summary>
        protected virtual bool IncludeRedirectUriInTokenRequest => true;

        /// <summary>Text shown in the browser after the callback is received.</summary>
        protected virtual string CallbackResponseText => "SinglePass: return to the application to finish signing in.";

        public virtual bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId)
            && (!RequiresClientSecret || !string.IsNullOrWhiteSpace(ClientSecret));

        public async Task<OAuthInfo> AuthorizeAsync(CancellationToken cancellationToken)
        {
            if (!IsConfigured)
                throw new InvalidOperationException($"{GetType().Name}: OAuth client credentials are missing.");

            var redirectUri = GetValidatedRedirectUri();
            var state = OAuthHelper.CreateRandomUrlToken();
            var verifier = OAuthHelper.CreateRandomUrlToken();

            var query = OAuthHelper.GetHttpQSCollection();
            query["response_type"] = "code";
            query["client_id"] = ClientId;
            query["redirect_uri"] = redirectUri.AbsoluteUri;
            query["scope"] = Scopes;
            query["state"] = state;
            query["code_challenge"] = OAuthHelper.CreatePkceChallenge(verifier);
            query["code_challenge_method"] = "S256";
            FillAuthorizationQuery(query);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(AuthorizationTimeout);

            AuthorizationCodeResponseUrl callback;
            using (var listener = StartCallbackListener(redirectUri))
            {
                OAuthHelper.OpenBrowser(new Uri($"{AuthEndpoint}?{query}"));
                callback = await OAuthHelper.GetResponseFromListener(listener, CallbackResponseText, timeout.Token)
                    .ConfigureAwait(false);
            }

            if (!string.Equals(callback?.State, state, StringComparison.Ordinal))
                throw new InvalidOperationException("The OAuth callback state is missing or does not match the authorization request.");

            if (callback.Error != null || string.IsNullOrWhiteSpace(callback.Code))
                throw new InvalidOperationException($"The OAuth authorization was declined or failed ({callback.Error}).");

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = callback.Code,
                ["code_verifier"] = verifier
            };
            if (IncludeRedirectUriInTokenRequest)
                form["redirect_uri"] = redirectUri.AbsoluteUri;

            return await RequestTokenAsync(form, previous: null, cancellationToken).ConfigureAwait(false);
        }

        public Task<OAuthInfo> RefreshTokenAsync(OAuthInfo oAuthInfo, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(oAuthInfo);

            // A refresh token is bound to the client that issued it. Credentials always come from the
            // current configuration so that rotating the secret does not break existing sessions.
            if (!IsConfigured || string.IsNullOrEmpty(oAuthInfo.RefreshToken)
                || !string.Equals(oAuthInfo.ClientId, ClientId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Sign in to the cloud again to renew access.");
            }

            return RequestTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = oAuthInfo.RefreshToken
            }, oAuthInfo, cancellationToken);
        }

        /// <summary>
        /// Revokes the session on the provider side. Best effort: a token that is already invalid
        /// must not prevent the user from signing out locally.
        /// </summary>
        public async Task RevokeTokenAsync(OAuthInfo oAuthInfo, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(oAuthInfo);
            if (!IsConfigured || !string.Equals(oAuthInfo.ClientId, ClientId, StringComparison.Ordinal))
                return;

            var revoke = GetRevokeRequest(oAuthInfo);
            if (revoke is null)
                return;

            var (endpoint, form) = revoke.Value;
            using var response = await PostFormAsync(endpoint, form, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Loopback redirect URI for this authorization attempt.</summary>
        protected abstract Uri GetRedirectUri();

        /// <summary>Adds provider-specific parameters to the authorization request.</summary>
        protected virtual void FillAuthorizationQuery(System.Collections.Specialized.NameValueCollection query) { }

        /// <summary>Revocation endpoint and form body, or <c>null</c> when there is nothing to revoke.</summary>
        protected abstract (string Endpoint, Dictionary<string, string> Form)? GetRevokeRequest(OAuthInfo oAuthInfo);

        private Uri GetValidatedRedirectUri()
        {
            var redirectUri = GetRedirectUri();
            if (redirectUri is null || !redirectUri.IsAbsoluteUri || !redirectUri.IsLoopback
                || redirectUri.Scheme != Uri.UriSchemeHttp || !redirectUri.AbsoluteUri.EndsWith('/')
                || redirectUri.UserInfo.Length != 0 || redirectUri.Query.Length != 0 || redirectUri.Fragment.Length != 0)
            {
                throw new InvalidOperationException("OAuth requires an HTTP loopback redirect URI ending in a slash.");
            }

            return redirectUri;
        }

        private static HttpListener StartCallbackListener(Uri redirectUri)
        {
            try
            {
                return OAuthHelper.StartListener(redirectUri.AbsoluteUri);
            }
            catch (HttpListenerException ex) when (ex.NativeErrorCode is 32 or 183 or 10048 or 10013)
            {
                throw new OAuthCallbackUnavailableException(redirectUri.Port, ex);
            }
        }

        private async Task<OAuthInfo> RequestTokenAsync(Dictionary<string, string> form, OAuthInfo previous,
            CancellationToken cancellationToken)
        {
            using var response = await PostFormAsync(TokenEndpoint, form, cancellationToken).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // invalid_grant on refresh: the refresh token was revoked or expired, the user must sign in again.
                string error = null;
                try { error = JsonConvert.DeserializeObject<TokenErrorResponse>(json)?.Error; }
                catch (JsonException) { }
                throw new InvalidOperationException(
                    $"The OAuth token request failed ({(int)response.StatusCode} {error}). Sign in to the cloud again.");
            }

            var token = JsonConvert.DeserializeObject<TokenResponse>(json);
            if (string.IsNullOrWhiteSpace(token?.AccessToken))
                throw new InvalidOperationException("The OAuth token response is missing an access token.");

            // Providers that do not rotate refresh tokens usually omit it on refresh; keep the existing one.
            var refreshToken = string.IsNullOrEmpty(token.RefreshToken) && !RequiresRefreshTokenInResponse
                ? previous?.RefreshToken
                : token.RefreshToken;
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new InvalidOperationException("The OAuth token response is missing a refresh token.");

            return new OAuthInfo
            {
                AccessToken = token.AccessToken,
                RefreshToken = refreshToken,
                ExpiresIn = token.ExpiresIn is > 0 ? token.ExpiresIn.Value : 3599,
                CreationTime = DateTimeOffset.UtcNow,
                TokenType = token.TokenType,
                ClientId = ClientId
            };
        }

        private async Task<HttpResponseMessage> PostFormAsync(string endpoint, Dictionary<string, string> form,
            CancellationToken cancellationToken)
        {
            form["client_id"] = ClientId;
            if (!string.IsNullOrWhiteSpace(ClientSecret))
                form["client_secret"] = ClientSecret;

            using var client = _httpClientFactory.CreateClient(nameof(OAuthProvider));
            using var content = new FormUrlEncodedContent(form);
            return await client.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
        }
    }

    internal sealed class OAuthCallbackUnavailableException(int port, Exception innerException)
        : Exception($"Unable to start the OAuth callback on local port {port}.", innerException)
    {
        public int Port { get; } = port;
    }
}
