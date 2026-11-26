using Erapkey.Core.Authorization.Brokers;
using Erapkey.Core.Authorization.Responses;
using Erapkey.Core.Authorization.TokenHolders;
using Erapkey.Core.Clouds.Models;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Erapkey.Core.Clouds.Services
{
    public class YandexDiskCloudService : ICloudService
    {
        internal const string HttpClientName = "YandexDisk";
        private const string ResourcesEndpoint = "https://cloud-api.yandex.net/v1/disk/resources";
        private static readonly TimeSpan ProactiveRefreshInterval = TimeSpan.FromDays(90);
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly SemaphoreSlim _tokenLock = new(1);

        public YandexDiskCloudService(YandexOAuthProvider oauthProvider, YandexDiskTokenHolder tokenHolder,
            IHttpClientFactory httpClientFactory)
        {
            OAuthProvider = oauthProvider;
            TokenHolder = tokenHolder;
            _httpClientFactory = httpClientFactory;
        }

        public IOAuthProvider OAuthProvider { get; }
        public ITokenHolder TokenHolder { get; }

        public async Task Upload(Stream stream, string fileName, CancellationToken cancellationToken)
        {
            var path = Uri.EscapeDataString("app:/" + fileName);
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            using var linkResponse = await GetAuthorizedAsync(client,
                $"{ResourcesEndpoint}/upload?path={path}&overwrite=true", cancellationToken).ConfigureAwait(false);
            var uploadUri = await ReadLinkAsync(linkResponse, cancellationToken).ConfigureAwait(false);

            using var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            using var response = await client.PutAsync(uploadUri, content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        public async Task<byte[]> Download(string fileName, CancellationToken cancellationToken)
        {
            var path = Uri.EscapeDataString("app:/" + fileName);
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            using var linkResponse = await GetAuthorizedAsync(client,
                $"{ResourcesEndpoint}/download?path={path}", cancellationToken).ConfigureAwait(false);
            if (linkResponse.StatusCode == HttpStatusCode.NotFound)
                return null;

            var downloadUri = await ReadLinkAsync(linkResponse, cancellationToken).ConfigureAwait(false);
            return await client.GetByteArrayAsync(downloadUri, cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> GetRevision(string fileName, CancellationToken cancellationToken)
        {
            var path = Uri.EscapeDataString("app:/" + fileName);
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await GetAuthorizedAsync(client,
                $"{ResourcesEndpoint}?path={path}&fields=md5", cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            var resource = await response.Content.ReadFromJsonAsync<ResourceInfo>(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(resource?.Md5))
                throw new InvalidDataException("Yandex Disk did not return the file checksum.");

            return resource.Md5.ToLowerInvariant();
        }

        public async Task<BaseUserInfo> GetUserInfo(CancellationToken cancellationToken)
        {
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await GetAuthorizedAsync(client, "https://login.yandex.ru/info?format=json",
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var profile = await response.Content.ReadFromJsonAsync<UserProfile>(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("Yandex ID returned an empty profile.");

            return new BaseUserInfo
            {
                UserName = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.Login : profile.DisplayName,
                ProfileUrl = profile.IsAvatarEmpty || string.IsNullOrEmpty(profile.AvatarId)
                    ? null
                    : $"https://avatars.yandex.net/get-yapic/{Uri.EscapeDataString(profile.AvatarId)}/islands-68"
            };
        }

        private async Task<HttpResponseMessage> GetAuthorizedAsync(HttpClient client, string url,
            CancellationToken cancellationToken)
        {
            var accessToken = await GetAccessTokenAsync(null, cancellationToken).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            response.Dispose();
            var renewedToken = await GetAccessTokenAsync(accessToken, cancellationToken).ConfigureAwait(false);
            using var retry = new HttpRequestMessage(HttpMethod.Get, url);
            retry.Headers.Authorization = new AuthenticationHeaderValue("OAuth", renewedToken);
            return await client.SendAsync(retry, cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> GetAccessTokenAsync(string rejectedToken, CancellationToken cancellationToken)
        {
            await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var token = TokenHolder.OAuthInfo;
                var now = DateTimeOffset.UtcNow;
                if (string.IsNullOrEmpty(token.AccessToken) || token.ExpirationTime <= now.AddMinutes(1)
                    || token.AccessToken == rejectedToken)
                {
                    token = await RefreshAndSaveAsync(token, cancellationToken).ConfigureAwait(false);
                }
                else if (token.CreationTime <= now - ProactiveRefreshInterval)
                {
                    // Yandex refresh tokens expire together with the access token (about a year), so waiting
                    // for expiry means a forced sign-in whenever the app was not running at that moment.
                    // Renew early instead, as Yandex recommends. Best effort: the current token still works.
                    try
                    {
                        token = await RefreshAndSaveAsync(token, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception) when (!cancellationToken.IsCancellationRequested)
                    {
                    }
                }

                return token.AccessToken;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private async Task<OAuthInfo> RefreshAndSaveAsync(OAuthInfo token, CancellationToken cancellationToken)
        {
            var renewed = await OAuthProvider.RefreshTokenAsync(token, cancellationToken).ConfigureAwait(false);
            // A successful refresh invalidates the previous refresh token, so persist it even if cancelled now.
            await TokenHolder.SetAndSaveToken(renewed, CancellationToken.None).ConfigureAwait(false);
            return renewed;
        }

        private static async Task<Uri> ReadLinkAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            response.EnsureSuccessStatusCode();
            var link = await response.Content.ReadFromJsonAsync<TransferLink>(cancellationToken).ConfigureAwait(false);
            if (!Uri.TryCreate(link?.Href, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidDataException("Yandex Disk returned an invalid transfer link.");

            return uri;
        }

        private sealed record TransferLink(string Href);

        private sealed record ResourceInfo([property: JsonPropertyName("md5")] string Md5);

        private sealed record UserProfile(
            string Login,
            [property: JsonPropertyName("display_name")] string DisplayName,
            [property: JsonPropertyName("is_avatar_empty")] bool IsAvatarEmpty,
            [property: JsonPropertyName("default_avatar_id")] string AvatarId);
    }
}
