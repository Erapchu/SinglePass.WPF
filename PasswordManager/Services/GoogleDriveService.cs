using PasswordManager.Authorization.Services;
using PasswordManager.Helpers;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Services
{
    public class GoogleDriveService
    {
        private readonly GoogleDriveTokenHolder _googleDriveTokenHolder;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly OAuthBrokerProviderService _oAuthBrokerProviderService;

        public GoogleDriveService(
            GoogleDriveTokenHolder googleDriveTokenHolder,
            IHttpClientFactory httpClientFactory,
            OAuthBrokerProviderService oAuthBrokerProviderService)
        {
            _googleDriveTokenHolder = googleDriveTokenHolder;
            _httpClientFactory = httpClientFactory;
            _oAuthBrokerProviderService = oAuthBrokerProviderService;
        }

        public async Task Synchronize()
        {
            if (_googleDriveTokenHolder.Token.RefreshRequired)
            {
                var authorizationBroker = _oAuthBrokerProviderService.GetAuthorizationBroker(Authorization.Enums.CloudType.GoogleDrive);
                await authorizationBroker.RefreshAccessToken(CancellationToken.None);
            }
            var accessToken = _googleDriveTokenHolder.Token.AccessToken;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var fileStream = File.Open(Constants.PasswordsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var metaContent = JsonContent.Create(new { name = Constants.PasswordsFileName });
            var streamContent = new StreamContent(fileStream);
            var multipart = new MultipartContent { metaContent, streamContent };
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);
            streamContent.Headers.ContentLength = fileStream.Length;
            var result = await client.PostAsync("https://www.googleapis.com/upload/drive/v3/files?uploadtype=multipart", multipart);
        }
    }
}
