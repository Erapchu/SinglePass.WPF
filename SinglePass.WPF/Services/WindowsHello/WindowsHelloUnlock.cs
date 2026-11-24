using Microsoft.Extensions.Logging;
using SinglePass.Core.Helpers;
using SinglePass.Core.Localization;
using SinglePass.Core.Services;
using SinglePass.WPF.Settings;
using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SinglePass.WPF.Services.WindowsHello
{
    internal enum WindowsHelloOutcome
    {
        Unlocked,
        Cancelled,
        /// <summary>The Windows Hello key is gone (for example, the PIN was reset), so unlock was turned off.</summary>
        KeyLost,
        Failed,
    }

    /// <summary>
    /// Opens the vault with Windows Hello instead of the master password, also when SinglePass starts.
    /// <para>
    /// After the user types the master password, the vault key (see <see cref="VaultKey"/>) is written to
    /// <c>windowsHello.dat</c> encrypted with the public key of a Windows Hello key (<see cref="NgcKey"/>). Only that key can
    /// decrypt it: on this computer, for this Windows user, and only after the user's PIN, fingerprint or face. The master
    /// password itself is not stored. Encrypting with the public key needs no prompt, so the file follows key changes
    /// (password change, key adopted by sync) silently.
    /// </para>
    /// <para>
    /// The master password is still required <see cref="MasterPasswordInterval"/> after it was last typed, so that the user
    /// keeps remembering it. The time it was typed is encrypted together with the key, so editing the file cannot extend it.
    /// </para>
    /// </summary>
    public sealed class WindowsHelloUnlock : IVaultKeyObserver
    {
        public static readonly TimeSpan MasterPasswordInterval = TimeSpan.FromDays(14);

        internal static string FilePath { get; } = Path.Combine(Constants.LocalAppDataDirectoryPath, "windowsHello.dat");

        // windowsHello.dat: magic (7) | version (1) | entered at, UTC ticks (8, LE) | RSA-PKCS1(vault key (32) | entered at (8)).
        private static ReadOnlySpan<byte> Magic => "SPHELLO"u8;
        private const byte FormatVersion = 1;
        private const int HeaderSize = 16;
        private const int VaultKeySize = 32;
        private const int SealedSize = VaultKeySize + sizeof(long);
        private const int MinWrappedSize = 256; // RSA-2048
        private const int MaxWrappedSize = 512; // RSA-4096
        // A clock set back a little must not hide the Windows Hello button.
        private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

        private readonly AppSettingsService _settings;
        private readonly ILogger<WindowsHelloUnlock> _logger;
        private static readonly object FileLock = new();

        public WindowsHelloUnlock(AppSettingsService settings, ILogger<WindowsHelloUnlock> logger)
        {
            _settings = settings;
            _logger = logger;
        }

        /// <summary>Whether the user has set up Windows Hello (at least a PIN) on this computer.</summary>
        public static bool IsAvailable => NgcKey.IsAvailable();

        /// <summary>Whether Windows Hello unlock is turned on in Settings.</summary>
        public bool IsEnabled => _settings.Settings.WindowsHelloEnabled;

        /// <summary>Whether the unlock window should offer Windows Hello instead of the master password right now.</summary>
        public bool IsOffered => IsOfferedFor(_settings.Settings);

        /// <summary>
        /// <see cref="IsOffered"/> before the services exist (the first unlock window): Windows Hello unlock is turned on in
        /// <paramref name="settings"/> and <c>windowsHello.dat</c> holds a key typed less than <see cref="MasterPasswordInterval"/> ago.
        /// </summary>
        internal static bool IsOfferedFor(AppSettings settings)
        {
            if (!settings.WindowsHelloEnabled)
                return false;

            try
            {
                var stored = Read();
                if (stored is null)
                    return false;
                var age = DateTimeOffset.UtcNow - stored.EnteredAt;
                return age > -ClockSkew && age < MasterPasswordInterval;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// Creates the Windows Hello key, which shows the Windows Hello prompt over <paramref name="owner"/>,
        /// stores the vault key for the next unlock and saves the setting. <paramref name="copyVaultKey"/> is called after
        /// the prompt, so a key that changed meanwhile (a sync, a password change) is not missed; it returns a copy that
        /// is zeroed here, or null when the vault was locked meanwhile. Returns false when the user cancelled the prompt.
        /// </summary>
        public async Task<bool> EnableAsync(Func<byte[]> copyVaultKey, IntPtr owner)
        {
            byte[] publicKey;
            try
            {
                publicKey = await Task.Run(() => NgcKey.Create(owner, Prompt));
            }
            catch (Win32Exception ex) when (IsCancellation(ex))
            {
                return false;
            }

            var vaultKey = copyVaultKey();
            if (vaultKey is null)
            {
                NgcKey.Delete();
                return false;
            }

            try
            {
                await Task.Run(() => Store(publicKey, vaultKey));
            }
            catch
            {
                NgcKey.Delete();
                throw;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(vaultKey);
            }
            _settings.Settings.WindowsHelloEnabled = true;
            await _settings.Save();
            _logger.LogInformation("Windows Hello unlock was turned on");
            return true;
        }

        /// <summary>Turns Windows Hello unlock off: deletes <c>windowsHello.dat</c> and the Windows Hello key, saves the setting.</summary>
        public async Task DisableAsync()
        {
            await Task.Run(() =>
            {
                DeleteFile();
                NgcKey.Delete();
            });
            _settings.Settings.WindowsHelloEnabled = false;
            await _settings.Save();
            _logger.LogInformation("Windows Hello unlock was turned off");
        }

        /// <summary>
        /// Stores the vault key after the user typed the master password (or the key changed), which restarts
        /// <see cref="MasterPasswordInterval"/>. Does nothing while Windows Hello unlock is off.
        /// </summary>
        void IVaultKeyObserver.OnMasterPasswordEntered(ReadOnlySpan<byte> vaultKey)
        {
            if (!IsEnabled)
                return;

            try
            {
                Store(NgcKey.GetPublicKey(), vaultKey);
            }
            catch (Exception ex) when (ex is Win32Exception or CryptographicException or IOException or UnauthorizedAccessException)
            {
                // A file with an older key would not open the vault: remove it, the master password works as before.
                DeleteFile();
                _logger.LogError(ex, "Failed to store the vault key for Windows Hello unlock");
            }
        }

        /// <summary>
        /// Asks for the Windows Hello gesture over <paramref name="owner"/> and returns the vault key. The caller zeroes it.
        /// </summary>
        internal async Task<(WindowsHelloOutcome Outcome, byte[] VaultKey)> UnlockAsync(IntPtr owner)
        {
            StoredKey stored;
            try
            {
                stored = Read();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Failed to read {Path}", FilePath);
                return (WindowsHelloOutcome.Failed, null);
            }
            if (stored is null)
                return (WindowsHelloOutcome.Failed, null);

            try
            {
                var key = await Task.Run(() => Unseal(stored, owner));
                return (WindowsHelloOutcome.Unlocked, key);
            }
            catch (Win32Exception ex) when (IsCancellation(ex))
            {
                return (WindowsHelloOutcome.Cancelled, null);
            }
            catch (Win32Exception ex) when (IsKeyLost(ex))
            {
                _logger.LogWarning(ex, "The Windows Hello key is no longer usable; Windows Hello unlock is turned off");
                await DisableAsync();
                return (WindowsHelloOutcome.KeyLost, null);
            }
            catch (Exception ex) when (ex is Win32Exception or CryptographicException)
            {
                _logger.LogError(ex, "Windows Hello unlock failed");
                return (WindowsHelloOutcome.Failed, null);
            }
        }

        /// <summary>
        /// Deletes <c>windowsHello.dat</c> when its key no longer opens the vault (the vault file was replaced), so that
        /// Windows Hello is not offered until the master password is typed again. Windows Hello unlock stays on.
        /// </summary>
        internal void ForgetStoredKey() => DeleteFile();

        private static string Prompt => LocalizationManager.Instance["WindowsHelloPrompt"];

        private static void Store(byte[] publicKey, ReadOnlySpan<byte> vaultKey)
        {
            if (vaultKey.Length != VaultKeySize)
                throw new CryptographicException("Unexpected vault key size.");

            var enteredAt = DateTimeOffset.UtcNow.UtcTicks;
            var plain = new byte[SealedSize];
            byte[] wrapped;
            try
            {
                vaultKey.CopyTo(plain);
                BinaryPrimitives.WriteInt64LittleEndian(plain.AsSpan(VaultKeySize), enteredAt);
                // 40 bytes fit into one RSA block, so they are encrypted with the Windows Hello key directly.
                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(publicKey, out _);
                wrapped = rsa.Encrypt(plain, RSAEncryptionPadding.Pkcs1);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plain);
            }

            var file = new byte[HeaderSize + wrapped.Length];
            Magic.CopyTo(file);
            file[Magic.Length] = FormatVersion;
            BinaryPrimitives.WriteInt64LittleEndian(file.AsSpan(8), enteredAt);
            wrapped.CopyTo(file, HeaderSize);
            Write(file);
        }

        private static byte[] Unseal(StoredKey stored, IntPtr owner)
        {
            var plain = NgcKey.Decrypt(stored.WrappedKey, owner, Prompt);
            try
            {
                // The time in the header must be the one sealed with the key: the file cannot be edited to skip the password.
                if (plain.Length != SealedSize ||
                    BinaryPrimitives.ReadInt64LittleEndian(plain.AsSpan(VaultKeySize)) != stored.EnteredAt.UtcTicks)
                    throw new CryptographicException("windowsHello.dat was modified.");
                return plain.AsSpan(0, VaultKeySize).ToArray();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plain);
            }
        }

        /// <summary>Reads <c>windowsHello.dat</c>; null when there is none or it is not a file of this version.</summary>
        private static StoredKey Read()
        {
            byte[] file;
            lock (FileLock)
            {
                if (!File.Exists(FilePath))
                    return null;
                file = File.ReadAllBytes(FilePath);
            }

            var wrappedSize = file.Length - HeaderSize;
            if (wrappedSize is < MinWrappedSize or > MaxWrappedSize ||
                !file.AsSpan(0, Magic.Length).SequenceEqual(Magic) || file[Magic.Length] != FormatVersion)
                return null;

            var ticks = BinaryPrimitives.ReadInt64LittleEndian(file.AsSpan(8));
            if (ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
                return null;
            return new StoredKey(file.AsSpan(HeaderSize).ToArray(), new DateTimeOffset(ticks, TimeSpan.Zero));
        }

        private static void Write(byte[] file)
        {
            lock (FileLock)
            {
                var tempPath = $"{FilePath}.{Guid.NewGuid():N}.tmp";
                try
                {
                    File.WriteAllBytes(tempPath, file);
                    File.Move(tempPath, FilePath, overwrite: true);
                }
                catch
                {
                    try { File.Delete(tempPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
        }

        private void DeleteFile()
        {
            lock (FileLock)
            {
                try
                {
                    File.Delete(FilePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogError(ex, "Failed to delete {Path}", FilePath);
                }
            }
        }

        private static bool IsCancellation(Win32Exception ex) =>
            ex.NativeErrorCode is NgcKey.UserCancelled or NgcKey.Cancelled;

        // A missing key, or a TPM key that no longer matches the user's Windows Hello container (TPM_20_E_* errors).
        private static bool IsKeyLost(Win32Exception ex) =>
            ex.NativeErrorCode is NgcKey.NoKey or NgcKey.BadKeyset or NgcKey.NotFound ||
            (ex.NativeErrorCode & unchecked((int)0xFFFF0000)) == unchecked((int)0x80280000);

        private sealed record StoredKey(byte[] WrappedKey, DateTimeOffset EnteredAt);
    }
}
