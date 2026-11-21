using SinglePass.Core.Models;
using SinglePass.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;

namespace SinglePass.Core.Tests
{
    public class VaultMergeTests
    {
        private static readonly DateTime T0 = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        private static Credential MakeCredential(Guid id, string name, DateTime modified)
        {
            var credential = new Credential { Id = id, LastModifiedTime = modified, CreationTime = modified };
            credential.NameField.Value = name;
            return credential;
        }

        private static (PasswordVault Vault, CredentialsMergeResult Result) Merge(PasswordVault local, PasswordVault incoming)
        {
            var result = CredentialsMergeResult.SuccessMergeResult;
            return (CredentialsCryptoService.MergeVaults(local, incoming, result), result);
        }

        [Fact]
        public void LocallyDeletedCredential_IsNotRestoredFromIncoming()
        {
            var id = Guid.NewGuid();
            var local = new PasswordVault { DeletedCredentials = new() { [id] = T0.AddHours(1) } };
            var incoming = new PasswordVault { Credentials = [MakeCredential(id, "mail", T0)] };

            var (merged, result) = Merge(local, incoming);

            Assert.Empty(merged.Credentials);
            Assert.True(merged.DeletedCredentials.ContainsKey(id));
            Assert.Empty(result.NewCredentials);
            Assert.False(result.AnyChanges);
        }

        [Fact]
        public void IncomingTombstone_RemovesLocalCredentialAndItsGroupLink()
        {
            var id = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var local = new PasswordVault
            {
                Credentials = [MakeCredential(id, "mail", T0)],
                Groups = new() { [groupId] = new(new PasswordGroup(groupId, "Work", null), T0) },
                Assignments = new() { [id] = new(groupId, T0) }
            };
            var incoming = new PasswordVault { DeletedCredentials = new() { [id] = T0.AddHours(1) } };

            var (merged, result) = Merge(local, incoming);

            Assert.Empty(merged.Credentials);
            Assert.Empty(merged.Assignments);
            Assert.Equal(T0.AddHours(1), merged.DeletedCredentials[id]);
            Assert.Equal("mail", Assert.Single(result.DeletedCredentials).NameField.Value);
            Assert.True(result.AnyChanges);
        }

        [Fact]
        public void EditAfterDeletion_RestoresCredentialAndDropsTombstone()
        {
            var id = Guid.NewGuid();
            var local = new PasswordVault { DeletedCredentials = new() { [id] = T0 } };
            var incoming = new PasswordVault { Credentials = [MakeCredential(id, "mail (new password)", T0.AddMinutes(5))] };

            var (merged, result) = Merge(local, incoming);

            Assert.Equal("mail (new password)", Assert.Single(merged.Credentials).NameField.Value);
            Assert.Empty(merged.DeletedCredentials);
            Assert.Single(result.NewCredentials);
            Assert.Empty(result.DeletedCredentials);
        }

        [Fact]
        public void LocalEditAfterIncomingDeletion_KeepsCredential()
        {
            var id = Guid.NewGuid();
            var local = new PasswordVault { Credentials = [MakeCredential(id, "mail", T0.AddHours(2))] };
            var incoming = new PasswordVault { DeletedCredentials = new() { [id] = T0.AddHours(1) } };

            var (merged, result) = Merge(local, incoming);

            Assert.Single(merged.Credentials);
            Assert.Empty(merged.DeletedCredentials);
            Assert.Empty(result.DeletedCredentials);
            Assert.NotSame(local, merged);
        }

        [Fact]
        public void LocalTimeModification_IsComparedInUtc()
        {
            var id = Guid.NewGuid();
            var deletedAt = new DateTimeOffset(T0);
            var editedBefore = T0.AddMinutes(-1).ToLocalTime();
            var local = new PasswordVault { Credentials = [MakeCredential(id, "mail", editedBefore)] };
            var incoming = new PasswordVault { DeletedCredentials = new() { [id] = deletedAt } };

            var (merged, _) = Merge(local, incoming);

            Assert.Empty(merged.Credentials);
        }

        [Fact]
        public void Tombstones_KeepLatestDeletionTime()
        {
            var id = Guid.NewGuid();
            var local = new PasswordVault { DeletedCredentials = new() { [id] = T0 } };
            var incoming = new PasswordVault { DeletedCredentials = new() { [id] = T0.AddDays(1) } };

            var (merged, _) = Merge(local, incoming);
            Assert.Equal(T0.AddDays(1), merged.DeletedCredentials[id]);

            var (unchanged, _) = Merge(merged, local);
            Assert.Same(merged, unchanged);
        }

        [Fact]
        public void IncomingOnlyTombstone_IsStoredWithoutReportingChanges()
        {
            var id = Guid.NewGuid();
            var incoming = new PasswordVault { DeletedCredentials = new() { [id] = T0 } };

            var (merged, result) = Merge(new PasswordVault(), incoming);

            Assert.True(merged.DeletedCredentials.ContainsKey(id));
            Assert.False(result.AnyChanges);
        }

        [Fact]
        public void IncomingAssignmentForDeletedCredential_IsIgnored()
        {
            var id = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var group = new GroupChange(new PasswordGroup(groupId, "Work", null), T0);
            var local = new PasswordVault
            {
                Groups = new() { [groupId] = group },
                DeletedCredentials = new() { [id] = T0.AddHours(1) }
            };
            var incoming = new PasswordVault
            {
                Credentials = [MakeCredential(id, "mail", T0)],
                Groups = new() { [groupId] = group },
                Assignments = new() { [id] = new(groupId, T0) }
            };

            var (merged, result) = Merge(local, incoming);

            Assert.Empty(merged.Assignments);
            Assert.False(result.GroupChanges);
            Assert.Same(local, merged);
        }

        [Fact]
        public void ChangedCredentials_ReportsWinningVersion()
        {
            var id = Guid.NewGuid();
            var local = new PasswordVault { Credentials = [MakeCredential(id, "old name", T0)] };
            var incoming = new PasswordVault { Credentials = [MakeCredential(id, "new name", T0.AddHours(1))] };

            var (merged, result) = Merge(local, incoming);

            Assert.Equal("new name", Assert.Single(merged.Credentials).NameField.Value);
            Assert.Equal("new name", Assert.Single(result.ChangedCredentials).NameField.Value);
        }

        [Fact]
        public void SshKeyEntry_LaterEditWins()
        {
            var id = Guid.NewGuid();
            var local = MakeCredential(id, "server", T0);
            local.Type = CredentialType.SshKey;
            local.SshKey = new SshKeyData { PrivateKey = "old", PublicKey = "ssh-ed25519 AAAA" };
            var incoming = MakeCredential(id, "server", T0.AddHours(1));
            incoming.Type = CredentialType.SshKey;
            incoming.SshKey = new SshKeyData { PrivateKey = "new", PublicKey = "ssh-ed25519 BBBB", Passphrase = "pass" };

            var (merged, _) = Merge(new PasswordVault { Credentials = [local] }, new PasswordVault { Credentials = [incoming] });

            var credential = Assert.Single(merged.Credentials);
            Assert.Equal(CredentialType.SshKey, credential.Type);
            Assert.Equal("new", credential.SshKey.PrivateKey);
            Assert.Equal("ssh-ed25519 BBBB", credential.SshKey.PublicKey);
            Assert.Equal("pass", credential.SshKey.Passphrase);
        }
    }

    public class VaultFormatTests
    {
        private const string Password = "correct horse battery staple";

        private static PasswordVault RoundTrip(object payload)
        {
            var crypto = new CryptoService();
            using var stream = new MemoryStream();
            crypto.EncryptToStream(payload, stream, Password);
            return CredentialsCryptoService.ReadVault(crypto, stream, Password, out _);
        }

        [Fact]
        public void Version1Vault_IsUpgradedWithEmptyTombstones()
        {
            var id = Guid.NewGuid();
            var v1 = new Dictionary<string, object>
            {
                ["Version"] = 1,
                ["Credentials"] = new[] { new Credential { Id = id } },
                ["Groups"] = new Dictionary<Guid, object>(),
                ["Assignments"] = new Dictionary<Guid, object>()
            };

            var vault = RoundTrip(v1);

            Assert.Equal(PasswordVault.CurrentVersion, vault.Version);
            Assert.Empty(vault.DeletedCredentials);
            Assert.Equal(id, Assert.Single(vault.Credentials).Id);
        }

        [Fact]
        public void CurrentVault_KeepsTombstones()
        {
            var id = Guid.NewGuid();
            var deletedAt = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

            var vault = RoundTrip(new PasswordVault { DeletedCredentials = new() { [id] = deletedAt } });

            Assert.Equal(deletedAt, vault.DeletedCredentials[id]);
        }

        [Fact]
        public void Version2Vault_ReadsEntriesAsLogins()
        {
            var id = Guid.NewGuid();
            var v2 = new Dictionary<string, object>
            {
                ["Version"] = 2,
                ["Credentials"] = new[] { new Dictionary<string, object> { ["Id"] = id } },
                ["Groups"] = new Dictionary<Guid, object>(),
                ["Assignments"] = new Dictionary<Guid, object>(),
                ["DeletedCredentials"] = new Dictionary<Guid, object>()
            };

            var vault = RoundTrip(v2);

            Assert.Equal(PasswordVault.CurrentVersion, vault.Version);
            var credential = Assert.Single(vault.Credentials);
            Assert.Equal(CredentialType.Login, credential.Type);
            Assert.Null(credential.SshKey);
        }

        [Fact]
        public void SshKeyEntry_SurvivesFile()
        {
            var credential = Credential.CreateNew(CredentialType.SshKey);
            credential.SshKey.PrivateKey = "private";
            credential.SshKey.PublicKey = "ssh-ed25519 AAAA";
            credential.SshKey.Passphrase = "pass";

            var stored = Assert.Single(RoundTrip(new PasswordVault { Credentials = [credential] }).Credentials);

            Assert.Equal(CredentialType.SshKey, stored.Type);
            Assert.Equal("private", stored.SshKey.PrivateKey);
            Assert.Equal("ssh-ed25519 AAAA", stored.SshKey.PublicKey);
            Assert.Equal("pass", stored.SshKey.Passphrase);
        }

        [Fact]
        public void NewSshKeyEntry_HasKeyData()
        {
            var credential = Credential.CreateNew(CredentialType.SshKey);
            credential.SshKey.PrivateKey = "key";

            var clone = credential.Clone();
            clone.SshKey.PrivateKey = "changed";

            Assert.Equal(CredentialType.SshKey, clone.Type);
            Assert.Equal("key", credential.SshKey.PrivateKey);
            Assert.Null(Credential.CreateNew().SshKey);
        }

        [Fact]
        public void NewerVaultVersion_IsRejected()
        {
            Assert.Throws<JsonException>(() => RoundTrip(new PasswordVault { Version = PasswordVault.CurrentVersion + 1 }));
        }
    }
}
