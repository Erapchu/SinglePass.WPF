using Erapkey.Core.Authorization.Brokers;
using Erapkey.Core.Authorization.TokenHolders;
using Erapkey.Core.Clouds.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Erapkey.Core.Clouds.Services
{
    public class GoogleDriveCloudService : ICloudService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly SemaphoreSlim _tokenLock = new(1);

        public IOAuthProvider OAuthProvider { get; }
        public ITokenHolder TokenHolder { get; }

        public GoogleDriveCloudService(
            GoogleOAuthProvider googleOAuthProvider,
            GoogleDriveTokenHolder googleDriveTokenHolder,
            IHttpClientFactory httpClientFactory)
        {
            OAuthProvider = googleOAuthProvider;
            TokenHolder = googleDriveTokenHolder;
            _httpClientFactory = httpClientFactory;
        }

        private async Task RefreshAccessTokenIfRequired(CancellationToken cancellationToken)
        {
            await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var token = TokenHolder.OAuthInfo;
                if (!token.HasUsableAccessToken())
                {
                    var oauthInfo = await OAuthProvider.RefreshTokenAsync(token, cancellationToken).ConfigureAwait(false);
                    await TokenHolder.SetAndSaveToken(oauthInfo, CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private HttpRequestMessage GetAuthHttpRequestMessage()
        {
            var requestMessage = new HttpRequestMessage();
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenHolder.OAuthInfo.AccessToken);
            return requestMessage;
        }

        private async Task<GoogleDriveFile> FindFile(string fileName, CancellationToken cancellationToken)
        {
            await RefreshAccessTokenIfRequired(cancellationToken).ConfigureAwait(false);
            var client = _httpClientFactory.CreateClient();

            // drive.file only exposes files this app created, so the vault is found by name.
            var query = Uri.EscapeDataString($"name = '{fileName.Replace("\\", "\\\\").Replace("'", "\\'")}' and trashed = false");
            var fields = Uri.EscapeDataString("files(id,name,md5Checksum,createdTime)");
            using var getFilesRM = GetAuthHttpRequestMessage();
            getFilesRM.Method = HttpMethod.Get;
            getFilesRM.RequestUri = new Uri($"https://www.googleapis.com/drive/v3/files?q={query}&fields={fields}&orderBy=createdTime&pageSize=100");
            using var filesResponse = await client.SendAsync(getFilesRM, cancellationToken).ConfigureAwait(false);
            filesResponse.EnsureSuccessStatusCode();
            using var content = filesResponse.Content;
            var jsonResponse = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var fileList = JsonSerializer.Deserialize<GoogleDriveFileList>(jsonResponse);
            if (fileList?.Files is null)
                throw new InvalidDataException("Google Drive returned an invalid file list.");

            // Older versions could leave duplicates behind; always use the oldest one so every device picks the same file.
            return fileList.Files.Find(f => f.Name == fileName);
        }

        public async Task Upload(Stream stream, string fileName, CancellationToken cancellationToken)
        {
            var targetFile = await FindFile(fileName, cancellationToken).ConfigureAwait(false);
            var client = _httpClientFactory.CreateClient();

            var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);
            streamContent.Headers.ContentLength = stream.Length;

            if (targetFile != null)
            {
                // Update
                using var updateFileRM = GetAuthHttpRequestMessage();
                updateFileRM.Method = HttpMethod.Patch;
                updateFileRM.RequestUri = new Uri($"https://www.googleapis.com/upload/drive/v3/files/{targetFile.Id}?uploadType=media");
                updateFileRM.Content = streamContent;
                using var updateResponse = await client.SendAsync(updateFileRM, cancellationToken).ConfigureAwait(false);
                updateResponse.EnsureSuccessStatusCode();
            }
            else
            {
                // Create with metadata and content in one request, so a failure cannot leave an unnamed file behind.
                using var multipart = new MultipartContent("related")
                {
                    JsonContent.Create(new { name = fileName }),
                    streamContent
                };
                using var createFileRM = GetAuthHttpRequestMessage();
                createFileRM.Method = HttpMethod.Post;
                createFileRM.RequestUri = new Uri("https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart");
                createFileRM.Content = multipart;
                using var createResponse = await client.SendAsync(createFileRM, cancellationToken).ConfigureAwait(false);
                createResponse.EnsureSuccessStatusCode();
            }
        }

        public async Task<byte[]> Download(string fileName, CancellationToken cancellationToken)
        {
            var targetFile = await FindFile(fileName, cancellationToken).ConfigureAwait(false);
            if (targetFile is null)
                return null;

            var client = _httpClientFactory.CreateClient();
            using var getFileStreamRM = GetAuthHttpRequestMessage();
            getFileStreamRM.Method = HttpMethod.Get;
            getFileStreamRM.RequestUri = new Uri($"https://www.googleapis.com/drive/v3/files/{targetFile.Id}?alt=media");
            using var fileResponse = await client.SendAsync(getFileStreamRM, cancellationToken).ConfigureAwait(false);
            fileResponse.EnsureSuccessStatusCode();
            return await fileResponse.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> GetRevision(string fileName, CancellationToken cancellationToken)
        {
            var targetFile = await FindFile(fileName, cancellationToken).ConfigureAwait(false);
            if (targetFile is null)
                return null;

            if (string.IsNullOrEmpty(targetFile.Md5Checksum))
                throw new InvalidDataException("Google Drive did not return the file checksum.");

            return targetFile.Md5Checksum.ToLowerInvariant();
        }

        public async Task<BaseUserInfo> GetUserInfo(CancellationToken cancellationToken)
        {
            await RefreshAccessTokenIfRequired(cancellationToken).ConfigureAwait(false);
            var client = _httpClientFactory.CreateClient();

            using var requestMessage = GetAuthHttpRequestMessage();

            requestMessage.Method = HttpMethod.Get;
            requestMessage.RequestUri = new Uri("https://www.googleapis.com/oauth2/v3/userinfo");
            using var userInfoResponse = await client.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
            userInfoResponse.EnsureSuccessStatusCode();
            using var content = userInfoResponse.Content;
            var jsonResponse = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var userInfo = JsonSerializer.Deserialize<GoogleUserInfo>(jsonResponse);
            return userInfo.ToBaseUserInfo();
        }
    }
}
