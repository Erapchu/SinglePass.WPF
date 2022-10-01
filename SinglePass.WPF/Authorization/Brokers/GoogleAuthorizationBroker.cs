using Microsoft.Extensions.Options;
using SinglePass.WPF.Authorization.Helpers;
using SinglePass.WPF.Authorization.TokenHolders;
using System.Net.Http;
using System.Web;

namespace SinglePass.WPF.Authorization.Brokers
{
    public class GoogleDriveConfig
    {
        public string ClientId { get; init; }
        public string ClientSecret { get; init; }
        public string Scopes { get; init; }
    }

    public class GoogleAuthorizationBroker : BaseAuthorizationBroker
    {
        private readonly GoogleDriveConfig _config;

        public GoogleAuthorizationBroker(
            IOptions<GoogleDriveConfig> options,
            GoogleDriveTokenHolder tokenHolder,
            IHttpClientFactory httpClientFactory) : base(httpClientFactory, tokenHolder)
        {
            _config = options.Value;
        }

        protected override string BuildAuthorizationUri()
        {
            return "https://accounts.google.com/o/oauth2/v2/auth?" +
                $"scope={HttpUtility.UrlEncode(_config.Scopes)}&" +
                "access_type=offline&" +
                "include_granted_scopes=true&" +
                "response_type=code&" +
                "state=state_parameter_passthrough_value&" +
                $"redirect_uri={HttpUtility.UrlEncode(RedirectUri)}&" +
                $"client_id={_config.ClientId}";
        }

        protected override void BuildRedirectUri()
        {
            var unusedPort = OAuthHelper.GetRandomUnusedPort();
            RedirectUri = $"http://localhost:{unusedPort}/";
        }

        protected override string BuildRefreshTokenEndpointUri()
        {
            return "https://oauth2.googleapis.com/token";
        }

        protected override string BuildRefreshTokenRequest()
        {
            return $"client_id={_config.ClientId}&" +
                $"client_secret={_config.ClientSecret}&" +
                $"refresh_token={TokenHolder.Token.RefreshToken}&" +
                $"grant_type=refresh_token";
        }

        protected override string BuildTokenRequest(string code)
        {
            return $"code={code}&" +
                $"client_id={_config.ClientId}&" +
                $"client_secret={_config.ClientSecret}&" +
                $"redirect_uri={HttpUtility.UrlEncode(RedirectUri)}&" +
                $"grant_type=authorization_code";
        }

        protected override string BuildTokenEndpointUri()
        {
            return "https://oauth2.googleapis.com/token";
        }

        protected override string BuildTokenRevokeEndpointUri()
        {
            return $"https://oauth2.googleapis.com/revoke?token={TokenHolder.Token.RefreshToken}";
        }
    }
}
