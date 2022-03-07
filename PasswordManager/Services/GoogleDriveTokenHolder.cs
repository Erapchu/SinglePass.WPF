using Microsoft.Extensions.Logging;
using PasswordManager.Authorization.Responses;
using PasswordManager.Helpers;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Services
{
    public class GoogleDriveTokenHolder
    {
        private readonly ILogger _logger;

        public GoogleDriveTokenResponse Token { get; private set; }

        public GoogleDriveTokenHolder(ILogger<GoogleDriveTokenHolder> logger)
        {
            _logger = logger;
            ReadTokenFromFile();
        }

        private void ReadTokenFromFile()
        {
            if (!File.Exists(Constants.GoogleDriveFilePath))
                return;

            using var fileStream = new FileStream(Constants.GoogleDriveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Token = JsonSerializer.Deserialize<GoogleDriveTokenResponse>(fileStream);
        }

        public async Task SetToken(string tokenResponse, CancellationToken cancellationToken)
        {
            Token = JsonSerializer.Deserialize<GoogleDriveTokenResponse>(tokenResponse);
            using var fileStream = new FileStream(Constants.GoogleDriveFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await JsonSerializer.SerializeAsync(fileStream, Token, cancellationToken: cancellationToken);
            _logger.LogInformation("Token response saved to file");
        }
    }
}
