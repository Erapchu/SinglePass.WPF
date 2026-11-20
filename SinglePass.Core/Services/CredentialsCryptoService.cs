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

    public class CredentialsCryptoService
    {
        private readonly ILogger<CredentialsCryptoService> _logger;
        private readonly CryptoService _cryptoService;
        private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
        private readonly object _passwordLock = new();
        private volatile PasswordVault _vault = new();
        private volatile bool _locked;
        internal bool IsLocked => _locked;

        public List<Credential> Credentials => _vault.Credentials.ToList();

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
                return new PasswordVault { Credentials = json.Deserialize<List<Credential>>() };

            if (json.ValueKind != JsonValueKind.Object ||
                !json.TryGetProperty(nameof(PasswordVault.Version), out var version) ||
                !version.TryGetInt32(out var number) || number < 1 || number > PasswordVault.CurrentVersion)
                throw new JsonException("Unsupported password vault format.");

            var vault = json.Deserialize<PasswordVault>();
            if (vault.Credentials == null || vault.Groups == null || vault.Assignments == null ||
                vault.DeletedCredentials == null)
                throw new JsonException("Incomplete password vault.");

            // Version 1 has no tombstones; the next write stores the vault as the current version.
            return vault.Version == PasswordVault.CurrentVersion ? vault : vault with { Version = PasswordVault.CurrentVersion };
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
                var previousPassword = current.PasswordField.Value;
                var history = current.PasswordHistory;
                if (!string.IsNullOrEmpty(previousPassword) &&
                    !string.Equals(previousPassword, credential.PasswordField.Value, StringComparison.Ordinal))
                {
                    var entry = new PasswordHistoryEntry { Password = previousPassword, ChangedAtUtc = DateTimeOffset.UtcNow };
                    history = [entry, .. history.Take(Credential.PasswordHistoryLimit - 1)];
                }

                saved = credential.Clone();
                saved.PasswordHistory = history;
                var credentials = vault.Credentials.ToList();
                credentials[index] = saved;
                var updated = vault with { Credentials = credentials };
                return changeGroup ? AssignGroupOrThrow(updated, credential.Id, groupId) : updated;
            }).ConfigureAwait(false);
            return saved;
        }

        public Task DeleteCredential(Credential credential)
        {
            ArgumentNullException.ThrowIfNull(credential);
            return UpdateVault(vault =>
            {
                if (!vault.Credentials.Contains(credential))
                    return vault;

                // The tombstone keeps other devices and the cloud copy from bringing the credential back on sync.
                var deleted = new Dictionary<Guid, DateTimeOffset>(vault.DeletedCredentials)
                {
                    [credential.Id] = DateTimeOffset.UtcNow
                };
                var assignments = vault.Assignments;
                if (assignments.ContainsKey(credential.Id))
                {
                    assignments = new Dictionary<Guid, GroupAssignment>(assignments);
                    assignments.Remove(credential.Id);
                }

                return vault with
                {
                    Credentials = vault.Credentials.Where(item => !item.Equals(credential)).ToList(),
                    DeletedCredentials = deleted,
                    Assignments = assignments
                };
            });
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

        internal async Task<CredentialsMergeResult> Merge(PasswordVault incoming)
        {
            var result = CredentialsMergeResult.SuccessMergeResult;
            await UpdateVault(vault => MergeVaults(vault, incoming, result));
            return result;
        }

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
                if (!ReferenceEquals(winner, current) || !ReferenceEquals(history, current.PasswordHistory))
                {
                    var merged = winner.Clone();
                    merged.PasswordHistory = history;
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

            if (!changed && !result.AnyChanges)
                return local;

            return local with
            {
                Credentials = credentials,
                Groups = groups,
                Assignments = assignments,
                DeletedCredentials = deleted
            };
        }

        private static bool IsModifiedAfter(Credential credential, DateTimeOffset deletedAt) =>
            credential.LastModifiedTime.ToUniversalTime() > deletedAt.UtcDateTime;

        private static IReadOnlyList<PasswordHistoryEntry> MergePasswordHistory(Credential winner, Credential other)
        {
            if (other.PasswordHistory.Count == 0)
                return winner.PasswordHistory;

            var history = winner.PasswordHistory.Concat(other.PasswordHistory)
                .DistinctBy(entry => (entry.ChangedAtUtc, entry.Password))
                .OrderByDescending(entry => entry.ChangedAtUtc)
                .ThenBy(entry => entry.Password, StringComparer.Ordinal)
                .Take(Credential.PasswordHistoryLimit)
                .ToArray();
            return history.SequenceEqual(winner.PasswordHistory) ? winner.PasswordHistory : history;
        }

        private Task UpdateVault(Func<PasswordVault, PasswordVault> update) => Task.Run(async () =>
        {
            using var locker = await _asyncKeyedLocker.LockAsync(Constants.PasswordsFilePath).ConfigureAwait(false);
            if (_locked)
                throw new InvalidOperationException("The password vault is locked.");

            var updated = update(_vault);
            if (ReferenceEquals(updated, _vault))
                return;

            WriteVault(updated, GetPassword());
            _vault = updated;
        });

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

                    var newPasswordSecure = SecureStringHelper.MakeSecureString(newPassword);
                    try
                    {
                        WriteVault(_vault, newPassword);
                    }
                    catch
                    {
                        newPasswordSecure.Dispose();
                        throw;
                    }

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
