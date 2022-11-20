using AsyncKeyedLock;
using Microsoft.Extensions.Logging;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Helpers.Threading;
using SinglePass.WPF.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace SinglePass.WPF.Services
{
    public class CredentialsCryptoService
    {
        private readonly object _credentialsLock = new();
        private readonly ILogger<CredentialsCryptoService> _logger;
        private readonly CryptoService _cryptoService;
        private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;

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
            CryptoService cryptoService, AsyncKeyedLocker<string> asyncKeyedLocker)
        {
            _logger = logger;
            _cryptoService = cryptoService;
            _asyncKeyedLocker = asyncKeyedLocker;
        }

        public Task<bool> IsCredentialsFileExistAsync()
        {
            return Task.FromResult(File.Exists(Constants.PasswordsFilePath));
        }

        public void SetPassword(string password)
        {
            PasswordSecure = SecureStringHelper.MakeSecureString(password);
        }

        public string GetPassword()
        {
            return SecureStringHelper.GetString(PasswordSecure);
        }

        public Task<bool> LoadCredentialsAsync()
        {
            return Task.Run(async () =>
            {
                _logger.LogInformation("Loading credentials from file...");
                bool success = false;
                try
                {
                    // Access to file
                    using var locker = await _asyncKeyedLocker.LockAsync(Constants.PasswordsFilePath);
                    using var fileStream = new FileStream(Constants.PasswordsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    // Just to ensure
                    fileStream.Seek(0, SeekOrigin.Begin);

                    Credentials = _cryptoService.DecryptFromStream<List<Credential>>(fileStream, GetPassword());
                    success = true;
                    _logger.LogInformation($"Credentials loaded from file, count: {Credentials.Count}");
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

            await SaveCredentials();
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

            await SaveCredentials();
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

            await SaveCredentials();
        }

        public async Task<CredentialsMergeResult> Merge(List<Credential> mergingCreds)
        {
            var result = CredentialsMergeResult.SuccessMergeResult;

            foreach (var mergingCred in mergingCreds)
            {
                var existingIndex = _credentials.IndexOf(mergingCred);
                if (existingIndex >= 0)
                {
                    // Update
                    var currentCred = _credentials[existingIndex];
                    if (currentCred.LastModifiedTime < mergingCred.LastModifiedTime)
                    {
                        _credentials[existingIndex] = mergingCred;
                        result.ChangedCredentials.Add(currentCred);
                    }
                }
                else
                {
                    // New
                    _credentials.Add(mergingCred);
                    result.NewCredentials.Add(mergingCred);
                }
            }

            if (result.AnyChanges)
                await SaveCredentials();

            return result;
        }

        public async Task Replace(List<Credential> newCredentials)
        {
            _credentials = newCredentials;
            await SaveCredentials();
        }

        public Task SaveCredentials()
        {
            return Task.Run(async () =>
            {
                try
                {
                    // Access to file
                    using var locker = await _asyncKeyedLocker.LockAsync(Constants.PasswordsFilePath);
                    using var fileStream = new FileStream(Constants.PasswordsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                    // Just to ensure
                    fileStream.Seek(0, SeekOrigin.Begin);

                    _cryptoService.EncryptToStream(Credentials, fileStream, GetPassword());
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
