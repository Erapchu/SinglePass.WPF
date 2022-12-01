using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SinglePass.WPF.Authorization.Responses;
using SinglePass.WPF.Helpers;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.WPF.Authorization.TokenHolders
{
    public class GoogleDriveTokenHolder : ITokenHolder
    {
        private readonly ILogger _logger;

        private readonly Lazy<OAuthInfo> _lazyOAuthInfo = new(GetOAuthInfo);
        public OAuthInfo OAuthInfo => _lazyOAuthInfo.Value;

        public GoogleDriveTokenHolder(ILogger<GoogleDriveTokenHolder> logger)
        {
            _logger = logger;
        }

        private static OAuthInfo GetOAuthInfo()
        {
            try
            {
                if (!File.Exists(Constants.GoogleDriveFilePath))
                    return new OAuthInfo();

                var fileContent = File.ReadAllText(Constants.GoogleDriveFilePath);
                return JsonConvert.DeserializeObject<OAuthInfo>(fileContent);
            }
            catch (Exception)
            {
                return new OAuthInfo();
            }
        }

        public Task SetAndSaveToken(OAuthInfo freshOAuthInfo, CancellationToken cancellationToken)
        {
            try
            {
                var oauthInfo = OAuthInfo;
                oauthInfo.AccessToken = freshOAuthInfo.AccessToken;
                oauthInfo.ClientId = freshOAuthInfo.ClientId;
                oauthInfo.ClientSecret = freshOAuthInfo.ClientSecret;
                oauthInfo.CreationTime = freshOAuthInfo.CreationTime;
                oauthInfo.ExpiresIn = freshOAuthInfo.ExpiresIn;
                oauthInfo.RedirectUri = freshOAuthInfo.RedirectUri;
                oauthInfo.RefreshToken = freshOAuthInfo.RefreshToken;
                oauthInfo.TokenType = freshOAuthInfo.TokenType;

                using var fileStream = new FileStream(Constants.GoogleDriveFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using (var writer = new StreamWriter(fileStream))
                {
                    using var jsonWriter = new JsonTextWriter(writer);
                    var ser = new JsonSerializer();
                    ser.Serialize(jsonWriter, oauthInfo);
                    jsonWriter.Flush();
                }
                _logger.LogInformation("Token response saved to file");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }
            return Task.CompletedTask;
        }

        public Task RemoveToken()
        {
            try
            {
                var fileInfo = new FileInfo(Constants.GoogleDriveFilePath);
                fileInfo.Delete();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }
            return Task.CompletedTask;
        }
    }
}
