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
            CryptoService cryptoService)
        {
            _logger = logger;
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

        public string GetPassword()
        {
            return SecureStringHelper.GetString(PasswordSecure);
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
            var result = new CredentialsMergeResult();

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

        public async Task SaveCredentials()
        {
            await Task.Run(() =>
            {
                // Lock access to file for multithreading environment
                var hashedPath = HashHelper.GetHash(_pathToPasswordsFile);

                using var waitHandleLocker = EventWaitHandleLocker.MakeWithEventHandle(true, EventResetMode.AutoReset, hashedPath);

                try
                {
                    // Access to file
                    using var fileStream = File.Open(_pathToPasswordsFile, FileMode.Create, FileAccess.Write, FileShare.Read);
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
