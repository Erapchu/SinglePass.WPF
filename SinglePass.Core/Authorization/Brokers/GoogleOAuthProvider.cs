using Microsoft.Extensions.Options;
using SinglePass.Core.Authorization.Helpers;
using SinglePass.Core.Authorization.Responses;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;

namespace SinglePass.Core.Authorization.Brokers
{
    public class GoogleDriveConfig
    {
        public string ClientId { get; init; }
        public string ClientSecret { get; init; }
    }

    public class GoogleOAuthProvider : OAuthProvider
    {
        private readonly GoogleDriveConfig _config;

        public GoogleOAuthProvider(IOptions<GoogleDriveConfig> options, IHttpClientFactory httpClientFactory)
            : base(httpClientFactory)
        {
            _config = options.Value;
        }

        public override string ClientId => _config.ClientId;
        public override string ClientSecret => _config.ClientSecret;
        protected override string AuthEndpoint => "https://accounts.google.com/o/oauth2/v2/auth";
        protected override string TokenEndpoint => "https://oauth2.googleapis.com/token";
        protected override string Scopes => "https://www.googleapis.com/auth/drive.file https://www.googleapis.com/auth/userinfo.profile";

        // Google "Desktop app" clients still require the (non-confidential) secret in token requests.
        protected override bool RequiresClientSecret => true;

        protected override void FillAuthorizationQuery(NameValueCollection query)
        {
            query["access_type"] = "offline";
            query["include_granted_scopes"] = "true";
        }

        // Google accepts any loopback port for desktop clients.
        protected override Uri GetRedirectUri() => new($"http://localhost:{OAuthHelper.GetRandomUnusedPort()}/");

        protected override (string Endpoint, Dictionary<string, string> Form)? GetRevokeRequest(OAuthInfo oAuthInfo)
        {
            // Revoking the refresh token also revokes the access tokens issued for it.
            var token = string.IsNullOrEmpty(oAuthInfo.RefreshToken) ? oAuthInfo.AccessToken : oAuthInfo.RefreshToken;
            if (string.IsNullOrEmpty(token))
                return null;

            return ("https://oauth2.googleapis.com/revoke", new Dictionary<string, string> { ["token"] = token });
        }
    }
}
