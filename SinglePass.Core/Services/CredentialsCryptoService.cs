using AsyncKeyedLock;
using Microsoft.Extensions.Logging;
using SinglePass.Core.Helpers;
using SinglePass.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace SinglePass.Core.Services
{
    internal enum CredentialLoadResult
    {
        Loaded,
        Failed
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
        private readonly object _passwordLock = new();
        private readonly VaultBackups _backups = new(Constants.PasswordsFilePath, Constants.BackupsDirectoryPath);
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

        private SecureString PasswordSecure { get; set; }

        public CredentialsCryptoService(
            ILogger<CredentialsCryptoService> logger,
            CryptoService cryptoService,
            AsyncKeyedLocker<string> asyncKeyedLocker)
        {
            _logger = logger;
            _cryptoService = cryptoService;
            _asyncKeyedLocker = asyncKeyedLocker;
        }

        public void SetPassword(string password)
        {
            ReplacePassword(SecureStringHelper.MakeSecureString(password));
        }

        public string GetPassword()
        {
            lock (_passwordLock)
                return SecureStringHelper.GetString(PasswordSecure);
        }

        private void ReplacePassword(SecureString password)
        {
            lock (_passwordLock)
            {
                PasswordSecure?.Dispose();
                PasswordSecure = password;
            }
        }

        internal async Task LockAsync()
        {
            _locked = true;
            await ClearAsync().ConfigureAwait(false);
        }

        internal async Task ClearAsync()
        {
            using var locker = await _asyncKeyedLocker.LockAsync(Constants.PasswordsFilePath).ConfigureAwait(false);
            _vault = new();
            ReplacePassword(null);
        }

        internal static PasswordVault ReadVault(CryptoService cryptoService, Stream stream, string password, out bool isLegacy)
        {
            var json = cryptoService.DecryptFromStream<JsonElement>(stream, password, out isLegacy);
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
                !version.TryGetInt32(out var number) || number < 1 || number > PasswordVault.CurrentVersion)
                throw new JsonException("Unsupported password vault format.");

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

        internal Task<CredentialLoadResult> LoadCredentialsAsync(string password, bool allowCreate = false) => Task.Run(async () =>
        {
            var path = Constants.PasswordsFilePath;
            try
            {
                using var locker = await _asyncKeyedLocker.LockAsync(path).ConfigureAwait(false);
                PasswordVault vault;
                var needsRewrite = false;
                try
                {
                    using var stream = File.OpenRead(path);
                    vault = ReadVault(_cryptoService, stream, password, out needsRewrite);
                }
                catch (FileNotFoundException) when (allowCreate)
                {
                    vault = new PasswordVault();
                    needsRewrite = true;
                }

                var purged = PurgeExpiredTrash(vault, DateTimeOffset.UtcNow);
                if (!ReferenceEquals(purged, vault))
                {
                    CreateDailyBackupIfDue();
                    vault = purged;
                    needsRewrite = true;
                }

                if (needsRewrite)
                    WriteVault(vault, password);

                _vault = vault;
                return CredentialLoadResult.Loaded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load the password vault.");
                return CredentialLoadResult.Failed;
            }
        });

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
        /// Merges a vault downloaded from the cloud that was decrypted with <paramref name="cloudPassword"/>.
        /// When the cloud vault uses another master password, the more recently changed password is kept:
        /// either this device switches to the cloud password, or the cloud copy has to be re-encrypted.
        /// Adds the changes to <paramref name="result"/>, which may already hold changes of a previous attempt.
        /// </summary>
        internal Task MergeFromCloud(PasswordVault incoming, string cloudPassword, CredentialsMergeResult result) => Task.Run(async () =>
        {
            using var locker = await _asyncKeyedLocker.LockAsync(Constants.PasswordsFilePath).ConfigureAwait(false);
            if (_locked)
                throw new InvalidOperationException("The password vault is locked.");

            var localPassword = GetPassword();
            var samePassword = string.Equals(cloudPassword, localPassword, StringComparison.Ordinal);
            var adoptCloudPassword = !samePassword &&
                (incoming.MasterPasswordChangedAt ?? DateTimeOffset.MinValue) > (_vault.MasterPasswordChangedAt ?? DateTimeOffset.MinValue);

            var merged = MergeVaults(_vault, incoming, result);
            if (adoptCloudPassword || !ReferenceEquals(merged, _vault))
            {
                // Sync writes changes that came from elsewhere: keep the vault as it was before them.
                _backups.Create(adoptCloudPassword ? VaultBackupReason.Password : VaultBackupReason.Sync, DateTimeOffset.UtcNow);
            }

            if (adoptCloudPassword)
            {
                var cloudPasswordSecure = SecureStringHelper.MakeSecureString(cloudPassword);
                try
                {
                    WriteVault(merged, cloudPassword);
                }
                catch
                {
                    cloudPasswordSecure.Dispose();
                    throw;
                }

                _vault = merged;
                ReplacePassword(cloudPasswordSecure);
                result.MasterPasswordAdopted = true;
            }
            else if (!ReferenceEquals(merged, _vault))
            {
                WriteVault(merged, localPassword);
                _vault = merged;
            }

            result.CloudPasswordReplaced = !samePassword && !adoptCloudPassword;
            result.CloudNeedsUpdate = result.CloudPasswordReplaced ||
                !ReferenceEquals(MergeVaults(incoming, merged, new CredentialsMergeResult()), incoming);
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
            using var locker = await _asyncKeyedLocker.LockAsync(Constants.PasswordsFilePath).ConfigureAwait(false);
            if (_locked)
                throw new InvalidOperationException("The password vault is locked.");

            var updated = update(_vault);
            if (ReferenceEquals(updated, _vault))
                return;

            CreateDailyBackupIfDue();
            WriteVault(updated, GetPassword());
            _vault = updated;
        });

        /// <summary>
        /// Reads a backup file. Returns null when <paramref name="password"/> does not open it;
        /// other failures (missing file, no access) throw.
        /// </summary>
        internal Task<PasswordVault> ReadBackup(string path, string password) => Task.Run(() =>
        {
            using var stream = File.OpenRead(path);
            try
            {
                return ReadVault(_cryptoService, stream, password, out _);
            }
            catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException
                or InvalidDataException or EndOfStreamException or ArgumentException)
            {
                return null;
            }
        });

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
                using var locker = await _asyncKeyedLocker.LockAsync(Constants.PasswordsFilePath).ConfigureAwait(false);
                if (_locked)
                    throw new InvalidOperationException("The password vault is locked.");

                var now = DateTimeOffset.UtcNow;
                var updated = ApplyBackup(_vault, backup, now, out result);
                if (ReferenceEquals(updated, _vault))
                    return;

                _backups.Create(VaultBackupReason.Restore, now);
                WriteVault(updated, GetPassword());
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

        private void WriteVault(PasswordVault vault, string password)
        {
            var path = Constants.PasswordsFilePath;
            var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            var tempFileCreated = false;
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    tempFileCreated = true;
                    _cryptoService.EncryptToStream(vault, stream, password);
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(path))
                    File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: false);
                else
                    File.Move(tempPath, path);
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

        internal Task<PasswordChangeResult> ChangePasswordAsync(string currentPassword, string newPassword)
        {
            return Task.Run(async () =>
            {
                try
                {
                    var path = Constants.PasswordsFilePath;
                    using var locker = await _asyncKeyedLocker.LockAsync(path).ConfigureAwait(false);
                    if (_locked)
                        return PasswordChangeResult.Failed;

                    if (!string.Equals(currentPassword, GetPassword(), StringComparison.Ordinal))
                        return PasswordChangeResult.IncorrectCurrentPassword;

                    _backups.Create(VaultBackupReason.Password, DateTimeOffset.UtcNow);
                    var newPasswordSecure = SecureStringHelper.MakeSecureString(newPassword);
                    var updated = _vault with { MasterPasswordChangedAt = DateTimeOffset.UtcNow };
                    try
                    {
                        WriteVault(updated, newPassword);
                    }
                    catch
                    {
                        newPasswordSecure.Dispose();
                        throw;
                    }

                    _vault = updated;
                    ReplacePassword(newPasswordSecure);
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
