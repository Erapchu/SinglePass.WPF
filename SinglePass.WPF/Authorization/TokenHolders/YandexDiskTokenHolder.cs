using Microsoft.Extensions.Logging;
using SinglePass.WPF.Authorization.Responses;
using SinglePass.WPF.Helpers;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.WPF.Authorization.TokenHolders
{
    public class YandexDiskTokenHolder : ITokenHolder
    {
        private static readonly string TokenPath = Path.Combine(Constants.LocalAppDataDirectoryPath, "yandexDisk.token");
        private readonly Lazy<OAuthInfo> _storedToken;
        private volatile OAuthInfo _currentToken;

        public YandexDiskTokenHolder(ILogger<YandexDiskTokenHolder> logger)
        {
            _storedToken = new Lazy<OAuthInfo>(() =>
            {
                try
                {
                    var encrypted = File.ReadAllBytes(TokenPath);
                    var plaintext = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                    try
                    {
                        return JsonSerializer.Deserialize<OAuthInfo>(plaintext) ?? new OAuthInfo();
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(plaintext);
                    }
                }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
                {
                    return new OAuthInfo();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
                {
                    logger.LogWarning(ex, "Unable to load the Yandex Disk session");
                    return new OAuthInfo();
                }
            });
        }

        public OAuthInfo OAuthInfo => _currentToken ?? _storedToken.Value;

        public async Task SetAndSaveToken(OAuthInfo oauthInfo, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var plaintext = JsonSerializer.SerializeToUtf8Bytes(oauthInfo);
            byte[] encrypted;
            try
            {
                encrypted = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }

            var temporaryPath = $"{TokenPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllBytesAsync(temporaryPath, encrypted, cancellationToken).ConfigureAwait(false);
                File.Move(temporaryPath, TokenPath, overwrite: true);
                _currentToken = oauthInfo;
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }

        public Task RemoveToken()
        {
            File.Delete(TokenPath);
            _currentToken = new OAuthInfo();
            return Task.CompletedTask;
        }
    }
}
