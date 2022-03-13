using Microsoft.Extensions.Logging;
using PasswordManager.Cloud.Enums;
using PasswordManager.Clouds.Services;
using PasswordManager.Helpers;
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
        private readonly AppSettingsService _appSettingsService;
        private readonly ILogger<SyncService> _logger;
        private readonly object _syncCTSLock = new();

        private CancellationTokenSource _syncCTS;

        public bool SyncInProgress { get; private set; }
        public event Action<bool> SyncReport;

        public SyncService(
            CloudServiceProvider cloudServiceProvider,
            AppSettingsService appSettingsService,
            ILogger<SyncService> logger)
        {
            _cloudServiceProvider = cloudServiceProvider;
            _appSettingsService = appSettingsService;
            _logger = logger;
        }

        public async Task Synchronize()
        {
            SyncReport?.Invoke(true);

            CancellationToken cancellationToken;
            lock (_syncCTSLock)
            {
                _syncCTS?.Cancel();
                _syncCTS = new CancellationTokenSource();
                cancellationToken = _syncCTS.Token;
            }

            try
            {
                var cloudTypesToSync = new List<CloudType>();
                if (_appSettingsService.GoogleDriveEnabled)
                {
                    cloudTypesToSync.Add(CloudType.GoogleDrive);
                }

                using var ms = new MemoryStream();
                using (var fileStream = File.Open(Constants.PasswordsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await fileStream.CopyToAsync(ms, cancellationToken);
                }

                foreach (var cloudType in cloudTypesToSync)
                {
                    // Ensure begining
                    ms.Seek(0, SeekOrigin.Begin);
                    var cloudService = _cloudServiceProvider.GetCloudService(cloudType);
                    await cloudService.Upload(ms, Constants.PasswordsFileName, cancellationToken);
                }

                SyncReport?.Invoke(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Synchronize has been cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
        }
    }
}
