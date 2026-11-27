using Microsoft.Extensions.Options;
using Erapkey.Core.Authorization.Helpers;
using Erapkey.Core.Authorization.Responses;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Erapkey.Core.Authorization.Brokers
{
    public class YandexDiskConfig
    {
        public string ClientId { get; init; }
        public string ClientSecret { get; init; }
        public string RedirectUri { get; init; } = "http://localhost:17682/";
    }

    public class YandexOAuthProvider : OAuthProvider
    {
        private const string OAuthEndpoint = "https://oauth.yandex.ru";
        private readonly YandexDiskConfig _config;

        public YandexOAuthProvider(IOptions<YandexDiskConfig> options, IHttpClientFactory httpClientFactory)
            : base(httpClientFactory)
        {
            _config = options.Value;
        }

        public override string ClientId => _config.ClientId;
        // Optional: with PKCE Yandex accepts a public client without a secret.
        public override string ClientSecret => _config.ClientSecret;
        protected override string AuthEndpoint => OAuthEndpoint + "/authorize";
        protected override string TokenEndpoint => OAuthEndpoint + "/token";
        protected override string Scopes => "cloud_api:disk.app_folder login:info login:avatar";
        protected override bool RequiresRefreshTokenInResponse => true;
        protected override bool IncludeRedirectUriInTokenRequest => false;

        protected override void FillAuthorizationQuery(NameValueCollection query)
        {
            query["force_confirm"] = "yes";
            query["device_id"] = GetDeviceId();
            query["device_name"] = GetDeviceName();
        }

        // Yandex requires an exact match with the redirect URI registered for the application.
        protected override Uri GetRedirectUri() =>
            Uri.TryCreate(_config.RedirectUri, UriKind.Absolute, out var uri) ? uri : null;

        protected override (string Endpoint, Dictionary<string, string> Form)? GetRevokeRequest(OAuthInfo oAuthInfo)
        {
            if (string.IsNullOrEmpty(oAuthInfo.AccessToken))
                return null;

            return (OAuthEndpoint + "/revoke_token",
                new Dictionary<string, string> { ["access_token"] = oAuthInfo.AccessToken });
        }

        private static string GetDeviceId()
        {
            var device = $"Erapkey:{Environment.MachineName}:{GetUserId()}";
            return OAuthHelper.Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(device)));
        }

        /// <summary>The SID on Windows, as before: Yandex keeps one session per device id.</summary>
        private static string GetUserId()
        {
            if (OperatingSystem.IsWindows())
            {
                using var identity = WindowsIdentity.GetCurrent();
                return identity.User?.Value;
            }

            return Environment.UserName;
        }

        private static string GetDeviceName() =>
            OperatingSystem.IsWindows() ? "Erapkey for Windows"
            : OperatingSystem.IsIOS() ? "Erapkey for iOS"
            : OperatingSystem.IsMacOS() ? "Erapkey for macOS"
            : "Erapkey";
    }
}
