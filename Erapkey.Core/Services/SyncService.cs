using Microsoft.Extensions.Logging;
using Erapkey.Core.Clouds.Enums;
using Erapkey.Core.Clouds.Services;
using Erapkey.Core.Helpers;
using Erapkey.Core.Localization;
using Erapkey.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Erapkey.Core.Services
{
    public class SyncService : IDisposable
    {
        // Each attempt downloads the cloud copy again, so a retry merges whatever another device uploaded meanwhile.
        private const int MaxAttempts = 3;

        private readonly CloudServiceProvider _cloudServiceProvider;
        private readonly ILogger<SyncService> _logger;
        private readonly CredentialsCryptoService _credentialsCryptoService;
        private readonly CryptoService _cryptoService;
        private readonly HashSet<CloudType> _syncClouds = new();
        private readonly CancellationTokenSource _syncCTS = new();
        internal bool IsLocked => _credentialsCryptoService.IsLocked;

        /// <summary>Current sync stage for the given cloud, as text for the UI; empty when sync ends.</summary>
        public event Action<CloudType, string> SyncStateChanged;

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
                        ? Erapkey.Language.Properties.Resources.DownloadingFile
                        : LocalizationManager.Instance["SyncRetrying"]);
                    var cloudBytes = await cloudService.Download(Constants.PasswordsFileName, token);
                    token.ThrowIfCancellationRequested();

                    string expectedRevision = null;
                    var needsUpload = true;
                    if (cloudBytes is not null)
                    {
                        expectedRevision = Convert.ToHexStringLower(MD5.HashData(cloudBytes));
                        var (incoming, cloudKey, password) = await DecryptCloudVault(
                            cloudType, cloudBytes, cloudPassword, userPasswordRequired, token);
                        if (incoming is null)
                            return CredentialsMergeResult.FailureMergeResult; // Cancelled by the user

                        using (cloudKey)
                        {
                            cloudPassword = password ?? cloudPassword;
                            SyncStateChanged?.Invoke(cloudType, LocalizationManager.Instance["Merging"]);
                            await _credentialsCryptoService.MergeFromCloud(incoming, cloudKey, password, result);
                        }
                        needsUpload = result.CloudNeedsUpdate;
                    }

                    if (!needsUpload)
                        return result;

                    SyncStateChanged?.Invoke(cloudType, Erapkey.Language.Properties.Resources.Uploading);
                    var localBytes = await _credentialsCryptoService.ReadVaultFileAsync();

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
            catch (UnsupportedVaultVersionException ex)
            {
                // No password can open it: the cloud copy was written by a newer Erapkey on another device.
                _logger.LogError(ex, "The '{CloudType}' vault was written by a newer version", cloudType);
                return new CredentialsMergeResult
                {
                    Success = false,
                    FailureReason = LocalizationManager.Instance["CloudVaultNewerVersion"]
                };
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
        /// Opens the cloud vault: with this device's vault key when the cloud copy uses it (no password, no Argon2),
        /// otherwise with a password entered in a previous attempt or kept for the first sync of a new or converted vault,
        /// then by asking the user until a password works or the user cancels. Returns a null vault when the user cancels.
        /// The key is that of the cloud file (null for Erapkey 1.x files); the password is null when the key opened it.
        /// Throws <see cref="UnsupportedVaultVersionException"/> for a copy of a newer Erapkey, without asking.
        /// </summary>
        private async Task<(PasswordVault Vault, VaultKey Key, string Password)> DecryptCloudVault(
            CloudType cloudType, byte[] cloudBytes, string knownPassword, Func<Task<string>> userPasswordRequired, CancellationToken token)
        {
            SyncStateChanged?.Invoke(cloudType, Erapkey.Language.Properties.Resources.Decrypting);
            var byKey = await Task.Run(() => (
                Opened: _credentialsCryptoService.TryReadWithCurrentKey(cloudBytes, out var vault, out var key), vault, key), token);
            if (byKey.Opened)
                return (byKey.vault, byKey.key, null);

            var candidates = new List<string>();
            foreach (var candidate in new[] { knownPassword, _credentialsCryptoService.FirstSyncPassword })
            {
                if (candidate is not null && !candidates.Contains(candidate))
                    candidates.Add(candidate);
            }

            for (var i = 0; ; i++)
            {
                var password = i < candidates.Count ? candidates[i] : await userPasswordRequired();
                if (password is null)
                    return (null, null, null);

                SyncStateChanged?.Invoke(cloudType, Erapkey.Language.Properties.Resources.Decrypting);
                try
                {
                    var read = await Task.Run(() =>
                    {
                        using var stream = new MemoryStream(cloudBytes, writable: false);
                        var vault = CredentialsCryptoService.ReadVault(_cryptoService, stream, password, out var key);
                        return (vault, key);
                    }, token);
                    return (read.vault, read.key, password);
                }
                catch (Exception ex) when (ex is not OperationCanceledException and not UnsupportedVaultVersionException)
                {
                    _logger.LogWarning(ex, "Could not decrypt the cloud vault");
                }

                token.ThrowIfCancellationRequested();
            }
        }

        internal void CancelTransfers() => _syncCTS.Cancel();

        public void Dispose() => _syncCTS.Dispose();
    }
}
