using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SinglePass.WPF.Authorization.Helpers;
using SinglePass.WPF.Authorization.Responses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.WPF.Authorization.Brokers
{
    public class YandexDiskConfig
    {
        public string ClientId { get; init; }
        public string ClientSecret { get; init; }
        public string RedirectUri { get; init; } = "http://localhost:17682/";
    }

    public class YandexOAuthProvider : IOAuthProvider
    {
        private const string OAuthEndpoint = "https://oauth.yandex.ru";
        private const string Scopes = "cloud_api:disk.app_folder login:info login:avatar";
        private readonly YandexDiskConfig _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public YandexOAuthProvider(IOptions<YandexDiskConfig> options, IHttpClientFactory httpClientFactory)
        {
            _config = options.Value;
            _httpClientFactory = httpClientFactory;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_config.ClientId)
            && !string.IsNullOrWhiteSpace(_config.ClientSecret);

        public async Task<OAuthInfo> AuthorizeAsync(CancellationToken cancellationToken)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Yandex Disk OAuth credentials are missing.");

            if (!Uri.TryCreate(_config.RedirectUri, UriKind.Absolute, out var redirectUri)
                || !redirectUri.IsLoopback || redirectUri.Scheme != Uri.UriSchemeHttp
                || !redirectUri.AbsoluteUri.EndsWith('/') || redirectUri.UserInfo.Length != 0
                || redirectUri.Query.Length != 0 || redirectUri.Fragment.Length != 0)
            {
                throw new InvalidOperationException("Yandex Disk requires an HTTP loopback redirect URI ending in a slash.");
            }

            var state = Base64Url(RandomNumberGenerator.GetBytes(32));
            var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
            var query = OAuthHelper.GetHttpQSCollection();
            query["response_type"] = "code";
            query["client_id"] = _config.ClientId;
            query["redirect_uri"] = redirectUri.AbsoluteUri;
            query["scope"] = Scopes;
            query["state"] = state;
            query["code_challenge"] = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            query["code_challenge_method"] = "S256";
            query["force_confirm"] = "yes";
            query["device_id"] = GetDeviceId();
            query["device_name"] = "SinglePass for Windows";

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            timeout.Token.ThrowIfCancellationRequested();
            using var listener = StartCallbackListener(redirectUri);
            using var browser = Process.Start(new ProcessStartInfo($"{OAuthEndpoint}/authorize?{query}")
            {
                UseShellExecute = true
            });
            var callback = await OAuthHelper.GetResponseFromListener(listener,
                "SinglePass: return to the application to finish signing in.", timeout.Token).ConfigureAwait(false);

            if (callback.State != state || callback.Error != null || string.IsNullOrEmpty(callback.Code))
                throw new InvalidOperationException("Yandex Disk authorization was declined or the callback was invalid.");

            return await RequestTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = callback.Code,
                ["code_verifier"] = verifier
            }, cancellationToken).ConfigureAwait(false);
        }

        private static HttpListener StartCallbackListener(Uri redirectUri)
        {
            try
            {
                return OAuthHelper.StartListener(redirectUri.AbsoluteUri);
            }
            catch (HttpListenerException ex) when (ex.NativeErrorCode is 32 or 183 or 10048 or 10013)
            {
                throw new YandexCallbackUnavailableException(redirectUri.Port, ex);
            }
        }

        public Task<OAuthInfo> RefreshTokenAsync(OAuthInfo oauthInfo, CancellationToken cancellationToken)
        {
            if (!IsConfigured || oauthInfo.ClientId != _config.ClientId || string.IsNullOrEmpty(oauthInfo.RefreshToken))
                throw new InvalidOperationException("Sign in to Yandex Disk again to renew access.");

            return RequestTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = oauthInfo.RefreshToken
            }, cancellationToken);
        }

        public async Task RevokeTokenAsync(OAuthInfo oauthInfo, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(oauthInfo.AccessToken) || !IsConfigured || oauthInfo.ClientId != _config.ClientId)
                return;

            using var response = await PostAsync("/revoke_token", new Dictionary<string, string>
            {
                ["access_token"] = oauthInfo.AccessToken
            }, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        private async Task<OAuthInfo> RequestTokenAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
        {
            using var response = await PostAsync("/token", form, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var token = JsonConvert.DeserializeObject<TokenResponse>(json);
            if (string.IsNullOrWhiteSpace(token?.AccessToken) || string.IsNullOrWhiteSpace(token.RefreshToken)
                || token.ExpiresIn is not > 0)
            {
                throw new InvalidOperationException("Yandex OAuth returned an incomplete token response.");
            }

            return new OAuthInfo
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                ExpiresIn = token.ExpiresIn.Value,
                CreationTime = DateTimeOffset.UtcNow,
                TokenType = token.TokenType,
                ClientId = _config.ClientId
            };
        }

        private async Task<HttpResponseMessage> PostAsync(string endpoint, Dictionary<string, string> form,
            CancellationToken cancellationToken)
        {
            form["client_id"] = _config.ClientId;
            form["client_secret"] = _config.ClientSecret;
            using var client = _httpClientFactory.CreateClient();
            using var content = new FormUrlEncodedContent(form);
            return await client.PostAsync(OAuthEndpoint + endpoint, content, cancellationToken).ConfigureAwait(false);
        }

        private static string GetDeviceId()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var device = $"SinglePass:{Environment.MachineName}:{identity.User?.Value}";
            return Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(device)));
        }

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    internal sealed class YandexCallbackUnavailableException(int port, Exception innerException)
        : Exception($"Unable to start the Yandex OAuth callback on local port {port}.", innerException)
    {
        public int Port { get; } = port;
    }
}
