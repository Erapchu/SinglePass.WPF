using SinglePass.WPF.Authorization.Brokers;
using SinglePass.WPF.Authorization.TokenHolders;
using SinglePass.WPF.Clouds.Models;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.WPF.Clouds.Services
{
    public class GoogleDriveCloudService : ICloudService
    {
        private readonly IHttpClientFactory _httpClientFactory;

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
            if (!TokenHolder.OAuthInfo.IsValid())
            {
                var oauthInfo = await OAuthProvider.RefreshTokenAsync(TokenHolder.OAuthInfo, cancellationToken).ConfigureAwait(false);
                // Only Googe Drive should re-use the same refresh token and it will not return it again
                oauthInfo.RefreshToken = TokenHolder.OAuthInfo.RefreshToken;
                await TokenHolder.SetAndSaveToken(oauthInfo, cancellationToken).ConfigureAwait(false);
            }
        }

        private HttpRequestMessage GetAuthHttpRequestMessage()
        {
            var requestMessage = new HttpRequestMessage();
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenHolder.OAuthInfo.AccessToken);
            return requestMessage;
        }

        private async Task<GoogleDriveFileList> GetGoogleDriveFileList(CancellationToken cancellationToken)
        {
            await RefreshAccessTokenIfRequired(cancellationToken).ConfigureAwait(false);
            var client = _httpClientFactory.CreateClient();

            using var getFilesRM = GetAuthHttpRequestMessage();
            getFilesRM.Method = HttpMethod.Get;
            getFilesRM.RequestUri = new System.Uri("https://www.googleapis.com/drive/v3/files?q=trashed%3Dfalse");
            using var filesResponse = await client.SendAsync(getFilesRM, cancellationToken).ConfigureAwait(false);
            using var content = filesResponse.Content;
            var jsonResponse = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var fileList = JsonSerializer.Deserialize<GoogleDriveFileList>(jsonResponse);

            return fileList;
        }

        public async Task Upload(Stream stream, string fileName, CancellationToken cancellationToken)
        {
            await RefreshAccessTokenIfRequired(cancellationToken).ConfigureAwait(false);
            var client = _httpClientFactory.CreateClient();

            // Check and search file
            var fileList = await GetGoogleDriveFileList(cancellationToken).ConfigureAwait(false);
            var targetFile = fileList.Files.Find(f => f.Name == fileName);

            // Prepare request
            var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);
            streamContent.Headers.ContentLength = stream.Length;

            if (targetFile != null)
            {
                // Update
                using var updateFileRM = GetAuthHttpRequestMessage();
                updateFileRM.Method = HttpMethod.Patch;
                updateFileRM.RequestUri = new System.Uri($"https://www.googleapis.com/upload/drive/v3/files/{targetFile.Id}?uploadType=media");
                updateFileRM.Content = streamContent;
                using var updateResponse = await client.SendAsync(updateFileRM, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Create
                using var createFileRM = GetAuthHttpRequestMessage();
                createFileRM.Method = HttpMethod.Post;
                createFileRM.RequestUri = new System.Uri("https://www.googleapis.com/upload/drive/v3/files?uploadType=media");
                createFileRM.Content = streamContent;
                using var createResponse = await client.SendAsync(createFileRM, cancellationToken).ConfigureAwait(false);

                // Rename
                using var createContent = createResponse.Content;
                var createContentString = await createContent.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var file = JsonSerializer.Deserialize<GoogleDriveFile>(createContentString);
                using var jsonMetaContent = JsonContent.Create(new { name = fileName });
                using var renameFileRM = GetAuthHttpRequestMessage();
                renameFileRM.Method = HttpMethod.Patch;
                renameFileRM.RequestUri = new System.Uri($"https://www.googleapis.com/drive/v3/files/{file.Id}");
                renameFileRM.Content = jsonMetaContent;
                using var renameResponse = await client.SendAsync(renameFileRM, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<Stream> Download(string fileName, CancellationToken cancellationToken)
        {
            await RefreshAccessTokenIfRequired(cancellationToken).ConfigureAwait(false);

            // Check and search file
            var fileList = await GetGoogleDriveFileList(cancellationToken).ConfigureAwait(false);
            var targetFile = fileList.Files.Find(f => f.Name == fileName);
            if (targetFile is null)
                return null;

            var client = _httpClientFactory.CreateClient();
            using var getFileStreamRM = GetAuthHttpRequestMessage();
            getFileStreamRM.Method = HttpMethod.Get;
            getFileStreamRM.RequestUri = new System.Uri($"https://www.googleapis.com/drive/v3/files/{targetFile.Id}?alt=media");
            var fileResponse = await client.SendAsync(getFileStreamRM, cancellationToken).ConfigureAwait(false);
            var fileContent = fileResponse.Content;
            return await fileContent.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<BaseUserInfo> GetUserInfo(CancellationToken cancellationToken)
        {
            await RefreshAccessTokenIfRequired(cancellationToken).ConfigureAwait(false);
            var client = _httpClientFactory.CreateClient();

            using var requestMessage = GetAuthHttpRequestMessage();

            requestMessage.Method = HttpMethod.Get;
            requestMessage.RequestUri = new System.Uri("https://www.googleapis.com/oauth2/v3/userinfo");
            using var userInfoResponse = await client.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
            using var content = userInfoResponse.Content;
            var jsonResponse = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var userInfo = JsonSerializer.Deserialize<GoogleUserInfo>(jsonResponse);
            return userInfo.ToBaseUserInfo();
        }
    }
}
