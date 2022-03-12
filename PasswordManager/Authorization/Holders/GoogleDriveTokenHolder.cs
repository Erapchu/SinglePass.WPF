using Microsoft.Extensions.Logging;
using PasswordManager.Authorization.Interfaces;
using PasswordManager.Authorization.Responses;
using PasswordManager.Helpers;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Authorization.Holders
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
            if (!File.Exists(Constants.GoogleDriveFilePath))
                return;

            using var fileStream = new FileStream(Constants.GoogleDriveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Token = JsonSerializer.Deserialize<GoogleDriveTokenResponse>(fileStream);
        }

        public async Task SetAndSaveToken(string tokenResponse, CancellationToken cancellationToken)
        {
            var tempToken = JsonSerializer.Deserialize<GoogleDriveTokenResponse>(tokenResponse);

            if (!string.IsNullOrWhiteSpace(tempToken.RefreshToken))
            {
                // Refresh token reset
                Token = tempToken;
            }
            else
            {
                // Leave refresh token as is
                var refreshToken = Token.RefreshToken;
                Token = new GoogleDriveTokenResponse()
                {
                    AccessToken = tempToken.AccessToken,
                    ExpiresIn = tempToken.ExpiresIn,
                    InitDate = tempToken.InitDate,
                    Scope = tempToken.Scope,
                    TokenType = tempToken.TokenType,
                    RefreshToken = refreshToken
                };
            }

            using var fileStream = new FileStream(Constants.GoogleDriveFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await JsonSerializer.SerializeAsync(fileStream, Token, cancellationToken: cancellationToken);
            _logger.LogInformation("Token response saved to file");
        }
    }
}
