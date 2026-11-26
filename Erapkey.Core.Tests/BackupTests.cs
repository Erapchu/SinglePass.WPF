using AsyncKeyedLock;
using Microsoft.Extensions.Logging.Abstractions;
using Erapkey.Core.Models;
using Erapkey.Core.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Erapkey.Core.Tests
{
    public sealed class VaultBackupsTests : IDisposable
    {
        private static readonly DateTimeOffset T0 = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        private readonly string _root = Path.Combine(Path.GetTempPath(), "Erapkey.Tests." + Guid.NewGuid().ToString("N"));
        private readonly string _vaultPath;
        private readonly VaultBackups _backups;

        public VaultBackupsTests()
        {
            Directory.CreateDirectory(_root);
            _vaultPath = Path.Combine(_root, "erapkey.dat");
            _backups = new VaultBackups(_vaultPath, Path.Combine(_root, "Backups"));
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);

        [Fact]
        public void NoVaultFile_NoBackup()
        {
            Assert.Null(_backups.Create(VaultBackupReason.Sync, T0));
            Assert.Empty(_backups.List());
        }

        [Fact]
        public void Create_CopiesFileAndListsNewestFirst()
        {
            File.WriteAllText(_vaultPath, "first");
            _backups.Create(VaultBackupReason.Daily, T0);
            File.WriteAllText(_vaultPath, "second");
            _backups.Create(VaultBackupReason.Sync, T0.AddHours(1));

            var list = _backups.List();

            Assert.Equal(2, list.Count);
            Assert.Equal(VaultBackupReason.Sync, list[0].Reason);
            Assert.Equal(T0.AddHours(1), list[0].CreatedAt);
            Assert.Equal("second", File.ReadAllText(list[0].Path));
            Assert.Equal("first", File.ReadAllText(list[1].Path));
        }

        [Fact]
        public void Daily_OnlyWhenNewestBackupIsADayOld()
        {
            File.WriteAllText(_vaultPath, "vault");

            Assert.NotNull(_backups.CreateDailyIfDue(T0));
            Assert.Null(_backups.CreateDailyIfDue(T0.AddHours(23)));
            _backups.Create(VaultBackupReason.Sync, T0.AddHours(20));
            Assert.Null(_backups.CreateDailyIfDue(T0.AddHours(30)));
            Assert.NotNull(_backups.CreateDailyIfDue(T0.AddHours(44)));
        }

        [Fact]
        public void KeepsOnlyNewestBackups()
        {
            File.WriteAllText(_vaultPath, "vault");
            for (var i = 0; i < VaultBackups.MaxBackups + 5; i++)
                _backups.Create(VaultBackupReason.Sync, T0.AddMinutes(i));

            var list = _backups.List();

            Assert.Equal(VaultBackups.MaxBackups, list.Count);
            Assert.Equal(T0.AddMinutes(VaultBackups.MaxBackups + 4), list[0].CreatedAt);
            Assert.Equal(T0.AddMinutes(5), list[^1].CreatedAt);
        }

        [Fact]
        public void ForeignFiles_AreIgnored()
        {
            File.WriteAllText(_vaultPath, "vault");
            _backups.Create(VaultBackupReason.Sync, T0);
            File.WriteAllText(Path.Combine(_root, "Backups", "erapkey-notes.dat"), "x");
            File.WriteAllText(Path.Combine(_root, "Backups", "erapkey-20261101-120000-000-other.dat"), "x");

            Assert.Single(_backups.List());
        }

        [Fact]
        public async Task ReadBackup_ReturnsNullForWrongPassword()
        {
            var crypto = new CryptoService();
            using (var stream = File.Create(_vaultPath))
                crypto.EncryptToStream(new PasswordVault(), stream, "right password");
            var service = new CredentialsCryptoService(
                NullLogger<CredentialsCryptoService>.Instance, crypto, new AsyncKeyedLocker<string>());

            Assert.Null(await service.ReadBackup(_vaultPath, "wrong password"));
            Assert.NotNull(await service.ReadBackup(_vaultPath, "right password"));
        }
    }

    public class ApplyBackupTests
    {
        private static readonly DateTime T0 = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

        private static Credential MakeCredential(Guid id, string name, string password, DateTime modified)
        {
            var credential = new Credential { Id = id, LastModifiedTime = modified, CreationTime = modified };
            credential.NameField.Value = name;
            credential.Login.PasswordField.Value = password;
            return credential;
        }

        private static PasswordVault Apply(PasswordVault current, PasswordVault backup, out BackupRestoreResult result) =>
            CredentialsCryptoService.ApplyBackup(current, backup, Now, out result);

        [Fact]
        public void DeletedCredential_ComesBackAndWinsOverCloudTombstone()
        {
            var id = Guid.NewGuid();
            var backup = new PasswordVault { Credentials = [MakeCredential(id, "mail", "p1", T0)] };
            var deletedAt = T0.AddDays(5);
            var current = new PasswordVault { DeletedCredentials = new() { [id] = deletedAt } };

            var restored = Apply(current, backup, out var result);

            var credential = Assert.Single(restored.Credentials);
            Assert.Equal(Now.UtcDateTime, credential.LastModifiedTime);
            Assert.Empty(restored.DeletedCredentials);
            Assert.Single(result.Restored);

            // The cloud copy still has the tombstone: the restored credential must survive the next sync.
            var cloud = new PasswordVault { DeletedCredentials = new() { [id] = deletedAt } };
            var synced = CredentialsCryptoService.MergeVaults(restored, cloud, new CredentialsMergeResult());
            Assert.Single(synced.Credentials);
            var cloudAfterUpload = CredentialsCryptoService.MergeVaults(cloud, restored, new CredentialsMergeResult());
            Assert.Single(cloudAfterUpload.Credentials);
            Assert.Empty(cloudAfterUpload.DeletedCredentials);
        }

        [Fact]
        public void ChangedCredential_IsRevertedAndCurrentPasswordKeptInHistory()
        {
            var id = Guid.NewGuid();
            var backup = new PasswordVault { Credentials = [MakeCredential(id, "mail", "old", T0)] };
            var current = new PasswordVault { Credentials = [MakeCredential(id, "mail", "new", T0.AddDays(1))] };

            var restored = Apply(current, backup, out var result);

            var credential = Assert.Single(restored.Credentials);
            Assert.Equal("old", credential.Login.PasswordField.Value);
            Assert.Contains(credential.Login.PasswordHistory, entry => entry.Password == "new");
            Assert.Single(result.Reverted);
        }

        [Fact]
        public void LaterCredentials_StayAndUnchangedOnesAreUntouched()
        {
            var same = MakeCredential(Guid.NewGuid(), "same", "p", T0);
            var later = MakeCredential(Guid.NewGuid(), "later", "p", T0.AddDays(2));
            var backup = new PasswordVault { Credentials = [MakeCredential(same.Id, "same", "p", T0)] };
            var current = new PasswordVault { Credentials = [same, later] };

            var restored = Apply(current, backup, out var result);

            Assert.Same(current, restored);
            Assert.False(result.AnyChanges);
        }

        [Fact]
        public void DeletedGroup_ComesBackUnderNewIdWithItsCredentials()
        {
            var id = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var group = new PasswordGroup(groupId, "Work", null);
            var backup = new PasswordVault
            {
                Credentials = [MakeCredential(id, "mail", "p", T0)],
                Groups = new() { [groupId] = new(group, T0) },
                Assignments = new() { [id] = new(groupId, T0) }
            };
            var current = new PasswordVault
            {
                Credentials = [MakeCredential(id, "mail", "p", T0)],
                Groups = new() { [groupId] = new(null, T0.AddDays(1), IsDeleted: true) },
                Assignments = new() { [id] = new(groupId, T0) }
            };

            var restored = Apply(current, backup, out var result);

            var revived = Assert.Single(restored.Groups.Values, change => !change.IsDeleted);
            Assert.NotEqual(groupId, revived.Group.Id);
            Assert.Equal("Work", revived.Group.Name);
            Assert.True(restored.Groups[groupId].IsDeleted);
            Assert.Equal(revived.Group.Id, restored.Assignments[id].GroupId);
            Assert.True(result.GroupsChanged);
        }
    }
}
