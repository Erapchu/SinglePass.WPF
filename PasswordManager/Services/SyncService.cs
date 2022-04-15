using Microsoft.Extensions.Logging;
using PasswordManager.Cloud.Enums;
using PasswordManager.Clouds.Services;
using PasswordManager.Helpers;
using PasswordManager.Models;
using PasswordManager.Views.InputBox;
using PasswordManager.Views.MessageBox;
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

        public async Task Synchronize(CloudType cloudType)
        {
            lock (_syncClouds)
            {
                if (!_syncClouds.Add(cloudType))
                {
                    _logger.LogInformation($"Sync for \'{cloudType}\' is in processing");
                    return;
                }
            }

            try
            {
                var windowDialogName = MvvmHelper.MainWindowDialogName;
                var token = _syncCTS.Token;
                var cloudService = _cloudServiceProvider.GetCloudService(cloudType);

                using var cloudFileStream = await cloudService.Download(Constants.PasswordsFileName, token);
                if (cloudFileStream != null)
                {
                    // File exists
                    List<Credential> cloudCredentials = null;
                    var password = _credentialsCryptoService.GetPassword();

                    do
                    {
                        try
                        {
                            cloudCredentials = _cryptoService.DecryptFromStream<List<Credential>>(cloudFileStream, password);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, string.Empty);
                        }

                        if (cloudCredentials is null)
                        {
                            password = await MaterialInputBox.ShowAsync(
                                "Input password of cloud file",
                                "Password",
                                windowDialogName,
                                true);

                            if (password is null)
                                return; // Cancel operation
                        }
                    }
                    while (cloudCredentials is null);

                    // Merge
                    var mergeResult = await _credentialsCryptoService.Merge(cloudCredentials);
                    await MaterialMessageBox.ShowAsync(
                        "Credentials successfully merged",
                        mergeResult.ToString(),
                        MaterialMessageBoxButtons.OK,
                        windowDialogName);
                }

                using var fileStream = File.Open(Constants.PasswordsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                // Ensure begining
                fileStream.Seek(0, SeekOrigin.Begin);
                await cloudService.Upload(fileStream, Constants.PasswordsFileName, token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning($"Synchronize for \'{cloudType}\' has been cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
            finally
            {
                lock (_syncClouds)
                {
                    _syncClouds.Remove(cloudType);
                    _logger.LogInformation($"Synchronize for \'{cloudType}\' completed");
                }
            }
        }
    }
}
