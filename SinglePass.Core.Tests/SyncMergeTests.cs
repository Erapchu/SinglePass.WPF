using SinglePass.Core.Models;
using SinglePass.Core.Services;
using System;
using System.Text.Json;
using Xunit;

namespace SinglePass.Core.Tests
{
    public class SyncMergeTests
    {
        private static readonly DateTime T0 = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        private static Credential MakeCredential(Guid id, string name, DateTime modified)
        {
            var credential = new Credential { Id = id, LastModifiedTime = modified, CreationTime = modified };
            credential.NameField.Value = name;
            return credential;
        }

        // Sync uploads when merging the local vault into the downloaded one would change the latter.
        private static bool CloudNeedsUpdate(PasswordVault cloud, PasswordVault local) =>
            !ReferenceEquals(CredentialsCryptoService.MergeVaults(cloud, local, new CredentialsMergeResult()), cloud);

        [Fact]
        public void AfterMerge_IdenticalCloudCopy_NeedsNoUpload()
        {
            var cloud = new PasswordVault { Credentials = [MakeCredential(Guid.NewGuid(), "mail", T0)] };
            var local = CredentialsCryptoService.MergeVaults(new PasswordVault(), cloud, new CredentialsMergeResult());

            Assert.False(CloudNeedsUpdate(cloud, local));
        }

        [Fact]
        public void LocalOnlyChanges_NeedUpload()
        {
            var id = Guid.NewGuid();
            var cloud = new PasswordVault { Credentials = [MakeCredential(id, "mail", T0)] };

            Assert.True(CloudNeedsUpdate(cloud, new PasswordVault { Credentials = [MakeCredential(id, "mail", T0.AddMinutes(1))] }));
            Assert.True(CloudNeedsUpdate(cloud, new PasswordVault { DeletedCredentials = new() { [id] = T0.AddMinutes(1) } }));
            Assert.True(CloudNeedsUpdate(cloud, new PasswordVault { MasterPasswordChangedAt = T0 }));
        }

        [Fact]
        public void MasterPasswordChangedAt_KeepsLatest()
        {
            var local = new PasswordVault { MasterPasswordChangedAt = T0 };
            var incoming = new PasswordVault { MasterPasswordChangedAt = T0.AddDays(1) };

            var merged = CredentialsCryptoService.MergeVaults(local, incoming, new CredentialsMergeResult());
            Assert.Equal(T0.AddDays(1), merged.MasterPasswordChangedAt);

            Assert.Same(incoming, CredentialsCryptoService.MergeVaults(incoming, local, new CredentialsMergeResult()));
        }

        [Fact]
        public void CredentialTimes_AreStoredInUtc()
        {
            var local = T0.ToLocalTime();
            var credential = MakeCredential(Guid.NewGuid(), "mail", local);

            Assert.Equal(DateTimeKind.Utc, credential.LastModifiedTime.Kind);
            Assert.Equal(T0, credential.LastModifiedTime);
            Assert.Equal(T0, credential.CreationTime);
        }

        [Fact]
        public void OldLocalTimeWithOffset_IsReadAsSameInstant()
        {
            // Older builds saved DateTime.Now, which System.Text.Json writes with the local offset.
            var json = """{ "Id": "6f9619ff-8b86-d011-b42d-00cf4fc964ff", "LastModifiedTime": "2026-11-01T15:00:00+03:00" }""";

            var credential = JsonSerializer.Deserialize<Credential>(json);

            Assert.Equal(T0, credential.LastModifiedTime);
            Assert.Equal(DateTimeKind.Utc, credential.LastModifiedTime.Kind);
            Assert.EndsWith("Z\"", JsonSerializer.Serialize(credential.LastModifiedTime));
        }

        [Fact]
        public void OldLocalTimeAndNewUtcTime_CompareByInstant()
        {
            var id = Guid.NewGuid();
            var oldJson = """{ "Id": "ID", "LastModifiedTime": "2026-11-01T15:00:00+03:00" }""".Replace("ID", id.ToString());
            var old = JsonSerializer.Deserialize<Credential>(oldJson);
            old.NameField.Value = "old";
            var newer = MakeCredential(id, "new", T0.AddMinutes(1));

            var merged = CredentialsCryptoService.MergeVaults(
                new PasswordVault { Credentials = [old] }, new PasswordVault { Credentials = [newer] }, new CredentialsMergeResult());

            Assert.Equal("new", Assert.Single(merged.Credentials).NameField.Value);
        }
    }
}
