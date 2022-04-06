using Microsoft.Extensions.Logging;
using PasswordManager.Helpers;
using PasswordManager.Helpers.Threading;
using PasswordManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Services
{
    public class CredentialsCryptoService
    {
        private readonly object _credentialsLock = new();
        private readonly ILogger<CredentialsCryptoService> _logger;
        private readonly SyncService _syncService;
        private readonly CryptoService _cryptoService;
        private readonly string _pathToPasswordsFile = Constants.PasswordsFilePath;

        private List<Credential> _credentials = new();
        public List<Credential> Credentials
        {
            get
            {
                lock (_credentialsLock)
                {
                    return _credentials.ToList();
                }
            }
            private set
            {
                lock (_credentialsLock)
                {
                    _credentials = value;
                }
            }
        }

        private SecureString PasswordSecure { get; set; }

        public CredentialsCryptoService(
            ILogger<CredentialsCryptoService> logger,
            SyncService syncService,
            CryptoService cryptoService)
        {
            _logger = logger;
            _syncService = syncService;
            _cryptoService = cryptoService;
        }

        public Task<bool> IsCredentialsFileExistAsync()
        {
            return Task.FromResult(File.Exists(_pathToPasswordsFile));
        }

        public void SetPassword(string password)
        {
            PasswordSecure = SecureStringHelper.MakeSecureString(password);
        }

        public async Task<bool> LoadCredentialsAsync()
        {
            return await Task.Run(() =>
            {
                _logger.LogInformation("Loading credentials from file...");
                bool success = false;
                try
                {
                    // Lock access to file for multithreading environment
                    var hashedPath = HashHelper.GetHash(_pathToPasswordsFile);
                    using var waitHandleLocker = EventWaitHandleLocker.MakeWithEventHandle(true, EventResetMode.AutoReset, hashedPath);

                    // Access to file
                    using var fileStream = new FileStream(_pathToPasswordsFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                    // Just to ensure
                    fileStream.Seek(0, SeekOrigin.Begin);

                    var password = SecureStringHelper.GetString(PasswordSecure);
                    Credentials = _cryptoService.DecryptFromStream<List<Credential>>(fileStream, password);
                    success = true;
                }
                catch (JsonException jex)
                {
                    _logger.LogError(jex, "JSON exception raised");
                }
                catch (CryptographicException cex)
                {
                    _logger.LogError(cex, "Cryptographic exception raised");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, string.Empty);
                }

                return success;
            });
        }

        public async Task AddCredential(Credential credential)
        {
            if (credential is null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            lock (_credentialsLock)
            {
                var index = _credentials.IndexOf(credential);
                if (index >= 0)
                {
                    // Shouldn't be
                    _logger.LogWarning("Duplicate credential found.");
                    return;
                }

                _credentials.Add(credential);
            }

            await SaveCredentialsAndSync();
        }

        public async Task EditCredential(Credential credential)
        {
            if (credential is null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            lock (_credentialsLock)
            {
                var index = _credentials.IndexOf(credential);
                if (index < 0)
                {
                    _logger.LogWarning("Prevented attempt to edit non-existed credential.");
                    return;
                }

                _credentials[index] = credential;
            }

            await SaveCredentialsAndSync();
        }

        public async Task DeleteCredential(Credential credential)
        {
            if (credential is null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            lock (_credentialsLock)
            {
                var index = _credentials.IndexOf(credential);
                if (index < 0)
                {
                    _logger.LogWarning("Prevented attempt to delete non-existed credential.");
                    return;
                }

                _credentials.RemoveAt(index);
            }

            await SaveCredentialsAndSync();
        }

        public async Task<CredentialsMergeResult> Merge(List<Credential> newCredentials)
        {
            var result = new CredentialsMergeResult();

            foreach (var newCredential in newCredentials)
            {
                var currentIndex = _credentials.IndexOf(newCredential);
                if (currentIndex >= 0)
                {
                    // The same found
                    var currentCredential = _credentials[currentIndex];
                    if (currentCredential.LastModifiedTime < newCredential.LastModifiedTime)
                    {
                        _credentials[currentIndex] = newCredential;
                        result.ChangedCredentials.Add(currentCredential);
                    }
                }
                else
                {
                    // New add
                    _credentials.Add(newCredential);
                    result.NewCredentials.Add(newCredential);
                }
            }

            if (result.NewCredentials.Count > 0 || result.ChangedCredentials.Count > 0)
                await SaveCredentialsAndSync();

            return result;
        }

        public async Task Replace(List<Credential> newCredentials)
        {
            _credentials = newCredentials;
            await SaveCredentialsAndSync();
        }

        private async Task SaveCredentialsAndSync()
        {
            await Task.Run(() =>
            {
                // Lock access to file for multithreading environment
                var hashedPath = HashHelper.GetHash(_pathToPasswordsFile);

                using var waitHandleLocker = EventWaitHandleLocker.MakeWithEventHandle(true, EventResetMode.AutoReset, hashedPath);

                try
                {
                    // Access to file
                    using (var fileStream = File.Open(_pathToPasswordsFile, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        // Just to ensure
                        fileStream.Seek(0, SeekOrigin.Begin);

                        var password = SecureStringHelper.GetString(PasswordSecure);
                        _cryptoService.EncryptToStream(Credentials, fileStream, password);
                    }

                    _ = _syncService.Synchronize();
                }
                catch (JsonException jsex)
                {
                    _logger.LogWarning("Failed to serialize credentials settings to file due to exception: {0}", jsex);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, string.Empty);
                }
            });
        }
    }
}
