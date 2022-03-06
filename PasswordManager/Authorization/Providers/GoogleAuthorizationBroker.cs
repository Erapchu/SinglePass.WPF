using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PasswordManager.Authorization.Helpers;
using PasswordManager.Helpers;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly ILogger<GoogleAuthorizationBroker> _logger;
        private readonly GoogleDriveConfig _config;

        public GoogleAuthorizationBroker(ILogger<GoogleAuthorizationBroker> logger, IOptions<GoogleDriveConfig> options)
        {
            _config = options.Value;
            _logger = logger;
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

        protected override string BuildRefreshAccessTokenUri()
        {
            throw new NotImplementedException();
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

        protected async override Task SaveResponse(string tokenResponse, CancellationToken cancellationToken)
        {
            using var fileStream = new FileStream(Constants.GoogleDriveFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await JsonSerializer.SerializeAsync(fileStream, tokenResponse, cancellationToken: cancellationToken);
            _logger.LogInformation("Token response saved to file");
        }
    }
}
