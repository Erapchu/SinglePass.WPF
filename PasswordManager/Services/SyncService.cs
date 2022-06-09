using Microsoft.Extensions.Logging;
using PasswordManager.Cloud.Enums;
using PasswordManager.Clouds.Services;
using PasswordManager.Helpers;
using PasswordManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Services
{
    public class SyncService
    {
        private readonly CloudServiceProvider _cloudServiceProvider;
        private readonly ILogger<SyncService> _logger;
        private readonly CredentialsCryptoService _credentialsCryptoService;
        private readonly CryptoService _cryptoService;
        private readonly HashSet<CloudType> _syncClouds = new();
        // TODO: implement IDisposable
        private readonly CancellationTokenSource _syncCTS = new();

        public event Action<string> SyncStateChanged;

        public SyncService(
            CloudServiceProvider cloudServiceProvider,
            CredentialsCryptoService credentialsCryptoService,
            CryptoService cryptoService,
            ILogger<SyncService> logger)
        {
            _cloudServiceProvider = cloudServiceProvider;
            _logger = logger;
            _credentialsCryptoService = credentialsCryptoService;
            _cryptoService = cryptoService;
        }

        public async Task<CredentialsMergeResult> Synchronize(CloudType cloudType, Func<Task<string>> userPasswordRequired)
        {
            CredentialsMergeResult mergeResult = null;

            try
            {
                lock (_syncClouds)
                {
                    if (!_syncClouds.Add(cloudType))
                    {
                        throw new Exception($"Sync for \'{cloudType}\' is in processing");
                    }
                }

                var token = _syncCTS.Token;
                var cloudService = _cloudServiceProvider.GetCloudService(cloudType);

                SyncStateChanged?.Invoke(PasswordManager.Language.Properties.Resources.DownloadingFile);
                using var cloudFileStream = await cloudService.Download(Constants.PasswordsFileName, token);
                if (cloudFileStream is null)
                {
                    throw new Exception("File doesn't exists on cloud");
                }

                // File exists
                List<Credential> cloudCredentials = null;
                var password = _credentialsCryptoService.GetPassword();

                do
                {
                    try
                    {
                        SyncStateChanged?.Invoke(PasswordManager.Language.Properties.Resources.Decrypting);
                        cloudCredentials = _cryptoService.DecryptFromStream<List<Credential>>(cloudFileStream, password);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, string.Empty);
                    }

                    if (cloudCredentials is null)
                    {
                        password = await userPasswordRequired();
                        if (password is null)
                            throw new Exception("Merge operation cancelled by user"); // Cancel operation
                    }
                }
                while (cloudCredentials is null);

                // Merge
                mergeResult = await _credentialsCryptoService.Merge(cloudCredentials);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning($"Synchronize for \'{cloudType}\' has been cancelled");
                mergeResult = CredentialsMergeResult.FailureMergeResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
                mergeResult = CredentialsMergeResult.FailureMergeResult;
            }
            finally
            {
                SyncStateChanged?.Invoke(string.Empty);
                lock (_syncClouds)
                {
                    _syncClouds.Remove(cloudType);
                    _logger.LogInformation($"Synchronize for \'{cloudType}\' completed");
                }
            }

            return mergeResult;
        }

        public async Task<bool> Upload(CloudType cloudType)
        {
            try
            {
                lock (_syncClouds)
                {
                    if (!_syncClouds.Add(cloudType))
                    {
                        throw new Exception($"Sync for \'{cloudType}\' is in processing");
                    }
                }

                var token = _syncCTS.Token;
                var cloudService = _cloudServiceProvider.GetCloudService(cloudType);
                SyncStateChanged?.Invoke(PasswordManager.Language.Properties.Resources.Uploading);
                using var fileStream = File.Open(Constants.PasswordsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                // Ensure begining
                fileStream.Seek(0, SeekOrigin.Begin);
                await cloudService.Upload(fileStream, Constants.PasswordsFileName, token);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
                return false;
            }
            finally
            {
                SyncStateChanged?.Invoke(string.Empty);
                lock (_syncClouds)
                {
                    _syncClouds.Remove(cloudType);
                    _logger.LogInformation($"Synchronize for \'{cloudType}\' completed");
                }
            }
        }
    }
}
