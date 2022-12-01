using Microsoft.Extensions.Options;
using SinglePass.WPF.Authorization.Helpers;
using SinglePass.WPF.Authorization.Responses;
using System.Collections.Specialized;
using System.Net.Http;

namespace SinglePass.WPF.Authorization.Brokers
{
    public class GoogleDriveConfig
    {
        public string ClientId { get; init; }
        public string ClientSecret { get; init; }
        public string Scopes { get; init; }
        public string AuthEndpoint { get; init; }
        public string TokenEndpoint { get; init; }
        public string TokenRevokeEndpoint { get; init; }
    }

    public class GoogleOAuthProvider : OAuthProvider
    {
        private readonly GoogleDriveConfig _config;

        public override string ClientId => _config.ClientId;
        public override string ClientSecret => _config.ClientSecret;
        public override string AuthEndpoint => _config.AuthEndpoint;
        public override string TokenEndpoint => _config.TokenEndpoint;

        public GoogleOAuthProvider(
            IOptions<GoogleDriveConfig> options,
            IHttpClientFactory httpClientFactory) : base(httpClientFactory)
        {
            _config = options.Value;
        }

        protected override void FillAuthUriQuery(NameValueCollection nvc)
        {
            nvc["scope"] = _config.Scopes;
            nvc["access_type"] = "offline";
            nvc["include_granted_scopes"] = "true";
            nvc["state"] = "state_parameter_passthrough_value";
        }

        protected override string GetResponseString()
        {
            return "(Google) Authorization success, you can return to application";
        }

        protected override string GetRedirectUri()
        {
            var unusedPort = OAuthHelper.GetRandomUnusedPort();
            return $"http://localhost:{unusedPort}/";
        }

        protected override string BuildTokenRevokeEndpoint(OAuthInfo oAuthInfo)
        {
            return _config.TokenRevokeEndpoint + oAuthInfo.RefreshToken;
        }
    }
}
