using Microsoft.Extensions.Logging;
using PasswordManager.Helpers;
using PasswordManager.Helpers.Threading;
using PasswordManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Services
{
    public class SettingsService
    {
        private readonly byte[] _predefinedKeyPart = new byte[AesCryptographyHelper.KeyLength]
        {
            97, 238, 238, 23, 235, 212, 131, 197, 191, 5, 236, 111, 81, 47, 125, 191,
            211, 41, 121, 148, 132, 70, 204, 94, 133, 220, 255, 225, 169, 242, 67, 114
        };
        private readonly object _credentialsLock = new();
        private readonly ILogger<SettingsService> _logger;

        private List<Credential> _credentials;
        private byte[] _keyBytes;

        public List<Credential> Credentials
        {
            get
            {
                lock (_credentialsLock)
                {
                    return _credentials.ToList();
                }
            }
        }

        public SettingsService(ILogger<SettingsService> logger)
        {
            _logger = logger;
            _credentials = new List<Credential>();
        }

        public async Task LoadCredentialsAsync(string password)
        {
            _credentials = await Task.Run(() =>
            {
                _logger.LogInformation("Loading credentials from file...");
                var credentials = new List<Credential>();
                try
                {
                    // Lock access to file for multithreading environment
                    var pathToPasswordsFile = Constants.PasswordsFilePath;
                    var hashedPath = GetHashForPath(pathToPasswordsFile);
                    using var waitHandleLocker = EventWaitHandleLocker.MakeWithEventHandle(true, EventResetMode.AutoReset, hashedPath);

                    if (!File.Exists(pathToPasswordsFile))
                    {
                        _logger.LogInformation("File is not exists.");
                        return credentials;
                    }

                    // Access to file
                    using var fileStream = new FileStream(pathToPasswordsFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                    // Just to ensure
                    fileStream.Seek(0, SeekOrigin.Begin);

                    using var br = new BinaryReader(fileStream);
                    var ivLength = AesCryptographyHelper.IVLength;
                    var ivBytes = new byte[ivLength];
                    br.Read(ivBytes);
                    var encryptedBytes = new byte[fileStream.Length - ivLength];
                    br.Read(encryptedBytes);

                    RestructureKeyBytes(password);

                    var jsonText = AesCryptographyHelper.DecryptStringFromBytes(encryptedBytes, _keyBytes, ivBytes);

                    credentials = JsonSerializer.Deserialize<List<Credential>>(jsonText);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, string.Empty);
                }

                return credentials;
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

            await SaveCredentialsAsync();
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

            await SaveCredentialsAsync();
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

            await SaveCredentialsAsync();
        }

        public async Task SaveCredentialsAsync()
        {
            await Task.Run(() =>
            {
                // Lock access to file for multithreading environment
                var pathToPasswordsFile = Constants.PasswordsFilePath;
                var hashedPath = GetHashForPath(pathToPasswordsFile);

                var appdataDir = new DirectoryInfo(Constants.RoamingAppDataDirectoryPath);
                if (!appdataDir.Exists)
                {
                    appdataDir.Create();
                }

                using var waitHandleLocker = EventWaitHandleLocker.MakeWithEventHandle(true, EventResetMode.AutoReset, hashedPath);

                try
                {
                    // Access to file
                    using var fileStream = File.Open(pathToPasswordsFile, FileMode.Create, FileAccess.Write, FileShare.Read);

                    // Just to ensure
                    fileStream.Seek(0, SeekOrigin.Begin);

                    // Generate new IV for each new saving
                    using var aesObj = Aes.Create();
                    var ivBytes = aesObj.IV;

                    // Get copy and serialize
                    var credentials = Credentials;
                    var jsonText = JsonSerializer.Serialize(credentials);
                    var encryptedBytes = AesCryptographyHelper.EncryptStringToBytes(jsonText, _keyBytes, ivBytes);

                    using var bw = new BinaryWriter(fileStream);
                    bw.Write(ivBytes);
                    bw.Write(encryptedBytes);
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

        /// <summary>
        /// Replaces pre-defined key bytes according to user password.
        /// </summary>
        /// <param name="password">User password.</param>
        private void RestructureKeyBytes(string password)
        {
            _keyBytes = new byte[AesCryptographyHelper.KeyLength];
            Array.Copy(_predefinedKeyPart, _keyBytes, AesCryptographyHelper.KeyLength);

            var passBytes = Encoding.UTF8.GetBytes(password);
            for (int i = 0; i < passBytes.Length; i++)
            {
                _keyBytes[i] = passBytes[i];
            }
        }

        private static string GetHashForPath(string path)
        {
            var passPathBytes = Encoding.UTF8.GetBytes(path);
            var hashData = SHA256.HashData(passPathBytes);
            var hashedPath = Encoding.UTF8.GetString(hashData);
            return hashedPath;
        }
    }
}
