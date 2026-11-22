using AsyncKeyedLock;
using Microsoft.Extensions.Logging;
using SinglePass.Core.Helpers;
using SinglePass.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace SinglePass.Core.Services
{
    internal enum CredentialLoadResult
    {
        Loaded,
        Failed,
        /// <summary>The vault was written by a newer SinglePass.</summary>
        NewerVersion
    }

    internal enum PasswordChangeResult
    {
        Changed,
        IncorrectCurrentPassword,
        Failed
    }

    /// <summary>What restoring a backup changed.</summary>
    internal sealed record BackupRestoreResult(
        IReadOnlyList<Credential> Restored, IReadOnlyList<Credential> Reverted, bool GroupsChanged)
    {
        public static BackupRestoreResult Empty { get; } = new([], [], false);
        public bool AnyChanges => Restored.Count > 0 || Reverted.Count > 0 || GroupsChanged;
    }

    /// <summary>An entry in the trash and when it was moved there.</summary>
    internal sealed record TrashedCredential(Credential Credential, DateTimeOffset DeletedAt)
    {
        /// <summary>When the entry is deleted for good.</summary>
        public DateTimeOffset PurgeAt => DeletedAt + PasswordVault.TrashRetention;
    }

    public class CredentialsCryptoService
    {
        private readonly ILogger<CredentialsCryptoService> _logger;
        private readonly CryptoService _cryptoService;
        private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
        private readonly IVaultKeyObserver _keyObserver;
        private readonly object _keyLock = new();
        private readonly string _path;
        private readonly VaultBackups _backups;
        private VaultKey _key;
        private string _firstSyncPassword;

        /// <summary>The file on disk is still in format 1: converting it on unlock failed. The next write converts it.</summary>
        private volatile bool _fileNeedsRewrite;
        private volatile PasswordVault _vault = new();
        private volatile bool _locked;
        internal bool IsLocked => _locked;

        /// <summary>Entries of the vault, without the ones in the trash.</summary>
        public List<Credential> Credentials
        {
            get
            {
                var vault = _vault;
                return vault.Credentials.Where(credential => !IsTrashed(vault, credential.Id)).ToList();
            }
        }

        /// <summary>Entries in the trash, most recently deleted first.</summary>
        internal IReadOnlyList<TrashedCredential> TrashedCredentials
        {
            get
            {
                var vault = _vault;
                return vault.Credentials
                    .Where(credential => IsTrashed(vault, credential.Id))
                    .Select(credential => new TrashedCredential(credential, vault.Trash[credential.Id].DeletedAt.Value))
                    .OrderByDescending(item => item.DeletedAt)
                    .ToList();
            }
        }

        private static bool IsTrashed(PasswordVault vault, Guid credentialId) =>
            vault.Trash.TryGetValue(credentialId, out var state) && state.IsTrashed;

        /// <summary>Backups of the vault file, newest first.</summary>
        internal IReadOnlyList<VaultBackupInfo> Backups => _backups.List();

        internal IReadOnlyList<PasswordGroup> PasswordGroups =>
            _vault.Groups.Values.Where(change => !change.IsDeleted).Select(change => change.Group).ToList();

        internal Dictionary<Guid, Guid> GroupAssignments
        {
            get
            {
                var vault = _vault;
                return vault.Assignments
                    .Where(pair => pair.Value.GroupId is { } groupId &&
                        vault.Groups.TryGetValue(groupId, out var group) && !group.IsDeleted)
                    .ToDictionary(pair => pair.Key, pair => pair.Value.GroupId.Value);
            }
        }

        internal Guid? GetGroupId(Guid credentialId)
        {
            var vault = _vault;
            return vault.Assignments.TryGetValue(credentialId, out var assignment) &&
                assignment.GroupId is { } groupId &&
                vault.Groups.TryGetValue(groupId, out var group) && !group.IsDeleted
                ? groupId
                : null;
        }

        public CredentialsCryptoService(
            ILogger<CredentialsCryptoService> logger,
            CryptoService cryptoService,
            AsyncKeyedLocker<string> asyncKeyedLocker,
            IVaultKeyObserver keyObserver = null)
            : this(logger, cryptoService, asyncKeyedLocker, keyObserver, Constants.PasswordsFilePath, Constants.BackupsDirectoryPath)
        {
        }

        /// <summary>For tests: a vault file and backups folder other than the user's.</summary>
        internal CredentialsCryptoService(
            ILogger<CredentialsCryptoService> logger,
            CryptoService cryptoService,
            AsyncKeyedLocker<string> asyncKeyedLocker,
            IVaultKeyObserver keyObserver,
            string vaultPath,
            string backupsDirectory)
        {
            _logger = logger;
            _cryptoService = cryptoService;
            _asyncKeyedLocker = asyncKeyedLocker;
            _keyObserver = keyObserver;
            _path = vaultPath;
            _backups = new VaultBackups(vaultPath, backupsDirectory);
        }

        /// <summary>A copy of the vault key, for Windows Hello unlock. The caller zeroes it. Null while locked.</summary>
        internal byte[] CopyVaultKey()
        {
            lock (_keyLock)
                return _key?.Key.ToArray();
        }

        /// <summary>
        /// The master password, kept only when this unlock created the vault or converted a vault file of SinglePass 1.x,
        /// and only until the vault locks. The cloud copy then uses another key or the old format, and its first sync needs
        /// the password; without it the user would be asked again right after typing it. Null otherwise.
        /// </summary>
        internal string FirstSyncPassword
        {
            get
            {
                lock (_keyLock)
                    return _firstSyncPassword;
            }
        }

        private void ForgetFirstSyncPassword()
        {
            lock (_keyLock)
                _firstSyncPassword = null;
        }

        private VaultKey CurrentKey()
        {
            lock (_keyLock)
                return _key ?? throw new InvalidOperationException("The password vault is locked.");
        }

        private void ReplaceKey(VaultKey key)
        {
            lock (_keyLock)
            {
                if (!ReferenceEquals(_key, key))
                    _key?.Dispose();
                _key = key;
                if (key is null)
                    _firstSyncPassword = null;
            }
        }

        /// <summary>The user typed the master password and <paramref name="key"/> opens the vault from now on.</summary>
        private void NotifyPasswordEntered(VaultKey key)
        {
            try
            {
                _keyObserver?.OnMasterPasswordEntered(key.Key);
            }
            catch (Exception ex)
            {
                // The vault is already open with this key; an observer must not undo that.
                _logger.LogError(ex, "Failed to pass the vault key on.");
            }
        }

        internal async Task LockAsync()
        {
            _locked = true;
            await ClearAsync().ConfigureAwait(false);
        }

        internal async Task ClearAsync()
        {
            using var locker = await _asyncKeyedLocker.LockAsync(_path).ConfigureAwait(false);
            _vault = new();
            ReplaceKey(null);
        }

        /// <summary>
        /// Reads a vault file of any format with the master password. <paramref name="key"/> is null for files in
        /// format 1 or the legacy format, which have no vault key yet.
        /// </summary>
        internal static PasswordVault ReadVault(CryptoService cryptoService, Stream stream, string password, out VaultKey key)
        {
            var json = cryptoService.DecryptFromStream<JsonElement>(stream, password, out key);
            try
            {
                return ParseVault(json);
            }
            catch
            {
                key?.Dispose();
                key = null;
                throw;
            }
        }

        /// <summary>
        /// Reads a format 2 file encrypted with the vault key <paramref name="key"/>. Returns false when the file uses
        /// another key or an older format. <paramref name="fileKey"/> is the key together with the header of the file.
        /// </summary>
        internal static bool TryReadVault(CryptoService cryptoService, Stream stream, ReadOnlySpan<byte> key,
            out PasswordVault vault, out VaultKey fileKey)
        {
            vault = null;
            if (!cryptoService.TryDecryptWithKey<JsonElement>(stream, key, out var json, out fileKey))
                return false;

            try
            {
                vault = ParseVault(json);
                return true;
            }
            catch
            {
                fileKey.Dispose();
                fileKey = null;
                throw;
            }
        }

        private static PasswordVault ParseVault(JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Array)
            {
                var array = JsonNode.Parse(json.GetRawText());
                MoveLoginFields(array);
                var credentials = array.Deserialize<List<Credential>>();
                credentials.ForEach(credential => credential.EnsureTypeData());
                return new PasswordVault { Credentials = credentials };
            }

            if (json.ValueKind != JsonValueKind.Object ||
                !json.TryGetProperty(nameof(PasswordVault.Version), out var version) ||
                !version.TryGetInt32(out var number) || number < 1)
                throw new JsonException("Unsupported password vault format.");

            if (number > PasswordVault.CurrentVersion)
                throw new UnsupportedVaultVersionException($"The vault has version {number}, which this version does not know.");

            // Not limited to versions before 3: early version 3 builds wrote the login fields at the top level too.
            var node = JsonNode.Parse(json.GetRawText());
            MoveLoginFields(node[nameof(PasswordVault.Credentials)]);
            var vault = node.Deserialize<PasswordVault>();

            if (vault.Credentials == null || vault.Groups == null || vault.Assignments == null ||
                vault.DeletedCredentials == null || vault.Trash == null)
                throw new JsonException("Incomplete password vault.");

            foreach (var credential in vault.Credentials)
                credential.EnsureTypeData();

            // Older versions are upgraded in memory; the next write stores the vault as the current version.
            return vault.Version == PasswordVault.CurrentVersion ? vault : vault with { Version = PasswordVault.CurrentVersion };
        }

        // Before version 3 the login fields were properties of the credential itself.
        private static readonly string[] LegacyLoginProperties =
            [nameof(LoginData.LoginField), nameof(LoginData.PasswordField), nameof(LoginData.SiteField),
             nameof(LoginData.Totp), nameof(LoginData.PasswordHistory)];

        private static void MoveLoginFields(JsonNode credentials)
        {
            if (credentials is not JsonArray array)
                return;

            foreach (var item in array)
            {
                if (item is not JsonObject credential || credential.ContainsKey(nameof(Credential.Login)) ||
                    !LegacyLoginProperties.Any(credential.ContainsKey))
                    continue;

                var login = new JsonObject();
                foreach (var name in LegacyLoginProperties)
                {
                    if (!credential.TryGetPropertyValue(name, out var value))
                        continue;

                    credential.Remove(name);
                    login[name] = value;
                }
                credential[nameof(Credential.Login)] = login;
            }
        }

        /// <summary>
        /// Opens the vault with the master password, or creates it when <paramref name="allowCreate"/> and there is no
        /// vault file yet. A file in format 1 or the legacy format gets a vault key and is rewritten in format 2;
        /// the old file is kept as an <see cref="VaultBackupReason.Upgrade"/> backup.
        /// </summary>
        internal Task<CredentialLoadResult> LoadCredentialsAsync(string password, bool allowCreate = false) => Task.Run(async () =>
        {
            try
            {
                using var locker = await _asyncKeyedLocker.LockAsync(_path).ConfigureAwait(false);
                PasswordVault vault;
                VaultKey key = null;
                var fileExists = true;
                try
                {
                    using var stream = File.OpenRead(_path);
                    vault = ReadVault(_cryptoService, stream, password, out key);
                }
                catch (FileNotFoundException) when (allowCreate)
                {
                    vault = new PasswordVault();
                    fileExists = false;
                }

                // A new vault, or a file of SinglePass 1.x: give it a vault key.
                var newKey = key is null;
                if (newKey)
                {
                    if (fileExists)
                        CreateUpgradeBackup();
                    key = CreateVerifiedKey(password);
                }

                try
                {
                    CompleteLoad(vault, key, forceWrite: newKey, conversion: newKey && fileExists);
                }
                catch
                {
                    key.Dispose();
                    throw;
                }

                if (newKey)
                {
                    lock (_keyLock)
                        _firstSyncPassword = password;
                }

                NotifyPasswordEntered(key);
                return CredentialLoadResult.Loaded;
            }
            catch (UnsupportedVaultVersionException ex)
            {
                _logger.LogError(ex, "The password vault was written by a newer version.");
                return CredentialLoadResult.NewerVersion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load the password vault.");
                return CredentialLoadResult.Failed;
            }
        });

        /// <summary>
        /// Opens the vault with its vault key, which Windows Hello unlock keeps. Fails when the vault file uses
        /// another key.
        /// </summary>
        internal Task<CredentialLoadResult> LoadCredentialsAsync(byte[] vaultKey) => Task.Run(async () =>
        {
            try
            {
                using var locker = await _asyncKeyedLocker.LockAsync(_path).ConfigureAwait(false);
                PasswordVault vault;
                VaultKey key;
                using (var stream = File.OpenRead(_path))
                {
                    if (!TryReadVault(_cryptoService, stream, vaultKey, out vault, out key))
                        return CredentialLoadResult.Failed;
                }

                try
                {
                    CompleteLoad(vault, key, forceWrite: false, conversion: false);
                }
                catch
                {
                    key.Dispose();
                    throw;
                }
                return CredentialLoadResult.Loaded;
            }
            catch (UnsupportedVaultVersionException ex)
            {
                _logger.LogError(ex, "The password vault was written by a newer version.");
                return CredentialLoadResult.NewerVersion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load the password vault with its vault key.");
                return CredentialLoadResult.Failed;
            }
        });

        /// <param name="conversion">
        /// The file is in format 1 or the legacy format. If it cannot be rewritten (read-only, locked by another program,
        /// disk full), the vault opens anyway, as it did before conversion existed: the old file stays as it is and
        /// still opens with the password, and the next successful write converts it.
        /// </param>
        private void CompleteLoad(PasswordVault vault, VaultKey key, bool forceWrite, bool conversion)
        {
            var purged = PurgeExpiredTrash(vault, DateTimeOffset.UtcNow);
            if (!ReferenceEquals(purged, vault))
            {
                CreateDailyBackupIfDue();
                vault = purged;
                forceWrite = true;
            }

            if (forceWrite)
            {
                try
                {
                    WriteVault(vault, key);
                }
                catch (Exception ex) when (conversion && ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Could not convert the password vault file; it opens in its old format for now.");
                    _fileNeedsRewrite = true;
                }
            }

            _vault = vault;
            ReplaceKey(key);
        }

        /// <summary>
        /// A new vault key for <paramref name="password"/>, its slot checked by opening it with the password.
        /// Files are written with this exact header (<see cref="WriteVault"/> compares it byte by byte), so every file
        /// with this key opens with the password. Runs Argon2 twice.
        /// </summary>
        private VaultKey CreateVerifiedKey(string password)
        {
            var key = _cryptoService.CreateKey(password);
            if (_cryptoService.OpensWithPassword(key, password))
                return key;

            key.Dispose();
            throw new System.Security.Cryptography.CryptographicException("The new vault key does not open with its password.");
        }

        public Task AddCredential(Credential credential) => AddCredential(credential, null);

        internal Task AddCredential(Credential credential, Guid? groupId)
        {
            ArgumentNullException.ThrowIfNull(credential);
            credential.EnsureTypeData();
            return UpdateVault(vault =>
            {
                if (vault.Credentials.Contains(credential))
                    return vault;

                var updated = vault with { Credentials = [.. vault.Credentials, credential] };
                if (vault.DeletedCredentials.ContainsKey(credential.Id))
                {
                    var deleted = new Dictionary<Guid, DateTimeOffset>(vault.DeletedCredentials);
                    deleted.Remove(credential.Id);
                    updated = updated with { DeletedCredentials = deleted };
                }
                return groupId.HasValue ? AssignGroupOrThrow(updated, credential.Id, groupId) : updated;
            });
        }

        public Task EditCredential(Credential credential) => EditCredential(credential, null, changeGroup: false);

        internal async Task<Credential> EditCredential(Credential credential, Guid? groupId, bool changeGroup)
        {
            ArgumentNullException.ThrowIfNull(credential);
            Credential saved = null;
            await UpdateVault(vault =>
            {
                var index = vault.Credentials.IndexOf(credential);
                if (index < 0)
                    throw new InvalidOperationException("The credential is no longer available.");

                var current = vault.Credentials[index];
                if (current.Type != credential.Type)
                    throw new InvalidOperationException("The type of an entry cannot be changed.");

                saved = credential.Clone();
                saved.EnsureTypeData();
                if (saved.Login is { } login)
                {
                    var previousPassword = current.Login.PasswordField.Value;
                    var history = current.Login.PasswordHistory;
                    if (!string.IsNullOrEmpty(previousPassword) &&
                        !string.Equals(previousPassword, login.PasswordField.Value, StringComparison.Ordinal))
                    {
                        var entry = new PasswordHistoryEntry { Password = previousPassword, ChangedAtUtc = DateTimeOffset.UtcNow };
                        history = [entry, .. history.Take(LoginData.PasswordHistoryLimit - 1)];
                    }
                    login.PasswordHistory = history;
                }
                var credentials = vault.Credentials.ToList();
                credentials[index] = saved;
                var updated = vault with { Credentials = credentials };
                return changeGroup ? AssignGroupOrThrow(updated, credential.Id, groupId) : updated;
            }).ConfigureAwait(false);
            return saved;
        }

        /// <summary>Deletes the credential for good, bypassing the trash.</summary>
        public Task DeleteCredential(Credential credential)
        {
            ArgumentNullException.ThrowIfNull(credential);
            return DeleteCredentials([credential.Id]);
        }

        /// <summary>Deletes credentials for good, whether they are in the trash or not.</summary>
        internal Task DeleteCredentials(IReadOnlyCollection<Guid> credentialIds) =>
            UpdateVault(vault => RemoveCredentials(vault, credentialIds, DateTimeOffset.UtcNow));

        /// <summary>Moves the credential to the trash. It keeps its data and group and can be restored.</summary>
        public Task MoveToTrash(Credential credential)
        {
            ArgumentNullException.ThrowIfNull(credential);
            return UpdateVault(vault => MoveToTrash(vault, credential.Id, DateTimeOffset.UtcNow));
        }

        internal static PasswordVault MoveToTrash(PasswordVault vault, Guid credentialId, DateTimeOffset now) =>
            vault.Credentials.Any(credential => credential.Id == credentialId) && !IsTrashed(vault, credentialId)
                ? SetTrashState(vault, [credentialId], new TrashState(now, now))
                : vault;

        /// <summary>Brings credentials back from the trash.</summary>
        internal Task RestoreFromTrash(IReadOnlyCollection<Guid> credentialIds) =>
            UpdateVault(vault => RestoreFromTrash(vault, credentialIds, DateTimeOffset.UtcNow));

        internal static PasswordVault RestoreFromTrash(PasswordVault vault, IEnumerable<Guid> credentialIds, DateTimeOffset now)
        {
            var trashed = credentialIds.Where(id => IsTrashed(vault, id)).ToList();
            return trashed.Count == 0 ? vault : SetTrashState(vault, trashed, new TrashState(null, now));
        }

        /// <summary>Deletes for good the credentials that have been in the trash longer than <see cref="PasswordVault.TrashRetention"/>.</summary>
        internal Task PurgeExpiredTrash() => UpdateVault(vault => PurgeExpiredTrash(vault, DateTimeOffset.UtcNow));

        internal static PasswordVault PurgeExpiredTrash(PasswordVault vault, DateTimeOffset now)
        {
            var expired = vault.Trash
                .Where(pair => pair.Value.DeletedAt is { } deletedAt && deletedAt + PasswordVault.TrashRetention <= now)
                .Select(pair => pair.Key)
                .ToList();
            return expired.Count == 0 ? vault : RemoveCredentials(vault, expired, now);
        }

        private static PasswordVault SetTrashState(PasswordVault vault, IEnumerable<Guid> credentialIds, TrashState state)
        {
            var trash = new Dictionary<Guid, TrashState>(vault.Trash);
            foreach (var id in credentialIds)
                trash[id] = state;
            return vault with { Trash = trash };
        }

        internal static PasswordVault RemoveCredentials(PasswordVault vault, IReadOnlyCollection<Guid> credentialIds, DateTimeOffset now)
        {
            var existing = vault.Credentials.Select(credential => credential.Id).ToHashSet();
            var ids = credentialIds.Where(existing.Contains).ToHashSet();
            if (ids.Count == 0)
                return vault;

            // The tombstone keeps other devices and the cloud copy from bringing the credential back on sync.
            var deleted = new Dictionary<Guid, DateTimeOffset>(vault.DeletedCredentials);
            var assignments = new Dictionary<Guid, GroupAssignment>(vault.Assignments);
            var trash = new Dictionary<Guid, TrashState>(vault.Trash);
            foreach (var id in ids)
            {
                deleted[id] = now;
                assignments.Remove(id);
                trash.Remove(id);
            }

            return vault with
            {
                Credentials = vault.Credentials.Where(item => !ids.Contains(item.Id)).ToList(),
                DeletedCredentials = deleted,
                Assignments = assignments,
                Trash = trash
            };
        }

        internal Task SaveGroup(PasswordGroup group, bool isNew) => UpdateVault(vault =>
        {
            if (!isNew && (!vault.Groups.TryGetValue(group.Id, out var current) || current.IsDeleted))
                return vault;

            var groups = new Dictionary<Guid, GroupChange>(vault.Groups)
            {
                [group.Id] = new(group, DateTimeOffset.UtcNow)
            };
            return vault with { Groups = groups };
        });

        internal Task DeleteGroup(Guid groupId) => UpdateVault(vault =>
        {
            if (!vault.Groups.TryGetValue(groupId, out var group) || group.IsDeleted)
                return vault;

            // The tombstone invalidates links to this group without overwriting a concurrent move elsewhere.
            var groups = new Dictionary<Guid, GroupChange>(vault.Groups)
            {
                [groupId] = new(null, DateTimeOffset.UtcNow, IsDeleted: true)
            };
            return vault with { Groups = groups };
        });

        internal Task MoveCredentials(IReadOnlyCollection<Guid> credentialIds, Guid? groupId) => UpdateVault(vault =>
        {
            var existingIds = vault.Credentials.Select(credential => credential.Id).ToHashSet();
            return AssignGroups(vault, credentialIds.Where(existingIds.Contains), groupId);
        });

        private static PasswordVault AssignGroupOrThrow(PasswordVault vault, Guid credentialId, Guid? groupId)
        {
            if (!IsGroupAvailable(vault, groupId))
                throw new InvalidOperationException("The selected password group is no longer available.");

            return AssignGroups(vault, [credentialId], groupId);
        }

        private static bool IsGroupAvailable(PasswordVault vault, Guid? groupId) =>
            groupId is not { } id || (vault.Groups.TryGetValue(id, out var group) && !group.IsDeleted);

        private static PasswordVault AssignGroups(PasswordVault vault, IEnumerable<Guid> credentialIds, Guid? groupId)
        {
            if (!IsGroupAvailable(vault, groupId))
                return vault;

            var assignments = new Dictionary<Guid, GroupAssignment>(vault.Assignments);
            var change = new GroupAssignment(groupId, DateTimeOffset.UtcNow);
            var changed = false;
            foreach (var credentialId in credentialIds)
            {
                if (assignments.GetValueOrDefault(credentialId)?.GroupId != groupId)
                {
                    assignments[credentialId] = change;
                    changed = true;
                }
            }

            return changed ? vault with { Assignments = assignments } : vault;
        }

        /// <summary>
        /// Merges a vault downloaded from the cloud. <paramref name="cloudKey"/> is the vault key of the cloud file, or null
        /// for a file in format 1 or the legacy format. <paramref name="cloudPassword"/> is the password the user entered
        /// to open it, or null when it opened with this device's key.
        /// <para>
        /// When the cloud file uses another vault key, the side whose master password changed later wins. On a tie the key
        /// with the lower id wins. The rule is the same on every device and for every cloud, so devices that got their keys
        /// independently (each converted its own 1.x file, or two clouds got different keys) all settle on one key instead
        /// of switching back and forth. The losing side is re-encrypted with the winning key: this device's file here, the
        /// cloud copy on upload. A 1.x cloud file switches this device to its password only when that password was changed
        /// later, as before; otherwise the cloud copy is converted with this device's key on upload.
        /// </para>
        /// Adds the changes to <paramref name="result"/>, which may already hold changes of a previous attempt.
        /// </summary>
        internal Task MergeFromCloud(PasswordVault incoming, VaultKey cloudKey, string cloudPassword, CredentialsMergeResult result) => Task.Run(async () =>
        {
            using var locker = await _asyncKeyedLocker.LockAsync(_path).ConfigureAwait(false);
            if (_locked)
                throw new InvalidOperationException("The password vault is locked.");

            var localKey = CurrentKey();
            var sameKey = cloudKey is not null && localKey.IsSameKey(cloudKey.Id);
            var cloudChangedAt = incoming.MasterPasswordChangedAt ?? DateTimeOffset.MinValue;
            var localChangedAt = _vault.MasterPasswordChangedAt ?? DateTimeOffset.MinValue;

            // Whether the cloud password differs from the local one; known only when the user entered it. Runs Argon2,
            // so it is evaluated before anything is written.
            var otherPassword = !sameKey && cloudPassword is not null && !_cryptoService.OpensWithPassword(localKey, cloudPassword);

            VaultKey adoptedKey = null;
            var adopted = false;
            try
            {
                if (!sameKey && cloudPassword is not null)
                {
                    if (cloudKey is not null)
                    {
                        var cloudWins = cloudChangedAt > localChangedAt ||
                            cloudChangedAt == localChangedAt && cloudKey.Id.AsSpan().SequenceCompareTo(localKey.Id) < 0;
                        if (cloudWins)
                            adoptedKey = cloudKey.Clone();
                    }
                    else if (cloudChangedAt > localChangedAt && otherPassword)
                    {
                        adoptedKey = CreateVerifiedKey(cloudPassword);
                    }
                }

                var merged = MergeVaults(_vault, incoming, result);
                if (adoptedKey is not null || !ReferenceEquals(merged, _vault) || _fileNeedsRewrite)
                {
                    // Sync writes changes that came from elsewhere: keep the vault as it was before them.
                    var reason = adoptedKey is not null && otherPassword ? VaultBackupReason.Password : VaultBackupReason.Sync;
                    _backups.Create(reason, DateTimeOffset.UtcNow);
                }

                if (adoptedKey is not null)
                {
                    // The cloud slot opened with cloudPassword, or the new key was checked with it: the file opens with it.
                    WriteVault(merged, adoptedKey);
                    result.MasterPasswordAdopted = otherPassword;
                    _vault = merged;
                    ReplaceKey(adoptedKey);
                    if (otherPassword)
                        ForgetFirstSyncPassword(); // it is the old password now
                    NotifyPasswordEntered(adoptedKey);
                    adoptedKey = null;
                    adopted = true;
                }
                else if (!ReferenceEquals(merged, _vault) || _fileNeedsRewrite)
                {
                    WriteVault(merged, localKey);
                    _vault = merged;
                }

                // Upload sends the local file, so the cloud copy gets this device's key (and password) whenever they differ.
                // A 1.x cloud file is always rewritten in format 2.
                var cloudHasOtherKey = cloudKey is null || !CurrentKey().IsSameKey(cloudKey.Id);
                result.CloudReencrypted = !adopted && otherPassword;
                result.CloudNeedsUpdate = cloudHasOtherKey ||
                    !ReferenceEquals(MergeVaults(incoming, merged, new CredentialsMergeResult()), incoming);
            }
            finally
            {
                adoptedKey?.Dispose();
            }
        });

        /// <summary>
        /// Merges <paramref name="incoming"/> into <paramref name="local"/>. Returns <paramref name="local"/>
        /// itself when nothing has to be written.
        /// </summary>
        internal static PasswordVault MergeVaults(PasswordVault local, PasswordVault incoming, CredentialsMergeResult result)
        {
            var changed = false;

            // Tombstones: the latest deletion time wins.
            var deleted = new Dictionary<Guid, DateTimeOffset>(local.DeletedCredentials);
            foreach (var (id, deletedAt) in incoming.DeletedCredentials)
            {
                if (!deleted.TryGetValue(id, out var known) || deletedAt > known)
                {
                    deleted[id] = deletedAt;
                    changed = true;
                }
            }

            // Credentials: the latest modification wins, password histories are combined.
            var credentials = local.Credentials.ToList();
            foreach (var credential in incoming.Credentials)
            {
                var index = credentials.IndexOf(credential);
                if (index < 0)
                {
                    credentials.Add(credential);
                    result.NewCredentials.Add(credential);
                    continue;
                }

                var current = credentials[index];
                var winner = current.LastModifiedTime < credential.LastModifiedTime ? credential : current;
                var history = MergePasswordHistory(winner, ReferenceEquals(winner, current) ? credential : current);
                if (!ReferenceEquals(winner, current) || !ReferenceEquals(history, current.Login?.PasswordHistory))
                {
                    var merged = winner.Clone();
                    if (merged.Login != null)
                        merged.Login.PasswordHistory = history;
                    result.ChangedCredentials.Add(merged);
                    credentials[index] = merged;
                }
            }

            // A deletion removes the credential unless it was edited after being deleted somewhere else:
            // losing a freshly changed password is worse than getting a deleted entry back.
            for (var i = credentials.Count - 1; i >= 0; i--)
            {
                var credential = credentials[i];
                if (!deleted.TryGetValue(credential.Id, out var deletedAt))
                    continue;

                if (IsModifiedAfter(credential, deletedAt))
                {
                    deleted.Remove(credential.Id);
                    changed = true;
                    continue;
                }

                credentials.RemoveAt(i);
                result.NewCredentials.Remove(credential);
                result.ChangedCredentials.Remove(credential);
                var index = local.Credentials.IndexOf(credential);
                if (index >= 0)
                    result.DeletedCredentials.Add(local.Credentials[index]);
            }

            var groups = new Dictionary<Guid, GroupChange>(local.Groups);
            foreach (var (id, change) in incoming.Groups)
            {
                if (!groups.TryGetValue(id, out var current) ||
                    !current.IsDeleted && (change.IsDeleted || change.ModifiedAt > current.ModifiedAt))
                {
                    groups[id] = change;
                    result.GroupChanges = true;
                }
            }

            // Group links are kept only for credentials that survived the merge.
            var credentialIds = credentials.Select(credential => credential.Id).ToHashSet();
            var assignments = new Dictionary<Guid, GroupAssignment>(local.Assignments);
            foreach (var id in local.Assignments.Keys)
            {
                if (!credentialIds.Contains(id))
                {
                    assignments.Remove(id);
                    changed = true;
                }
            }

            foreach (var (id, assignment) in incoming.Assignments)
            {
                if (!credentialIds.Contains(id))
                    continue;

                if (!assignments.TryGetValue(id, out var current) || assignment.ModifiedAt > current.ModifiedAt)
                {
                    assignments[id] = assignment;
                    result.GroupChanges = true;
                }
            }

            // Trash state: the later of moving to the trash and restoring wins, for credentials that survived.
            var localIds = local.Credentials.Select(credential => credential.Id).ToHashSet();
            var trash = new Dictionary<Guid, TrashState>(local.Trash);
            foreach (var id in local.Trash.Keys)
            {
                if (!credentialIds.Contains(id))
                {
                    trash.Remove(id);
                    changed = true;
                }
            }

            foreach (var (id, state) in incoming.Trash)
            {
                if (!credentialIds.Contains(id))
                    continue;

                if (trash.TryGetValue(id, out var current) && state.ModifiedAt <= current.ModifiedAt)
                    continue;

                trash[id] = state;
                if (localIds.Contains(id) && (current?.IsTrashed ?? false) != state.IsTrashed)
                    result.TrashChanges = true;
                else
                    changed = true;
            }

            // Entries in the trash are not reported as new or changed: they are not shown in the vault.
            foreach (var list in new[] { result.NewCredentials, result.ChangedCredentials })
            {
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    if (trash.TryGetValue(list[i].Id, out var state) && state.IsTrashed)
                    {
                        list.RemoveAt(i);
                        changed = true;
                    }
                }
            }

            var passwordChangedAt = local.MasterPasswordChangedAt;
            if (incoming.MasterPasswordChangedAt > (passwordChangedAt ?? DateTimeOffset.MinValue))
            {
                passwordChangedAt = incoming.MasterPasswordChangedAt;
                changed = true;
            }

            if (!changed && !result.AnyChanges)
                return local;

            return local with
            {
                Credentials = credentials,
                Groups = groups,
                Assignments = assignments,
                DeletedCredentials = deleted,
                Trash = trash,
                MasterPasswordChangedAt = passwordChangedAt
            };
        }

        private static bool IsModifiedAfter(Credential credential, DateTimeOffset deletedAt) =>
            credential.LastModifiedTime.ToUniversalTime() > deletedAt.UtcDateTime;

        /// <summary>History of the winning version combined with the other one. Null for entries without a password.</summary>
        private static IReadOnlyList<PasswordHistoryEntry> MergePasswordHistory(Credential winner, Credential other)
        {
            if (winner.Login is not { } login)
                return null;

            if (other.Login is not { PasswordHistory.Count: > 0 } otherLogin)
                return login.PasswordHistory;

            var history = login.PasswordHistory.Concat(otherLogin.PasswordHistory)
                .DistinctBy(entry => (entry.ChangedAtUtc, entry.Password))
                .OrderByDescending(entry => entry.ChangedAtUtc)
                .ThenBy(entry => entry.Password, StringComparer.Ordinal)
                .Take(LoginData.PasswordHistoryLimit)
                .ToArray();
            return history.SequenceEqual(login.PasswordHistory) ? login.PasswordHistory : history;
        }

        private Task UpdateVault(Func<PasswordVault, PasswordVault> update) => Task.Run(async () =>
        {
            using var locker = await _asyncKeyedLocker.LockAsync(_path).ConfigureAwait(false);
            if (_locked)
                throw new InvalidOperationException("The password vault is locked.");

            var updated = update(_vault);
            if (ReferenceEquals(updated, _vault))
                return;

            CreateDailyBackupIfDue();
            WriteVault(updated, CurrentKey());
            _vault = updated;
        });

        /// <summary>
        /// Reads a backup file with the master password it was saved with. Returns null when <paramref name="password"/>
        /// does not open it; other failures (missing file, no access, a newer version) throw.
        /// </summary>
        internal Task<PasswordVault> ReadBackup(string path, string password) => Task.Run(() =>
        {
            using var stream = File.OpenRead(path);
            try
            {
                var vault = ReadVault(_cryptoService, stream, password, out var key);
                key?.Dispose();
                return vault;
            }
            catch (Exception ex) when (ex is not UnsupportedVaultVersionException &&
                ex is System.Security.Cryptography.CryptographicException or JsonException
                    or InvalidDataException or EndOfStreamException or ArgumentException)
            {
                return null;
            }
        });

        /// <summary>
        /// Reads a backup file that uses the current vault key, without a password. Returns null for a backup with
        /// another key (made before a password change) or in an older format.
        /// </summary>
        internal Task<PasswordVault> ReadBackup(string path) => Task.Run(() =>
        {
            using var stream = File.OpenRead(path);
            return TryReadWithCurrentKey(stream, out var vault) ? vault : null;
        });

        /// <summary>
        /// Reads a vault file downloaded from the cloud if it uses the current vault key. Returns false for another key
        /// or an older format, which need a password.
        /// </summary>
        internal bool TryReadWithCurrentKey(byte[] file, out PasswordVault vault, out VaultKey fileKey)
        {
            using var stream = new MemoryStream(file, writable: false);
            return TryReadWithCurrentKey(stream, out vault, out fileKey);
        }

        private bool TryReadWithCurrentKey(Stream stream, out PasswordVault vault)
        {
            if (!TryReadWithCurrentKey(stream, out vault, out var fileKey))
                return false;
            fileKey.Dispose();
            return true;
        }

        private bool TryReadWithCurrentKey(Stream stream, out PasswordVault vault, out VaultKey fileKey)
        {
            var key = CopyVaultKey() ?? throw new InvalidOperationException("The password vault is locked.");
            try
            {
                return TryReadVault(_cryptoService, stream, key, out vault, out fileKey);
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
            }
        }

        /// <summary>The vault file as it is on disk, for upload.</summary>
        internal async Task<byte[]> ReadVaultFileAsync()
        {
            using var locker = await _asyncKeyedLocker.LockAsync(_path).ConfigureAwait(false);
            return await File.ReadAllBytesAsync(_path).ConfigureAwait(false);
        }

        /// <summary>
        /// Brings back credentials and earlier versions from <paramref name="backup"/> as a new change of this
        /// vault (see <see cref="ApplyBackup"/>). The current vault is backed up first.
        /// </summary>
        internal async Task<BackupRestoreResult> RestoreBackup(PasswordVault backup)
        {
            ArgumentNullException.ThrowIfNull(backup);
            var result = BackupRestoreResult.Empty;
            await Task.Run(async () =>
            {
                using var locker = await _asyncKeyedLocker.LockAsync(_path).ConfigureAwait(false);
                if (_locked)
                    throw new InvalidOperationException("The password vault is locked.");

                var now = DateTimeOffset.UtcNow;
                var updated = ApplyBackup(_vault, backup, now, out result);
                if (ReferenceEquals(updated, _vault))
                    return;

                _backups.Create(VaultBackupReason.Restore, now);
                WriteVault(updated, CurrentKey());
                _vault = updated;
            }).ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// Applies a backup on top of <paramref name="current"/> without losing anything:
        /// credentials missing now are added back; credentials that differ are reverted to the backup
        /// version (the current password goes to the password history); credentials created after the
        /// backup stay; groups and group links of the backup credentials are reverted too.
        /// Every restored item gets the time <paramref name="now"/>, so on sync it wins over the changes and
        /// deletions it undoes, in the cloud copy and on other devices as well. A group deleted since the
        /// backup comes back under a new id, because a group tombstone always wins.
        /// </summary>
        internal static PasswordVault ApplyBackup(PasswordVault current, PasswordVault backup, DateTimeOffset now,
            out BackupRestoreResult result)
        {
            var restored = new List<Credential>();
            var reverted = new List<Credential>();
            var credentials = current.Credentials.ToList();
            var deleted = new Dictionary<Guid, DateTimeOffset>(current.DeletedCredentials);
            var trash = new Dictionary<Guid, TrashState>(current.Trash);
            // Entries that were in the trash at the time of the backup were not part of the vault then.
            var backupCredentials = backup.Credentials.Where(credential => !IsTrashed(backup, credential.Id)).ToList();
            foreach (var saved in backupCredentials)
            {
                var index = credentials.IndexOf(saved);
                var inTrash = IsTrashed(current, saved.Id);
                if (inTrash)
                    trash[saved.Id] = new TrashState(null, now);

                if (index >= 0 && HasSameContent(credentials[index], saved))
                {
                    if (inTrash)
                        restored.Add(credentials[index]);
                    continue;
                }

                var copy = saved.Clone();
                copy.LastModifiedTime = now.UtcDateTime;
                if (index >= 0)
                {
                    if (copy.Login != null && credentials[index].Login is { } currentLogin)
                        copy.Login.PasswordHistory = CombineHistory(copy.Login, currentLogin, now);
                    credentials[index] = copy;
                    (inTrash ? restored : reverted).Add(copy);
                }
                else
                {
                    credentials.Add(copy);
                    restored.Add(copy);
                }

                deleted.Remove(saved.Id);
            }

            var groupsChanged = false;
            var groups = new Dictionary<Guid, GroupChange>(current.Groups);
            var groupIds = new Dictionary<Guid, Guid>(); // backup group id to its id in the result
            foreach (var (id, change) in backup.Groups)
            {
                if (change.IsDeleted)
                    continue;

                if (groups.TryGetValue(id, out var existing) && !existing.IsDeleted)
                {
                    groupIds[id] = id;
                    if (existing.Group != change.Group)
                    {
                        groups[id] = new GroupChange(change.Group, now);
                        groupsChanged = true;
                    }
                    continue;
                }

                var targetId = existing is null ? id : Guid.NewGuid();
                groups[targetId] = new GroupChange(change.Group with { Id = targetId }, now);
                groupIds[id] = targetId;
                groupsChanged = true;
            }

            var assignments = new Dictionary<Guid, GroupAssignment>(current.Assignments);
            foreach (var saved in backupCredentials)
            {
                Guid? target = backup.Assignments.TryGetValue(saved.Id, out var assignment) &&
                    assignment.GroupId is { } savedGroupId && groupIds.TryGetValue(savedGroupId, out var mapped)
                    ? mapped
                    : null;
                Guid? currentGroup = assignments.TryGetValue(saved.Id, out var currentAssignment) &&
                    currentAssignment.GroupId is { } groupId && groups.TryGetValue(groupId, out var group) && !group.IsDeleted
                    ? groupId
                    : null;
                if (target != currentGroup)
                {
                    assignments[saved.Id] = new GroupAssignment(target, now);
                    groupsChanged = true;
                }
            }

            result = new BackupRestoreResult(restored, reverted, groupsChanged);
            if (!result.AnyChanges)
                return current;

            return current with
            {
                Credentials = credentials,
                Groups = groups,
                Assignments = assignments,
                DeletedCredentials = deleted,
                Trash = trash
            };
        }

        /// <summary>Same entry apart from modification time and password history.</summary>
        private static bool HasSameContent(Credential first, Credential second)
        {
            static string Content(Credential credential)
            {
                var copy = credential.Clone();
                copy.LastModifiedTime = default;
                if (copy.Login != null)
                    copy.Login.PasswordHistory = [];
                return JsonSerializer.Serialize(copy);
            }

            return Content(first) == Content(second);
        }

        private static IReadOnlyList<PasswordHistoryEntry> CombineHistory(LoginData restored, LoginData current, DateTimeOffset now)
        {
            IEnumerable<PasswordHistoryEntry> entries = restored.PasswordHistory.Concat(current.PasswordHistory);
            var currentPassword = current.PasswordField.Value;
            if (!string.IsNullOrEmpty(currentPassword) &&
                !string.Equals(currentPassword, restored.PasswordField.Value, StringComparison.Ordinal))
                entries = entries.Append(new PasswordHistoryEntry { Password = currentPassword, ChangedAtUtc = now });

            return entries
                .DistinctBy(entry => (entry.ChangedAtUtc, entry.Password))
                .OrderByDescending(entry => entry.ChangedAtUtc)
                .ThenBy(entry => entry.Password, StringComparer.Ordinal)
                .Take(LoginData.PasswordHistoryLimit)
                .ToArray();
        }

        private void CreateDailyBackupIfDue()
        {
            try
            {
                _backups.CreateDailyIfDue(DateTimeOffset.UtcNow);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A missed daily backup must not block saving the user's change.
                _logger.LogError(ex, "Failed to back up the password vault.");
            }
        }

        /// <summary>
        /// Keeps the 1.x file before it is converted. Once per file: when the newest <c>upgrade</c> backup already holds
        /// the same bytes (an earlier conversion attempt failed), no copy is made, so retries do not push out older backups.
        /// </summary>
        private void CreateUpgradeBackup()
        {
            try
            {
                var latest = _backups.List().FirstOrDefault(backup => backup.Reason == VaultBackupReason.Upgrade);
                if (latest is not null && File.ReadAllBytes(latest.Path).AsSpan().SequenceEqual(File.ReadAllBytes(_path)))
                    return;

                _backups.Create(VaultBackupReason.Upgrade, DateTimeOffset.UtcNow);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The vault is written only after reading it back, so a missed copy must not block unlocking.
                _logger.LogError(ex, "Failed to back up the password vault.");
            }
        }

        /// <summary>
        /// Writes the vault with <paramref name="key"/> and replaces the file only after reading the written file back:
        /// it must start with exactly this key's header and its contents must decrypt with this key. The header holds
        /// the password slot, which was checked against the password when the key was made (<see cref="CreateVerifiedKey"/>)
        /// or opened with it (a key from another file), so the written file opens with the password too.
        /// </summary>
        private void WriteVault(PasswordVault vault, VaultKey key)
        {
            var tempPath = $"{_path}.{Guid.NewGuid():N}.tmp";
            var tempFileCreated = false;
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    tempFileCreated = true;
                    _cryptoService.EncryptToStream(vault, stream, key);
                    stream.Flush(flushToDisk: true);

                    if (!_cryptoService.IsWrittenWith(stream, key))
                        throw new InvalidDataException("The written vault file does not match its vault key.");
                }

                if (File.Exists(_path))
                    File.Replace(tempPath, _path, destinationBackupFileName: null, ignoreMetadataErrors: false);
                else
                    File.Move(tempPath, _path);
                _fileNeedsRewrite = false;
            }
            catch
            {
                if (tempFileCreated)
                {
                    try { File.Delete(tempPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
                throw;
            }
        }

        /// <summary>
        /// Changes the master password. The vault also gets a new vault key, so the old password together with an old
        /// copy of the file does not open the files written from now on.
        /// </summary>
        internal Task<PasswordChangeResult> ChangePasswordAsync(string currentPassword, string newPassword)
        {
            return Task.Run(async () =>
            {
                try
                {
                    using var locker = await _asyncKeyedLocker.LockAsync(_path).ConfigureAwait(false);
                    if (_locked)
                        return PasswordChangeResult.Failed;

                    if (!_cryptoService.OpensWithPassword(CurrentKey(), currentPassword))
                        return PasswordChangeResult.IncorrectCurrentPassword;

                    _backups.Create(VaultBackupReason.Password, DateTimeOffset.UtcNow);
                    var newKey = CreateVerifiedKey(newPassword);
                    var updated = _vault with { MasterPasswordChangedAt = DateTimeOffset.UtcNow };
                    try
                    {
                        WriteVault(updated, newKey);
                    }
                    catch
                    {
                        newKey.Dispose();
                        throw;
                    }

                    _vault = updated;
                    ReplaceKey(newKey);
                    ForgetFirstSyncPassword(); // it is the old password now
                    NotifyPasswordEntered(newKey);
                    return PasswordChangeResult.Changed;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to change the credentials password.");
                    return PasswordChangeResult.Failed;
                }
            });
        }

    }
}
