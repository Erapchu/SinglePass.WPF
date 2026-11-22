using SinglePass.Core.Models;
using SinglePass.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SinglePass.Core.Tests
{
    public class TrashTests
    {
        private static readonly DateTime T0 = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        private static Credential MakeCredential(Guid id, string name = "mail")
        {
            var credential = new Credential { Id = id, LastModifiedTime = T0, CreationTime = T0 };
            credential.NameField.Value = name;
            credential.EnsureTypeData();
            return credential;
        }

        private static (PasswordVault Vault, CredentialsMergeResult Result) Merge(PasswordVault local, PasswordVault incoming)
        {
            var result = CredentialsMergeResult.SuccessMergeResult;
            return (CredentialsCryptoService.MergeVaults(local, incoming, result), result);
        }

        private static bool InTrash(PasswordVault vault, Guid id) =>
            vault.Trash.TryGetValue(id, out var state) && state.IsTrashed;

        [Fact]
        public void MoveToTrash_KeepsCredentialAndGroupLink()
        {
            var id = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var vault = new PasswordVault
            {
                Credentials = [MakeCredential(id)],
                Groups = new() { [groupId] = new(new PasswordGroup(groupId, "Work", null), T0) },
                Assignments = new() { [id] = new(groupId, T0) }
            };

            var trashed = CredentialsCryptoService.MoveToTrash(vault, id, T0.AddHours(1));

            Assert.Single(trashed.Credentials);
            Assert.True(InTrash(trashed, id));
            Assert.Equal(groupId, trashed.Assignments[id].GroupId);
            Assert.Empty(trashed.DeletedCredentials);

            var restored = CredentialsCryptoService.RestoreFromTrash(trashed, [id], T0.AddHours(2));
            Assert.False(InTrash(restored, id));
            Assert.Equal(groupId, restored.Assignments[id].GroupId);
        }

        [Fact]
        public void IncomingTrash_HidesCredentialAndIsReported()
        {
            var id = Guid.NewGuid();
            var local = new PasswordVault { Credentials = [MakeCredential(id)] };
            var incoming = CredentialsCryptoService.MoveToTrash(local, id, T0.AddHours(1));

            var (merged, result) = Merge(local, incoming);

            Assert.True(InTrash(merged, id));
            Assert.True(result.TrashChanges);
            Assert.Empty(result.DeletedCredentials);
        }

        [Fact]
        public void LaterRestore_WinsOverEarlierTrash()
        {
            var id = Guid.NewGuid();
            var vault = new PasswordVault { Credentials = [MakeCredential(id)] };
            var trashed = CredentialsCryptoService.MoveToTrash(vault, id, T0.AddHours(1));
            var restored = CredentialsCryptoService.RestoreFromTrash(trashed, [id], T0.AddHours(2));

            Assert.False(InTrash(Merge(trashed, restored).Vault, id));
            Assert.False(InTrash(Merge(restored, trashed).Vault, id));
            Assert.Same(restored, Merge(restored, trashed).Vault);
        }

        [Fact]
        public void NewCredentialArrivingInTrash_IsNotReportedAsNew()
        {
            var id = Guid.NewGuid();
            var cloud = CredentialsCryptoService.MoveToTrash(
                new PasswordVault { Credentials = [MakeCredential(id)] }, id, T0.AddHours(1));

            var (merged, result) = Merge(new PasswordVault(), cloud);

            Assert.True(InTrash(merged, id));
            Assert.Empty(result.NewCredentials);
            Assert.False(result.TrashChanges);
        }

        [Fact]
        public void PurgedCredential_DropsTrashStateEverywhere()
        {
            var id = Guid.NewGuid();
            var trashed = CredentialsCryptoService.MoveToTrash(
                new PasswordVault { Credentials = [MakeCredential(id)] }, id, T0.AddHours(1));
            var purged = CredentialsCryptoService.RemoveCredentials(trashed, [id], T0.AddHours(2));

            Assert.Empty(purged.Credentials);
            Assert.Empty(purged.Trash);
            Assert.True(purged.DeletedCredentials.ContainsKey(id));

            var (merged, _) = Merge(trashed, purged);
            Assert.Empty(merged.Credentials);
            Assert.Empty(merged.Trash);
        }

        [Fact]
        public void PurgeExpiredTrash_RemovesOnlyEntriesOlderThanRetention()
        {
            var expired = Guid.NewGuid();
            var recent = Guid.NewGuid();
            var active = Guid.NewGuid();
            var vault = new PasswordVault { Credentials = [MakeCredential(expired), MakeCredential(recent), MakeCredential(active)] };
            vault = CredentialsCryptoService.MoveToTrash(vault, expired, T0);
            vault = CredentialsCryptoService.MoveToTrash(vault, recent, T0.AddDays(10));

            var now = T0 + PasswordVault.TrashRetention;
            var purged = CredentialsCryptoService.PurgeExpiredTrash(vault, now);

            Assert.Equal([recent, active], purged.Credentials.Select(credential => credential.Id));
            Assert.True(purged.DeletedCredentials.ContainsKey(expired));
            Assert.Same(purged, CredentialsCryptoService.PurgeExpiredTrash(purged, now));
        }

        [Fact]
        public void RestoringBackup_BringsCredentialBackFromTrash()
        {
            var id = Guid.NewGuid();
            var backup = new PasswordVault { Credentials = [MakeCredential(id)] };
            var current = CredentialsCryptoService.MoveToTrash(backup, id, T0.AddHours(1));

            var restored = CredentialsCryptoService.ApplyBackup(current, backup, T0.AddHours(2), out var result);

            Assert.False(InTrash(restored, id));
            Assert.Single(result.Restored);
        }

        [Fact]
        public void RestoringBackup_SkipsCredentialsThatWereInTrashThen()
        {
            var id = Guid.NewGuid();
            var backup = CredentialsCryptoService.MoveToTrash(
                new PasswordVault { Credentials = [MakeCredential(id)] }, id, T0);
            var current = CredentialsCryptoService.RemoveCredentials(backup, [id], T0.AddHours(1));

            var restored = CredentialsCryptoService.ApplyBackup(current, backup, T0.AddHours(2), out var result);

            Assert.Same(current, restored);
            Assert.False(result.AnyChanges);
        }

        [Fact]
        public void TrashState_SurvivesWriteAndRead()
        {
            var id = Guid.NewGuid();
            var vault = CredentialsCryptoService.MoveToTrash(
                new PasswordVault { Credentials = [MakeCredential(id)] }, id, T0);

            var crypto = new CryptoService();
            using var stream = new System.IO.MemoryStream();
            crypto.EncryptToStream(vault, stream, "password");
            stream.Position = 0;
            var read = CredentialsCryptoService.ReadVault(crypto, stream, "password", out _);

            Assert.Equal(PasswordVault.CurrentVersion, read.Version);
            Assert.Equal(new DateTimeOffset(T0), read.Trash[id].DeletedAt);
        }
    }
}
