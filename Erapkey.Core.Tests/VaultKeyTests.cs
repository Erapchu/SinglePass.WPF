using AsyncKeyedLock;
using Microsoft.Extensions.Logging.Abstractions;
using Erapkey.Core.Models;
using Erapkey.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Erapkey.Core.Tests
{
    /// <summary>Encryption format 2: the vault key, its password slot and the checks around them.</summary>
    public class VaultKeyFormatTests
    {
        private const string Password = "correct horse battery staple";
        private static readonly CryptoService Crypto = new();

        private static byte[] Write(object payload, VaultKey key)
        {
            using var stream = new MemoryStream();
            Crypto.EncryptToStream(payload, stream, key);
            return stream.ToArray();
        }

        private static Stream Open(byte[] file) => new MemoryStream(file, writable: false);

        [Fact]
        public void PasswordOpensTheFile_AndGivesTheSameKey()
        {
            using var key = Crypto.CreateKey(Password);
            var file = Write(new[] { "secret" }, key);

            var read = Crypto.DecryptFromStream<string[]>(Open(file), Password, out var fileKey);

            using (fileKey)
            {
                Assert.Equal(["secret"], read);
                Assert.True(fileKey.IsSameKey(key.Id));
                Assert.True(fileKey.Key.SequenceEqual(key.Key));
            }
        }

        [Fact]
        public void WrongPassword_Throws()
        {
            using var key = Crypto.CreateKey(Password);
            var file = Write(new[] { "secret" }, key);

            Assert.ThrowsAny<CryptographicException>(() => Crypto.DecryptFromStream<string[]>(Open(file), "wrong password", out _));
        }

        [Fact]
        public void SavingWithTheKey_KeepsTheHeader()
        {
            using var key = Crypto.CreateKey(Password);

            var first = Write(new[] { "one" }, key);
            var second = Write(new[] { "two" }, key);

            // Same salt and password slot: saving does not derive a new key from the password.
            Assert.Equal(key.Header, first[..key.Header.Length]);
            Assert.Equal(key.Header, second[..key.Header.Length]);
            Assert.NotEqual(first[key.Header.Length..], second[key.Header.Length..]);
        }

        [Theory]
        [InlineData(20)]  // salt
        [InlineData(40)]  // key id
        [InlineData(70)]  // encrypted vault key
        [InlineData(115)] // contents nonce
        [InlineData(-1)]  // contents
        public void AnyChangedByte_FailsDecryption(int offset)
        {
            using var key = Crypto.CreateKey(Password);
            var file = Write(new[] { "secret" }, key);
            file[offset < 0 ? file.Length + offset : offset] ^= 1;

            Assert.ThrowsAny<Exception>(() => Crypto.DecryptFromStream<string[]>(Open(file), Password, out _));
        }

        [Fact]
        public void TheKeyOpensOnlyItsOwnFiles()
        {
            using var key = Crypto.CreateKey(Password);
            using var otherKey = Crypto.CreateKey(Password);
            var file = Write(new[] { "secret" }, key);

            Assert.True(Crypto.TryDecryptWithKey<string[]>(Open(file), key.Key, out var read, out var fileKey));
            fileKey.Dispose();
            Assert.Equal(["secret"], read);
            Assert.False(Crypto.TryDecryptWithKey<string[]>(Open(file), otherKey.Key, out _, out _));

            using var format1 = new MemoryStream();
            Crypto.EncryptToStreamFormat1(new[] { "secret" }, format1, Password);
            Assert.False(Crypto.TryDecryptWithKey<string[]>(Open(format1.ToArray()), key.Key, out _, out _));
        }

        [Fact]
        public void OpensWithPassword_ChecksThePasswordSlot()
        {
            using var key = Crypto.CreateKey(Password);

            Assert.True(Crypto.OpensWithPassword(key, Password));
            Assert.False(Crypto.OpensWithPassword(key, "wrong password"));
        }

        [Fact]
        public void IsWrittenWith_RejectsAFileOfAnotherKey()
        {
            using var key = Crypto.CreateKey(Password);
            using var otherKey = Crypto.CreateKey(Password);
            var file = Write(new[] { "secret" }, key);

            Assert.True(Crypto.IsWrittenWith(Open(file), key));
            Assert.False(Crypto.IsWrittenWith(Open(file), otherKey));
        }

        [Fact]
        public void DisposedKey_CannotBeUsed()
        {
            var key = Crypto.CreateKey(Password);
            key.Dispose();

            Assert.Throws<ObjectDisposedException>(() => Write(new[] { "secret" }, key));
        }

        [Fact]
        public void NewerFormat_IsReportedAsNewerVersion()
        {
            using var key = Crypto.CreateKey(Password);
            var file = Write(new[] { "secret" }, key);
            file[8] = 3;

            Assert.Throws<UnsupportedVaultVersionException>(() => Crypto.DecryptFromStream<string[]>(Open(file), Password, out _));
            Assert.Throws<UnsupportedVaultVersionException>(() => Crypto.TryDecryptWithKey<string[]>(Open(file), key.Key, out _, out _));
        }

        [Fact]
        public void KdfParametersAboveTheLimits_AreReportedAsNewerVersion_BeforeDerivingTheKey()
        {
            using var key = Crypto.CreateKey(Password);
            var file = Write(new[] { "secret" }, key);
            BitConverter.TryWriteBytes(file.AsSpan(9), 64 * 1024 * 1024); // 64 GiB of memory

            Assert.Throws<UnsupportedVaultVersionException>(() => Crypto.DecryptFromStream<string[]>(Open(file), Password, out _));
        }

        [Fact]
        public void KdfParametersBelowTheLimits_AreInvalid()
        {
            using var key = Crypto.CreateKey(Password);
            var file = Write(new[] { "secret" }, key);
            BitConverter.TryWriteBytes(file.AsSpan(13), 0); // no iterations

            Assert.Throws<InvalidDataException>(() => Crypto.DecryptFromStream<string[]>(Open(file), Password, out _));
        }

        [Fact]
        public void Format1AndLegacyFiles_HaveNoVaultKey()
        {
            using var format1 = new MemoryStream();
            Crypto.EncryptToStreamFormat1(new[] { "secret" }, format1, Password);

            Assert.Equal(["secret"], Crypto.DecryptFromStream<string[]>(format1, Password, out var key));
            Assert.Null(key);
        }
    }

    /// <summary>The vault on disk: unlocking, converting older files, saving, changing the password and merging.</summary>
    public sealed class VaultKeyServiceTests : IDisposable
    {
        private const string Password = "correct horse battery staple";
        private const string FixturePassword = "fixture password 1.9.0";
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ErapkeyTests", Guid.NewGuid().ToString("N"));
        private readonly CryptoService _crypto = new();

        public void Dispose()
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException) { }
        }

        private sealed class KeyObserver : IVaultKeyObserver
        {
            public List<byte[]> Keys { get; } = new();
            public void OnMasterPasswordEntered(ReadOnlySpan<byte> vaultKey) => Keys.Add(vaultKey.ToArray());
        }

        private sealed record Device(CredentialsCryptoService Service, string VaultPath, string BackupsPath, KeyObserver Observer);

        private Device NewDevice(string name)
        {
            var directory = Path.Combine(_root, name);
            Directory.CreateDirectory(directory);
            var vaultPath = Path.Combine(directory, "erapkey.dat");
            var backupsPath = Path.Combine(directory, "Backups");
            var observer = new KeyObserver();
            var service = new CredentialsCryptoService(NullLogger<CredentialsCryptoService>.Instance, _crypto,
                new AsyncKeyedLocker<string>(), observer, vaultPath, backupsPath);
            return new Device(service, vaultPath, backupsPath, observer);
        }

        /// <summary>The same device after a restart: a new service on the same files.</summary>
        private Device Restart(Device device)
        {
            var observer = new KeyObserver();
            var service = new CredentialsCryptoService(NullLogger<CredentialsCryptoService>.Instance, _crypto,
                new AsyncKeyedLocker<string>(), observer, device.VaultPath, device.BackupsPath);
            return device with { Service = service, Observer = observer };
        }

        private void WriteFormat1(string path, PasswordVault vault, string password)
        {
            using var stream = File.Create(path);
            _crypto.EncryptToStreamFormat1(vault, stream, password);
        }

        private static Credential NewLogin(string name)
        {
            var credential = Credential.CreateNew();
            credential.NameField.Value = name;
            credential.Login.PasswordField.Value = name + " password";
            credential.LastModifiedTime = DateTime.UtcNow;
            return credential;
        }

        private static int FormatOf(string path)
        {
            using var stream = File.OpenRead(path);
            return CryptoService.ReadFormat(stream);
        }

        private PasswordVault ReadWithPassword(string path, string password)
        {
            using var stream = File.OpenRead(path);
            var vault = CredentialsCryptoService.ReadVault(_crypto, stream, password, out var key);
            key?.Dispose();
            return vault;
        }

        private (PasswordVault Vault, VaultKey Key) ReadCloud(byte[] file, string password)
        {
            using var stream = new MemoryStream(file, writable: false);
            var vault = CredentialsCryptoService.ReadVault(_crypto, stream, password, out var key);
            return (vault, key);
        }

        private static byte[] KeyId(CredentialsCryptoService service) => VaultKey.ComputeId(service.CopyVaultKey());

        [Fact]
        public async Task VaultOfRelease190_OpensAndIsConvertedWithoutLosses()
        {
            // Written by the real Erapkey 1.9.0 code: format 1, contents version 1, login fields at the top level.
            var device = NewDevice("a");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", "vault-v1.9.0.dat"), device.VaultPath);

            Assert.Equal(CredentialLoadResult.Loaded, await device.Service.LoadCredentialsAsync(FixturePassword));

            void AssertContents(CredentialsCryptoService service)
            {
                var mail = service.Credentials.Single(c => c.NameField.Value == "Mail");
                Assert.Equal(CredentialType.Login, mail.Type);
                Assert.Equal("user@example.com", mail.Login.LoginField.Value);
                Assert.Equal("p@ss-Wörd 1", mail.Login.PasswordField.Value);
                Assert.Equal("https://mail.example.com", mail.Login.SiteField.Value);
                Assert.Equal("JBSWY3DPEHPK3PXP", mail.Login.Totp.SecretBase32);
                Assert.Equal("Recovery codes in the safe", mail.OtherField.Value);
                Assert.Equal("4321", Assert.Single(mail.AdditionalFields).Value);
                var bank = service.Credentials.Single(c => c.NameField.Value == "Bank");
                Assert.Equal("s3cret", bank.Login.PasswordField.Value);
                var group = Assert.Single(service.PasswordGroups);
                Assert.Equal("Work", group.Name);
                Assert.Equal(group.Id, service.GetGroupId(mail.Id));
                Assert.Null(service.GetGroupId(bank.Id));
            }

            AssertContents(device.Service);
            Assert.Equal(2, FormatOf(device.VaultPath));
            var backup = Assert.Single(device.Service.Backups);
            Assert.Equal(VaultBackupReason.Upgrade, backup.Reason);
            Assert.Equal(1, FormatOf(backup.Path));

            // After a restart the converted file opens with the same password and without a rewrite.
            var bytes = File.ReadAllBytes(device.VaultPath);
            var restarted = Restart(device);
            Assert.Equal(CredentialLoadResult.Loaded, await restarted.Service.LoadCredentialsAsync(FixturePassword));
            AssertContents(restarted.Service);
            Assert.Equal(bytes, File.ReadAllBytes(device.VaultPath));
            Assert.Null(restarted.Service.FirstSyncPassword);
        }

        [Fact]
        public async Task ConvertingAFile_KeepsThePasswordForTheFirstSyncOnly()
        {
            var device = NewDevice("a");
            WriteFormat1(device.VaultPath, new PasswordVault { Credentials = [NewLogin("mail")] }, Password);

            await device.Service.LoadCredentialsAsync(Password);

            Assert.Equal(Password, device.Service.FirstSyncPassword);
            var key = Assert.Single(device.Observer.Keys);
            Assert.Equal(device.Service.CopyVaultKey(), key);

            await device.Service.LockAsync();
            Assert.Null(device.Service.FirstSyncPassword);
            Assert.Null(device.Service.CopyVaultKey());
        }

        [Fact]
        public async Task FileThatCannotBeRewritten_OpensAnyway_AndIsConvertedOnTheNextSave()
        {
            var device = NewDevice("a");
            WriteFormat1(device.VaultPath, new PasswordVault { Credentials = [NewLogin("mail")] }, Password);
            File.SetAttributes(device.VaultPath, FileAttributes.ReadOnly);

            // Several unlocks while the file is read-only: each opens, the file stays as it was, one upgrade backup.
            for (var i = 0; i < 3; i++)
            {
                var attempt = Restart(device);
                Assert.Equal(CredentialLoadResult.Loaded, await attempt.Service.LoadCredentialsAsync(Password));
                Assert.Equal("mail", Assert.Single(attempt.Service.Credentials).NameField.Value);
                Assert.Equal(1, FormatOf(device.VaultPath));
            }
            Assert.Single(device.Service.Backups, backup => backup.Reason == VaultBackupReason.Upgrade);

            File.SetAttributes(device.VaultPath, FileAttributes.Normal);
            var session = Restart(device);
            await session.Service.LoadCredentialsAsync(Password);
            await session.Service.AddCredential(NewLogin("bank"));

            Assert.Equal(2, FormatOf(device.VaultPath));
            Assert.Equal(2, ReadWithPassword(device.VaultPath, Password).Credentials.Count);
        }

        [Fact]
        public async Task UnconvertedFile_IsRewrittenBeforeUpload()
        {
            var device = NewDevice("a");
            WriteFormat1(device.VaultPath, new PasswordVault { Credentials = [NewLogin("mail")] }, Password);
            File.SetAttributes(device.VaultPath, FileAttributes.ReadOnly);
            await device.Service.LoadCredentialsAsync(Password);
            File.SetAttributes(device.VaultPath, FileAttributes.Normal);

            // Nothing new in the cloud, yet the local file must not be uploaded in format 1 under a key it does not have.
            var cloud = File.ReadAllBytes(device.VaultPath);
            var (incoming, _) = ReadCloud(cloud, Password);
            await device.Service.MergeFromCloud(incoming, null, Password, new CredentialsMergeResult());

            Assert.Equal(2, FormatOf(device.VaultPath));
        }

        [Fact]
        public async Task ChangingThePassword_ForgetsTheFirstSyncPassword()
        {
            var device = NewDevice("a");
            await device.Service.LoadCredentialsAsync(Password, allowCreate: true);
            Assert.Equal(Password, device.Service.FirstSyncPassword);

            await device.Service.ChangePasswordAsync(Password, "new password 2");

            Assert.Null(device.Service.FirstSyncPassword);
        }

        [Fact]
        public async Task NewVault_IsCreatedInFormat2()
        {
            var device = NewDevice("a");

            Assert.Equal(CredentialLoadResult.Loaded, await device.Service.LoadCredentialsAsync(Password, allowCreate: true));

            Assert.Equal(2, FormatOf(device.VaultPath));
            Assert.Empty(device.Service.Backups);
            Assert.Equal(Password, device.Service.FirstSyncPassword);
        }

        [Fact]
        public async Task WrongPassword_DoesNotTouchTheFile()
        {
            var device = NewDevice("a");
            WriteFormat1(device.VaultPath, new PasswordVault(), Password);
            var bytes = File.ReadAllBytes(device.VaultPath);

            Assert.Equal(CredentialLoadResult.Failed, await device.Service.LoadCredentialsAsync("wrong password"));

            Assert.Equal(bytes, File.ReadAllBytes(device.VaultPath));
            Assert.Empty(device.Observer.Keys);
        }

        [Fact]
        public async Task NewerContentsVersion_IsReported()
        {
            var device = NewDevice("a");
            using (var stream = File.Create(device.VaultPath))
                _crypto.EncryptToStream(new PasswordVault { Version = PasswordVault.CurrentVersion + 1 }, stream, Password);

            Assert.Equal(CredentialLoadResult.NewerVersion, await device.Service.LoadCredentialsAsync(Password));
        }

        [Fact]
        public async Task SavingAChange_UsesTheVaultKey()
        {
            var device = NewDevice("a");
            await device.Service.LoadCredentialsAsync(Password, allowCreate: true);
            var header = File.ReadAllBytes(device.VaultPath)[..110];

            await device.Service.AddCredential(NewLogin("mail"));

            Assert.Equal(header, File.ReadAllBytes(device.VaultPath)[..110]);
            Assert.Equal("mail", Assert.Single(ReadWithPassword(device.VaultPath, Password).Credentials).NameField.Value);
        }

        [Fact]
        public async Task VaultKey_UnlocksWithoutThePassword()
        {
            var device = NewDevice("a");
            await device.Service.LoadCredentialsAsync(Password, allowCreate: true);
            await device.Service.AddCredential(NewLogin("mail"));
            var key = device.Service.CopyVaultKey();

            var restarted = Restart(device);
            Assert.Equal(CredentialLoadResult.Loaded, await restarted.Service.LoadCredentialsAsync(key));
            Assert.Equal("mail", Assert.Single(restarted.Service.Credentials).NameField.Value);
            Assert.Empty(restarted.Observer.Keys); // Windows Hello does not count as typing the password

            Assert.Equal(CredentialLoadResult.WrongKey, await Restart(device).Service.LoadCredentialsAsync(RandomNumberGenerator.GetBytes(32)));
        }

        [Fact]
        public async Task ChangingThePassword_ReplacesTheKey()
        {
            var device = NewDevice("a");
            await device.Service.LoadCredentialsAsync(Password, allowCreate: true);
            await device.Service.AddCredential(NewLogin("mail"));
            var oldKey = device.Service.CopyVaultKey();

            Assert.Equal(PasswordChangeResult.IncorrectCurrentPassword, await device.Service.ChangePasswordAsync("wrong password", "new password 2"));
            Assert.Equal(PasswordChangeResult.Changed, await device.Service.ChangePasswordAsync(Password, "new password 2"));

            Assert.NotEqual(oldKey, device.Service.CopyVaultKey());
            Assert.Equal(device.Service.CopyVaultKey(), device.Observer.Keys.Last());
            Assert.Equal("mail", Assert.Single(ReadWithPassword(device.VaultPath, "new password 2").Credentials).NameField.Value);
            Assert.ThrowsAny<CryptographicException>(() => ReadWithPassword(device.VaultPath, Password));
            Assert.Equal(CredentialLoadResult.WrongKey, await Restart(device).Service.LoadCredentialsAsync(oldKey));

            // The backup made before the change keeps the old key and password.
            var backup = device.Service.Backups.Single(b => b.Reason == VaultBackupReason.Password);
            Assert.Null(await device.Service.ReadBackup(backup.Path));
            Assert.NotNull(await device.Service.ReadBackup(backup.Path, Password));
        }

        [Fact]
        public async Task BackupWithTheCurrentKey_OpensWithoutPassword()
        {
            var device = NewDevice("a");
            await device.Service.LoadCredentialsAsync(Password, allowCreate: true);
            await device.Service.AddCredential(NewLogin("mail"));
            await device.Service.ChangePasswordAsync(Password, "new password 2"); // makes a backup with the old key
            await device.Service.AddCredential(NewLogin("bank"));
            await device.Service.ChangePasswordAsync("new password 2", "new password 3"); // backup with the second key

            var backups = device.Service.Backups.Where(b => b.Reason == VaultBackupReason.Password).ToList();
            Assert.Equal(2, backups.Count);
            Assert.All(backups, backup => Assert.Null(device.Service.ReadBackup(backup.Path).Result));

            await device.Service.AddCredential(NewLogin("shop"));
            File.Copy(device.VaultPath, Path.Combine(_root, "copy.dat"));
            Assert.Equal(3, (await device.Service.ReadBackup(Path.Combine(_root, "copy.dat"))).Credentials.Count);
        }

        [Fact]
        public async Task CloudWithTheSameKey_MergesWithoutPassword()
        {
            var a = NewDevice("a");
            await a.Service.LoadCredentialsAsync(Password, allowCreate: true);
            var b = NewDevice("b");
            File.Copy(a.VaultPath, b.VaultPath);
            await b.Service.LoadCredentialsAsync(Password);
            await a.Service.AddCredential(NewLogin("mail"));

            var cloud = File.ReadAllBytes(a.VaultPath);
            Assert.True(b.Service.TryReadWithCurrentKey(cloud, out var incoming, out var cloudKey));
            var result = new CredentialsMergeResult();
            using (cloudKey)
                await b.Service.MergeFromCloud(incoming, cloudKey, cloudPassword: null, result);

            Assert.Equal("mail", Assert.Single(b.Service.Credentials).NameField.Value);
            Assert.False(result.MasterPasswordAdopted);
            Assert.False(result.CloudReencrypted);
            Assert.False(result.CloudNeedsUpdate);
            Assert.Equal(KeyId(a.Service), KeyId(b.Service));
        }

        /// <summary>One sync of <paramref name="device"/> with a cloud holding <paramref name="cloud"/>; returns the cloud file after it.</summary>
        private async Task<(byte[] Cloud, bool PasswordAsked)> Sync(Device device, byte[] cloud)
        {
            var result = new CredentialsMergeResult();
            var passwordAsked = false;
            if (device.Service.TryReadWithCurrentKey(cloud, out var incoming, out var key))
            {
                using (key)
                    await device.Service.MergeFromCloud(incoming, key, null, result);
            }
            else
            {
                passwordAsked = true;
                var (vault, cloudKey) = ReadCloud(cloud, Password);
                using (cloudKey)
                    await device.Service.MergeFromCloud(vault, cloudKey, Password, result);
            }
            return (result.CloudNeedsUpdate ? File.ReadAllBytes(device.VaultPath) : cloud, passwordAsked);
        }

        [Fact]
        public async Task TwoCloudsWithDifferentKeys_SettleOnOneKey()
        {
            // Three devices converted their 1.x files independently: three keys. A uploaded to Google, C to Yandex.
            var devices = new[] { NewDevice("a"), NewDevice("b"), NewDevice("c") };
            foreach (var device in devices)
            {
                WriteFormat1(device.VaultPath, new PasswordVault { Credentials = [NewLogin(Path.GetFileName(Path.GetDirectoryName(device.VaultPath)))] }, Password);
                await device.Service.LoadCredentialsAsync(Password);
            }
            var lowest = devices.Select(device => KeyId(device.Service)).Min(Comparer<byte[]>.Create((x, y) => x.AsSpan().SequenceCompareTo(y)));
            var google = File.ReadAllBytes(devices[0].VaultPath);
            var yandex = File.ReadAllBytes(devices[2].VaultPath);

            // Every device syncs both clouds in turn, several rounds. With the cloud always winning a tie,
            // the key of a device syncing two clouds would flip on every sync.
            var passwordPrompts = 0;
            for (var round = 0; round < 4; round++)
            {
                foreach (var device in devices)
                {
                    bool asked;
                    (google, asked) = await Sync(device, google);
                    passwordPrompts += asked ? 1 : 0;
                    (yandex, asked) = await Sync(device, yandex);
                    passwordPrompts += asked ? 1 : 0;
                }
            }

            foreach (var device in devices)
            {
                Assert.Equal(lowest, KeyId(device.Service));
                Assert.Equal(["a", "b", "c"], device.Service.Credentials.Select(c => c.NameField.Value).Order());
                Assert.DoesNotContain(device.Service.Backups, backup => backup.Reason == VaultBackupReason.Password);
            }
            Assert.True(devices[0].Service.TryReadWithCurrentKey(google, out _, out var googleKey));
            Assert.True(devices[0].Service.TryReadWithCurrentKey(yandex, out _, out var yandexKey));
            googleKey.Dispose();
            yandexKey.Dispose();
            Assert.True(passwordPrompts <= 2 * devices.Length, $"{passwordPrompts} password prompts");
        }

        [Fact]
        public async Task DevicesConvertedIndependently_EndUpWithOneKey()
        {
            // Two devices of 1.x get their own random keys when each converts its file.
            var a = NewDevice("a");
            WriteFormat1(a.VaultPath, new PasswordVault { Credentials = [NewLogin("mail")] }, Password);
            await a.Service.LoadCredentialsAsync(Password);
            var b = NewDevice("b");
            WriteFormat1(b.VaultPath, new PasswordVault { Credentials = [NewLogin("bank")] }, Password);
            await b.Service.LoadCredentialsAsync(Password);
            Assert.NotEqual(KeyId(a.Service), KeyId(b.Service));

            // A uploaded first; B opens the cloud copy with the password. Neither changed the password: the lower key id wins.
            var lower = new[] { KeyId(a.Service), KeyId(b.Service) }.Min(Comparer<byte[]>.Create((x, y) => x.AsSpan().SequenceCompareTo(y)));
            var cloud = File.ReadAllBytes(a.VaultPath);
            Assert.False(b.Service.TryReadWithCurrentKey(cloud, out _, out _));
            var (incoming, cloudKey) = ReadCloud(cloud, Password);
            var result = new CredentialsMergeResult();
            using (cloudKey)
                await b.Service.MergeFromCloud(incoming, cloudKey, Password, result);

            Assert.Equal(lower, KeyId(b.Service));
            Assert.False(result.MasterPasswordAdopted); // same password, only the key differs
            Assert.False(result.CloudReencrypted);
            Assert.True(result.CloudNeedsUpdate);       // the cloud lacks "bank", and maybe B's key
            Assert.Equal(["bank", "mail"], ReadWithPassword(b.VaultPath, Password).Credentials.Select(c => c.NameField.Value).Order());
            Assert.DoesNotContain(b.Service.Backups, backup => backup.Reason == VaultBackupReason.Password);
        }

        [Fact]
        public async Task PasswordChangedOnThisDevice_ReplacesTheCloudKey()
        {
            var a = NewDevice("a");
            await a.Service.LoadCredentialsAsync(Password, allowCreate: true);
            var b = NewDevice("b");
            File.Copy(a.VaultPath, b.VaultPath);
            await b.Service.LoadCredentialsAsync(Password);
            await b.Service.ChangePasswordAsync(Password, "new password 2");
            var bKey = KeyId(b.Service);

            var (incoming, cloudKey) = ReadCloud(File.ReadAllBytes(a.VaultPath), Password);
            var result = new CredentialsMergeResult();
            using (cloudKey)
                await b.Service.MergeFromCloud(incoming, cloudKey, Password, result);

            Assert.Equal(bKey, KeyId(b.Service));
            Assert.False(result.MasterPasswordAdopted);
            Assert.True(result.CloudReencrypted);
            Assert.True(result.CloudNeedsUpdate);
        }

        [Fact]
        public async Task PasswordChangedOnAnotherDevice_IsAdopted()
        {
            var a = NewDevice("a");
            await a.Service.LoadCredentialsAsync(Password, allowCreate: true);
            var b = NewDevice("b");
            File.Copy(a.VaultPath, b.VaultPath);
            await b.Service.LoadCredentialsAsync(Password);
            await a.Service.ChangePasswordAsync(Password, "new password 2");

            var (incoming, cloudKey) = ReadCloud(File.ReadAllBytes(a.VaultPath), "new password 2");
            var result = new CredentialsMergeResult();
            using (cloudKey)
                await b.Service.MergeFromCloud(incoming, cloudKey, "new password 2", result);

            Assert.True(result.MasterPasswordAdopted);
            Assert.False(result.CloudReencrypted);
            Assert.Equal(KeyId(a.Service), KeyId(b.Service));
            Assert.Equal(b.Service.CopyVaultKey(), b.Observer.Keys.Last());
            ReadWithPassword(b.VaultPath, "new password 2");
            Assert.Contains(b.Service.Backups, backup => backup.Reason == VaultBackupReason.Password);
        }

        [Fact]
        public async Task CloudCopyOf1x_WithTheSamePassword_IsConvertedOnUpload()
        {
            var b = NewDevice("b");
            await b.Service.LoadCredentialsAsync(Password, allowCreate: true);
            var bKey = KeyId(b.Service);
            var cloudPath = Path.Combine(_root, "cloud.dat");
            WriteFormat1(cloudPath, new PasswordVault { Credentials = [NewLogin("mail")] }, Password);

            var (incoming, cloudKey) = ReadCloud(File.ReadAllBytes(cloudPath), Password);
            Assert.Null(cloudKey);
            var result = new CredentialsMergeResult();
            await b.Service.MergeFromCloud(incoming, null, Password, result);

            Assert.Equal(bKey, KeyId(b.Service));
            Assert.False(result.MasterPasswordAdopted);
            Assert.False(result.CloudReencrypted);
            Assert.True(result.CloudNeedsUpdate);
            Assert.Equal("mail", Assert.Single(b.Service.Credentials).NameField.Value);
        }

        [Fact]
        public async Task CloudCopyOf1x_WithANewerPassword_SwitchesThisDevice()
        {
            var b = NewDevice("b");
            await b.Service.LoadCredentialsAsync(Password, allowCreate: true);
            var cloudPath = Path.Combine(_root, "cloud.dat");
            WriteFormat1(cloudPath, new PasswordVault { MasterPasswordChangedAt = DateTimeOffset.UtcNow }, "new password 2");

            var (incoming, _) = ReadCloud(File.ReadAllBytes(cloudPath), "new password 2");
            var result = new CredentialsMergeResult();
            await b.Service.MergeFromCloud(incoming, null, "new password 2", result);

            Assert.True(result.MasterPasswordAdopted);
            Assert.True(result.CloudNeedsUpdate);
            ReadWithPassword(b.VaultPath, "new password 2");
        }
    }
}
