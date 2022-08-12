using Microsoft.Extensions.Logging;
using SinglePass.WPF.Authorization.Responses;
using SinglePass.WPF.Helpers;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.WPF.Authorization.TokenHolders
{
    public class GoogleDriveTokenHolder : ITokenHolder
    {
        private readonly ILogger _logger;

        public ITokenResponse Token { get; private set; }

        public GoogleDriveTokenHolder(ILogger<GoogleDriveTokenHolder> logger)
        {
            _logger = logger;
            ReadToken();
        }

        private void ReadToken()
        {
            try
            {
                if (!File.Exists(Constants.GoogleDriveFilePath))
                    return;

                using var fileStream = new FileStream(Constants.GoogleDriveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                Token = JsonSerializer.Deserialize<GoogleDriveTokenResponse>(fileStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
        }

        public async Task SetAndSaveToken(string tokenResponse, CancellationToken cancellationToken)
        {
            var deserialized = JsonSerializer.Deserialize<GoogleDriveTokenResponse>(tokenResponse);

            if (!string.IsNullOrWhiteSpace(deserialized.RefreshToken))
            {
                // Refresh token reset
                Token = deserialized;
            }
            else
            {
                // Leave refresh token as is
                var refreshToken = Token?.RefreshToken;
                Token = new GoogleDriveTokenResponse()
                {
                    AccessToken = deserialized.AccessToken,
                    ExpiresIn = deserialized.ExpiresIn,
                    InitDate = deserialized.InitDate,
                    Scope = deserialized.Scope,
                    TokenType = deserialized.TokenType,
                    RefreshToken = refreshToken
                };
            }

            using var fileStream = new FileStream(Constants.GoogleDriveFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await JsonSerializer.SerializeAsync(fileStream, Token as GoogleDriveTokenResponse, cancellationToken: cancellationToken);
            _logger.LogInformation("Token response saved to file");
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
                _logger.LogError(ex, string.Empty);
            }
            return Task.CompletedTask;
        }
    }
}
