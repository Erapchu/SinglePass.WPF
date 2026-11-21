using AsyncKeyedLock;
using Microsoft.Extensions.Logging;
using SinglePass.Core.Clouds.Enums;
using SinglePass.Core.Clouds.Services;
using SinglePass.Core.Helpers;
using SinglePass.Core.Localization;
using SinglePass.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.Core.Services
{
    public class SyncService : IDisposable
    {
        // Each attempt downloads the cloud copy again, so a retry merges whatever another device uploaded meanwhile.
        private const int MaxAttempts = 3;

        private readonly CloudServiceProvider _cloudServiceProvider;
        private readonly ILogger<SyncService> _logger;
        private readonly CredentialsCryptoService _credentialsCryptoService;
        private readonly CryptoService _cryptoService;
        private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
        private readonly HashSet<CloudType> _syncClouds = new();
        private readonly CancellationTokenSource _syncCTS = new();
        internal bool IsLocked => _credentialsCryptoService.IsLocked;

        /// <summary>Current sync stage for the given cloud, as text for the UI; empty when sync ends.</summary>
        public event Action<CloudType, string> SyncStateChanged;

        public SyncService(
            CloudServiceProvider cloudServiceProvider,
            CredentialsCryptoService credentialsCryptoService,
            CryptoService cryptoService,
            AsyncKeyedLocker<string> asyncKeyedLocker,
            ILogger<SyncService> logger)
        {
            _cloudServiceProvider = cloudServiceProvider;
            _logger = logger;
            _credentialsCryptoService = credentialsCryptoService;
            _cryptoService = cryptoService;
            _asyncKeyedLocker = asyncKeyedLocker;
        }

        /// <summary>
        /// Two-way sync: downloads the cloud vault, merges it into the local one and uploads the result
        /// when the cloud copy is missing something. The upload is skipped and the whole cycle repeated
        /// when the cloud copy changed after it was downloaded.
        /// Runs on the caller's context: the password prompt is shown from here.
        /// </summary>
        public async Task<CredentialsMergeResult> Synchronize(CloudType cloudType, Func<Task<string>> userPasswordRequired)
        {
            try
            {
                lock (_syncClouds)
                {
                    if (!_syncClouds.Add(cloudType))
                    {
                        throw new InvalidOperationException($"Sync for \'{cloudType}\' is in processing");
                    }
                }

                var token = _syncCTS.Token;
                var cloudService = _cloudServiceProvider.GetCloudService(cloudType);
                var result = CredentialsMergeResult.SuccessMergeResult;
                string cloudPassword = null;

                for (var attempt = 1; attempt <= MaxAttempts; attempt++)
                {
                    SyncStateChanged?.Invoke(cloudType, attempt == 1
                        ? SinglePass.Language.Properties.Resources.DownloadingFile
                        : LocalizationManager.Instance["SyncRetrying"]);
                    var cloudBytes = await cloudService.Download(Constants.PasswordsFileName, token);
                    token.ThrowIfCancellationRequested();

                    string expectedRevision = null;
                    var needsUpload = true;
                    if (cloudBytes is not null)
                    {
                        expectedRevision = Convert.ToHexStringLower(MD5.HashData(cloudBytes));
                        var (incoming, password, isLegacy) = await DecryptCloudVault(
                            cloudType, cloudBytes, cloudPassword, userPasswordRequired, token);
                        if (incoming is null)
                            return CredentialsMergeResult.FailureMergeResult; // Cancelled by the user

                        cloudPassword = password;
                        SyncStateChanged?.Invoke(cloudType, LocalizationManager.Instance["Merging"]);
                        await _credentialsCryptoService.MergeFromCloud(incoming, password, result);
                        needsUpload = result.CloudNeedsUpdate || isLegacy;
                    }

                    if (!needsUpload)
                        return result;

                    SyncStateChanged?.Invoke(cloudType, SinglePass.Language.Properties.Resources.Uploading);
                    var localBytes = await ReadLocalVault();

                    // Neither provider offers an atomic conditional write, so check right before uploading.
                    var currentRevision = await cloudService.GetRevision(Constants.PasswordsFileName, token);
                    if (!string.Equals(currentRevision, expectedRevision, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("The '{CloudType}' vault changed during sync (attempt {Attempt})", cloudType, attempt);
                        continue;
                    }

                    using var uploadStream = new MemoryStream(localBytes, writable: false);
                    await cloudService.Upload(uploadStream, Constants.PasswordsFileName, token);
                    result.CloudUpdated = true;
                    return result;
                }

                return new CredentialsMergeResult
                {
                    Success = false,
                    FailureReason = LocalizationManager.Instance["CloudVaultKeptChanging"]
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Synchronize for '{CloudType}' has been cancelled", cloudType);
                return CredentialsMergeResult.FailureMergeResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Synchronize for '{CloudType}' failed", cloudType);
                return CredentialsMergeResult.FailureMergeResult;
            }
            finally
            {
                SyncStateChanged?.Invoke(cloudType, string.Empty);
                lock (_syncClouds)
                {
                    _syncClouds.Remove(cloudType);
                    _logger.LogInformation("Synchronize for '{CloudType}' completed", cloudType);
                }
            }
        }

        /// <summary>
        /// Decrypts the cloud vault with the local master password, then with the password entered during
        /// a previous attempt, then asks the user. Returns a null vault when the user cancels.
        /// </summary>
        private async Task<(PasswordVault Vault, string Password, bool IsLegacy)> DecryptCloudVault(
            CloudType cloudType, byte[] cloudBytes, string knownPassword, Func<Task<string>> userPasswordRequired, CancellationToken token)
        {
            var candidates = new List<string> { _credentialsCryptoService.GetPassword() };
            if (knownPassword is not null && knownPassword != candidates[0])
                candidates.Add(knownPassword);

            for (var i = 0; ; i++)
            {
                var password = i < candidates.Count ? candidates[i] : await userPasswordRequired();
                if (password is null)
                    return (null, null, false);

                SyncStateChanged?.Invoke(cloudType, SinglePass.Language.Properties.Resources.Decrypting);
                try
                {
                    var vault = await Task.Run(() =>
                    {
                        using var stream = new MemoryStream(cloudBytes, writable: false);
                        var read = CredentialsCryptoService.ReadVault(_cryptoService, stream, password, out var isLegacy);
                        return (read, isLegacy);
                    }, token);
                    return (vault.read, password, vault.isLegacy);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Could not decrypt the cloud vault");
                }

                token.ThrowIfCancellationRequested();
            }
        }

        private async Task<byte[]> ReadLocalVault()
        {
            using var locker = await _asyncKeyedLocker.LockAsync(Constants.PasswordsFilePath).ConfigureAwait(false);
            return await File.ReadAllBytesAsync(Constants.PasswordsFilePath).ConfigureAwait(false);
        }

        internal void CancelTransfers() => _syncCTS.Cancel();

        public void Dispose() => _syncCTS.Dispose();
    }
}
