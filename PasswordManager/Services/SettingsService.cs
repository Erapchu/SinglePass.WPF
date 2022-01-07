using Newtonsoft.Json;
using NLog;
using PasswordManager.Helpers;
using PasswordManager.Helpers.Threading;
using PasswordManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Services
{
    public class SettingsService
    {
        public Lazy<JsonSerializerSettings> _lazyDefaultSerializerSettings = new(() => new JsonSerializerSettings()
        {
            DefaultValueHandling = DefaultValueHandling.Include
        });
        public JsonSerializerSettings DefaultSerializerSettings => _lazyDefaultSerializerSettings.Value;

        private readonly object _credentialsLock = new object();

        private readonly ILogger _logger;
        private List<Credential> _credentials;

        private readonly string _key = "agddhethbqerthnmklutrasdcxzfgttr";

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

        public SettingsService(ILogger logger)
        {
            _logger = logger;

            _credentials = new List<Credential>();
        }

        public async Task LoadCredentialsAsync()
        {
            _credentials = await Task.Run(() =>
            {
                _logger.Info("Loading credentials from file...");
                var credentials = new List<Credential>();
                try
                {
                    // Lock access to file for multithreading environment
                    var pathToPasswordsFile = Constants.PasswordsFilePath;
                    var hashedPath = GetHashForPath(pathToPasswordsFile);
                    using var waitHandleLocker = EventWaitHandleLocker.MakeWithEventHandle(true, EventResetMode.AutoReset, hashedPath);

                    if (!File.Exists(pathToPasswordsFile))
                    {
                        // File is not exists yet
                        _logger.Info("File is not exists.");
                        return credentials;
                    }

                    // Access to file
                    var bufferSize = 4096;
                    using var fileStream = new FileStream(pathToPasswordsFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true);
                    // Just to ensure
                    fileStream.Seek(0, SeekOrigin.Begin);

                    using var br = new BinaryReader(fileStream);
                    var ivLength = AesCryptographyHelper.IVLength;
                    var ivBytes = new byte[ivLength];
                    br.Read(ivBytes);
                    var encryptedBytes = new byte[fileStream.Length - ivLength];
                    br.Read(encryptedBytes);

                    var keyBytes = Encoding.UTF8.GetBytes(_key);

                    var jsonText = AesCryptographyHelper.DecryptStringFromBytes(encryptedBytes, keyBytes, ivBytes);

                    try
                    {
                        credentials = JsonConvert.DeserializeObject<List<Credential>>(jsonText, DefaultSerializerSettings);
                    }
                    catch (JsonException jsex)
                    {
                        _logger.Warn("Failed to deserialize credentials settings file content due to exception: {0}", jsex);

                        if (credentials is null)
                        {
                            _logger.Info("Using empty credentials list");
                            credentials = new();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
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
                    _logger.Warn("Duplicate credential found.");
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
                    _logger.Warn("Prevented attempt to edit non-existed credential.");
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
                    _logger.Warn("Prevented attempt to delete non-existed credential.");
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

                    var keyBytes = Encoding.UTF8.GetBytes(_key);

                    // Generate new IV for each new saving
                    using var aesObj = Aes.Create();
                    var ivBytes = aesObj.IV;

                    // Get copy and serialize
                    var credentials = Credentials;
                    var jsonText = JsonConvert.SerializeObject(credentials, DefaultSerializerSettings);
                    var encryptedBytes = AesCryptographyHelper.EncryptStringToBytes(jsonText, keyBytes, ivBytes);

                    using var bw = new BinaryWriter(fileStream);
                    bw.Write(ivBytes);
                    bw.Write(encryptedBytes);
                }
                catch (JsonException jsex)
                {
                    _logger.Warn("Failed to serialize credentials settings to file due to exception: {0}", jsex);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }
            });
        }

        private static string GetHashForPath(string path)
        {
            var passPathBytes = Encoding.UTF8.GetBytes(path);
            var hashData = System.Security.Cryptography.SHA256.HashData(passPathBytes);
            var hashedPath = Encoding.UTF8.GetString(hashData);
            return hashedPath;
        }
    }
}
