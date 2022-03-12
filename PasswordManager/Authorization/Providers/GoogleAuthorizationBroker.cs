using Microsoft.Extensions.Options;
using PasswordManager.Authorization.Helpers;
using PasswordManager.Services;
using System.Net.Http;
using System.Web;

namespace PasswordManager.Authorization.Providers
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
        private readonly GoogleDriveTokenHolder _googleDriveTokenHolder;

        public GoogleAuthorizationBroker(
            IOptions<GoogleDriveConfig> options,
            GoogleDriveTokenHolder googleDriveTokenHolder,
            IHttpClientFactory httpClientFactory) : base(httpClientFactory, googleDriveTokenHolder)
        {
            _config = options.Value;
            _googleDriveTokenHolder = googleDriveTokenHolder;
        }

        protected override string BuildAuthorizationUri(string redirectUri)
        {
            return "https://accounts.google.com/o/oauth2/v2/auth?" +
                $"scope={HttpUtility.UrlEncode(_config.Scopes)}&" +
                "access_type=offline&" +
                "include_granted_scopes=true&" +
                "response_type=code&" +
                "state=state_parameter_passthrough_value&" +
                $"redirect_uri={HttpUtility.UrlEncode(redirectUri)}&" +
                $"client_id={_config.ClientId}";
        }

        protected override string BuildRedirectUri()
        {
            var unusedPort = OAuthHelper.GetRandomUnusedPort();
            return $"http://localhost:{unusedPort}/";
        }

        protected override string BuildRefreshAccessTokenEndpointUri()
        {
            return "https://oauth2.googleapis.com/token";
        }

        protected override string BuildRequestForRefreshToken()
        {
            return $"client_id={_config.ClientId}&" +
                $"client_secret={_config.ClientSecret}&" +
                $"refresh_token={_googleDriveTokenHolder.Token.RefreshToken}&" +
                $"grant_type=refresh_token";
        }

        protected override string BuildRequestForToken(string code, string redirectUri)
        {
            return $"code={code}&" +
                $"client_id={_config.ClientId}&" +
                $"client_secret={_config.ClientSecret}&" +
                $"redirect_uri={HttpUtility.UrlEncode(redirectUri)}&" +
                $"grant_type=authorization_code";
        }

        protected override string BuildTokenEndpointUri()
        {
            return "https://oauth2.googleapis.com/token";
        }
    }
}
