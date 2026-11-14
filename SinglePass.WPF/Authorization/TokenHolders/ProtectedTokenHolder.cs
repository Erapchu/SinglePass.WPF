using Microsoft.Extensions.Logging;
using SinglePass.WPF.Authorization.Responses;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.WPF.Authorization.TokenHolders
{
    public abstract class ProtectedTokenHolder : ITokenHolder
    {
        private readonly string _tokenPath;
        private readonly string _legacyPath;
        private readonly ILogger _logger;
        private readonly object _gate = new();
        private OAuthInfo _token;

        protected ProtectedTokenHolder(string tokenPath, ILogger logger, string legacyPath = null)
        {
            _tokenPath = tokenPath;
            _logger = logger;
            _legacyPath = legacyPath;
        }

        public OAuthInfo OAuthInfo
        {
            get
            {
                lock (_gate)
                    return _token ??= LoadToken();
            }
        }

        private OAuthInfo LoadToken()
        {
            try
            {
                var encrypted = File.ReadAllBytes(_tokenPath);
                var plaintext = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                OAuthInfo token;
                try
                {
                    token = JsonSerializer.Deserialize<OAuthInfo>(plaintext);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(plaintext);
                }

                if (token != null)
                    DeleteLegacyFile();

                return token ?? new OAuthInfo();
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return LoadLegacyToken();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
            {
                _logger.LogWarning(ex, "Unable to load the protected cloud session");
                return new OAuthInfo();
            }
        }

        private OAuthInfo LoadLegacyToken()
        {
            if (_legacyPath == null)
                return new OAuthInfo();

            OAuthInfo token;
            try
            {
                using var stream = File.OpenText(_legacyPath);
                using var reader = new Newtonsoft.Json.JsonTextReader(stream);
                token = Newtonsoft.Json.JsonSerializer.CreateDefault().Deserialize<OAuthInfo>(reader);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return new OAuthInfo();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
            {
                _logger.LogWarning("Unable to read the legacy cloud session");
                return new OAuthInfo();
            }

            if (token == null)
                return new OAuthInfo();

            try
            {
                WriteProtectedToken(token);
                DeleteLegacyFile();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
            {
                _logger.LogWarning(ex, "Unable to migrate the legacy cloud session");
            }

            return token;
        }

        public Task SetAndSaveToken(OAuthInfo oauthInfo, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteProtectedToken(oauthInfo);
                _token = oauthInfo;
                DeleteLegacyFile();
            }

            return Task.CompletedTask;
        }

        public Task RemoveToken()
        {
            lock (_gate)
            {
                if (_legacyPath != null)
                    File.Delete(_legacyPath);

                File.Delete(_tokenPath);
                _token = new OAuthInfo();
            }

            return Task.CompletedTask;
        }

        private void WriteProtectedToken(OAuthInfo oauthInfo)
        {
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

            var temporaryPath = $"{_tokenPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllBytes(temporaryPath, encrypted);
                File.Move(temporaryPath, _tokenPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private void DeleteLegacyFile()
        {
            if (_legacyPath == null)
                return;

            try
            {
                File.Delete(_legacyPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Unable to remove the legacy cloud session file");
            }
        }
    }
}
