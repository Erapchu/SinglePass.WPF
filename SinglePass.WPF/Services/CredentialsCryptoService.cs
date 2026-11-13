using AsyncKeyedLock;
using Microsoft.Extensions.Logging;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Settings;
using SinglePass.WPF.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json;
using System.Threading.Tasks;

namespace SinglePass.WPF.Services
{
    internal enum CredentialLoadResult
    {
        Loaded,
        FileNotFound,
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
        private readonly AppSettings _settings;
        private volatile PasswordVault _vault = new();

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
            AsyncKeyedLocker<string> asyncKeyedLocker,
            AppSettings settings = null)
        {
            _logger = logger;
            _cryptoService = cryptoService;
            _asyncKeyedLocker = asyncKeyedLocker;
            _settings = settings;
        }

        public void SetPassword(string password)
        {
            PasswordSecure = SecureStringHelper.MakeSecureString(password);
        }

        public string GetPassword() => SecureStringHelper.GetString(PasswordSecure);

        internal static PasswordVault ReadVault(CryptoService cryptoService, Stream stream, string password, out bool isLegacy)
        {
            var json = cryptoService.DecryptFromStream<JsonElement>(stream, password, out isLegacy);
            if (json.ValueKind == JsonValueKind.Array)
                return new PasswordVault { Credentials = json.Deserialize<List<Credential>>() };

            if (json.ValueKind != JsonValueKind.Object ||
                !json.TryGetProperty(nameof(PasswordVault.Version), out var version) ||
                !version.TryGetInt32(out var number) || number != 1)
                throw new JsonException("Unsupported password vault format.");

            var vault = json.Deserialize<PasswordVault>();
            if (vault.Credentials == null || vault.Groups == null || vault.Assignments == null)
                throw new JsonException("Incomplete password vault.");

            return vault;
        }

        internal Task<CredentialLoadResult> LoadCredentialsAsync(string password) => Task.Run(async () =>
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
                catch (FileNotFoundException)
                {
                    if (_settings?.PasswordGroups is not { Groups.Count: > 0 })
                        return CredentialLoadResult.FileNotFound;

                    vault = new PasswordVault();
                }

                if (_settings?.PasswordGroups is { Groups.Count: > 0 } legacy && vault.Groups.Count == 0)
                {
                    var groups = legacy.Groups.ToDictionary(group => group.Id, group => new GroupChange(group, DateTimeOffset.MinValue));
                    var credentials = vault.Credentials.Select(credential => credential.Id).ToHashSet();
                    var assignments = legacy.Assignments
                        .Where(pair => credentials.Contains(pair.Key) && groups.ContainsKey(pair.Value))
                        .ToDictionary(pair => pair.Key, pair => new GroupAssignment(pair.Value, DateTimeOffset.MinValue));
                    vault = vault with { Groups = groups, Assignments = assignments };
                    if (File.Exists(path) && !File.Exists(path + ".before-groups.bak"))
                        File.Copy(path, path + ".before-groups.bak", overwrite: false);
                    needsRewrite = true;
                }

                if (needsRewrite)
                    WriteVault(vault, password);

                _vault = vault;
                if (_settings != null)
                    _settings.PasswordGroups = null;
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
                return groupId.HasValue ? AssignGroupOrThrow(updated, credential.Id, groupId) : updated;
            });
        }

        public Task EditCredential(Credential credential) => EditCredential(credential, null, changeGroup: false);

        internal Task EditCredential(Credential credential, Guid? groupId, bool changeGroup)
        {
            ArgumentNullException.ThrowIfNull(credential);
            return UpdateVault(vault =>
            {
                var index = vault.Credentials.IndexOf(credential);
                if (index < 0)
                    return vault;

                var credentials = vault.Credentials.ToList();
                credentials[index] = credential;
                var updated = vault with { Credentials = credentials };
                return changeGroup ? AssignGroupOrThrow(updated, credential.Id, groupId) : updated;
            });
        }

        public Task DeleteCredential(Credential credential)
        {
            ArgumentNullException.ThrowIfNull(credential);
            return UpdateVault(vault => vault.Credentials.Contains(credential)
                ? vault with { Credentials = vault.Credentials.Where(item => !item.Equals(credential)).ToList() }
                : vault);
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
            await UpdateVault(vault =>
            {
                var credentials = vault.Credentials.ToList();
                foreach (var credential in incoming.Credentials)
                {
                    var index = credentials.IndexOf(credential);
                    if (index < 0)
                    {
                        credentials.Add(credential);
                        result.NewCredentials.Add(credential);
                    }
                    else if (credentials[index].LastModifiedTime < credential.LastModifiedTime)
                    {
                        result.ChangedCredentials.Add(credentials[index]);
                        credentials[index] = credential;
                    }
                }

                var groups = new Dictionary<Guid, GroupChange>(vault.Groups);
                foreach (var (id, change) in incoming.Groups)
                {
                    if (!groups.TryGetValue(id, out var local) ||
                        !local.IsDeleted && (change.IsDeleted || change.ModifiedAt > local.ModifiedAt))
                    {
                        groups[id] = change;
                        result.GroupChanges = true;
                    }
                }

                var assignments = new Dictionary<Guid, GroupAssignment>(vault.Assignments);
                foreach (var (id, assignment) in incoming.Assignments)
                {
                    if (!assignments.TryGetValue(id, out var local) || assignment.ModifiedAt > local.ModifiedAt)
                    {
                        assignments[id] = assignment;
                        result.GroupChanges = true;
                    }
                }

                return result.AnyChanges ? vault with { Credentials = credentials, Groups = groups, Assignments = assignments } : vault;
            });
            return result;
        }

        private Task UpdateVault(Func<PasswordVault, PasswordVault> update) => Task.Run(async () =>
        {
            using var locker = await _asyncKeyedLocker.LockAsync(Constants.PasswordsFilePath).ConfigureAwait(false);
            var updated = update(_vault);
            if (ReferenceEquals(updated, _vault))
                return;

            WriteVault(updated, GetPassword());
            _vault = updated;
        });

        private void WriteVault(PasswordVault vault, string password)
        {
            var path = Constants.PasswordsFilePath;
            var tempPath = path + ".tmp";
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    _cryptoService.EncryptToStream(vault, stream, password);
                    stream.Flush(flushToDisk: true);
                }

                File.Move(tempPath, path, overwrite: true);
            }
            catch
            {
                try { File.Delete(tempPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
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

                    PasswordSecure = newPasswordSecure;
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
