using Microsoft.Extensions.Logging;
using SinglePass.Core.Helpers;
using SinglePass.Core.Localization;
using SinglePass.Core.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    /// Unlocks the vault with Windows Hello instead of the master password.
    /// <para>
    /// The master password is kept in <c>windowsHello.dat</c>, encrypted with AES-256-GCM under a random key, which in turn
    /// is encrypted with the public key of a Windows Hello key (<see cref="NgcKey"/>). Only that key can decrypt it, and only
    /// after the user's PIN, fingerprint or face, on this computer. Re-encrypting with the public key needs no prompt, so
    /// the copy follows password changes silently.
    /// </para>
    /// <para>
    /// The master password is still required every <see cref="MasterPasswordInterval"/> so that the user keeps remembering it;
    /// the time it was last entered is authenticated together with the encrypted password.
    /// </para>
    /// </summary>
    public sealed class WindowsHelloUnlock : IMasterPasswordObserver
    {
        public static readonly TimeSpan MasterPasswordInterval = TimeSpan.FromDays(14);

        private const int FormatVersion = 1;
        private const int KeySize = 32;
        private const int NonceSize = 12;
        private const int TagSize = 16;

        internal static string FilePath { get; } = Path.Combine(Constants.LocalAppDataDirectoryPath, "windowsHello.dat");

        private readonly ILogger<WindowsHelloUnlock> _logger;
        private readonly object _fileLock = new();

        public WindowsHelloUnlock(ILogger<WindowsHelloUnlock> logger)
        {
            _logger = logger;
        }

        /// <summary>Whether the user has set up Windows Hello on this computer.</summary>
        public static bool IsAvailable => NgcKey.IsAvailable();

        /// <summary>Whether Windows Hello unlock is turned on for this vault.</summary>
        public static bool IsEnabled => File.Exists(FilePath);

        /// <summary>Whether the unlock window should offer Windows Hello instead of the master password right now.</summary>
        public static bool IsOffered()
        {
            try
            {
                var stored = Read();
                return stored is not null && DateTimeOffset.UtcNow - stored.PasswordEnteredAt < MasterPasswordInterval && IsAvailable;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Creates the Windows Hello key, which shows the Windows Hello prompt, and stores <paramref name="password"/>.
        /// Returns false when the user cancelled the prompt.
        /// </summary>
        public Task<bool> EnableAsync(string password, IntPtr owner) => Task.Run(() =>
        {
            byte[] publicKey;
            try
            {
                publicKey = NgcKey.Create(owner, Prompt);
            }
            catch (Win32Exception ex) when (IsCancellation(ex))
            {
                return false;
            }

            Store(publicKey, password);
            _logger.LogInformation("Windows Hello unlock was turned on");
            return true;
        });

        /// <summary>Turns Windows Hello unlock off: forgets the stored password and deletes the key.</summary>
        public void Disable()
        {
            lock (_fileLock)
            {
                try
                {
                    File.Delete(FilePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogError(ex, "Failed to delete the Windows Hello unlock data");
                }
            }

            NgcKey.Delete();
            _logger.LogInformation("Windows Hello unlock was turned off");
        }

        /// <summary>
        /// Updates the stored password after the user typed the master password, which also restarts
        /// <see cref="MasterPasswordInterval"/>. Does nothing while Windows Hello unlock is off.
        /// </summary>
        public void Remember(string password)
        {
            try
            {
                var stored = Read();
                if (stored is not null)
                    Store(stored.PublicKey, password);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or CryptographicException)
            {
                _logger.LogError(ex, "Failed to update the Windows Hello unlock data");
            }
        }

        void IMasterPasswordObserver.OnMasterPasswordChanged(string password) => Remember(password);

        /// <summary>Asks for the Windows Hello gesture over <paramref name="owner"/> and returns the master password.</summary>
        internal Task<(WindowsHelloOutcome Outcome, string Password)> UnlockAsync(IntPtr owner) => Task.Run(() =>
        {
            StoredPassword stored;
            try
            {
                stored = Read();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogError(ex, "Failed to read the Windows Hello unlock data");
                return (WindowsHelloOutcome.Failed, null);
            }

            if (stored is null)
                return (WindowsHelloOutcome.Failed, null);

            byte[] key = null;
            byte[] plaintext = null;
            try
            {
                key = NgcKey.Decrypt(stored.WrappedKey, owner, Prompt);
                if (key.Length != KeySize)
                    throw new CryptographicException("Unexpected Windows Hello unlock key size.");

                plaintext = new byte[stored.Ciphertext.Length];
                using (var aes = new AesGcm(key, TagSize))
                    aes.Decrypt(stored.Nonce, stored.Ciphertext, stored.Tag, plaintext, AssociatedData(stored.PasswordEnteredAt));
                return (WindowsHelloOutcome.Unlocked, Encoding.UTF8.GetString(plaintext));
            }
            catch (Win32Exception ex) when (IsCancellation(ex))
            {
                return (WindowsHelloOutcome.Cancelled, null);
            }
            catch (Win32Exception ex) when (IsKeyLost(ex))
            {
                _logger.LogWarning(ex, "The Windows Hello key is no longer usable; Windows Hello unlock is turned off");
                Disable();
                return (WindowsHelloOutcome.KeyLost, null);
            }
            catch (Exception ex) when (ex is Win32Exception or CryptographicException)
            {
                _logger.LogError(ex, "Windows Hello unlock failed");
                return (WindowsHelloOutcome.Failed, null);
            }
            finally
            {
                if (key is not null)
                    CryptographicOperations.ZeroMemory(key);
                if (plaintext is not null)
                    CryptographicOperations.ZeroMemory(plaintext);
            }
        });

        private static string Prompt => LocalizationManager.Instance["WindowsHelloPrompt"];

        private void Store(byte[] publicKey, string password)
        {
            var enteredAt = DateTimeOffset.UtcNow;
            var key = RandomNumberGenerator.GetBytes(KeySize);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var plaintext = Encoding.UTF8.GetBytes(password);
            try
            {
                var ciphertext = new byte[plaintext.Length];
                var tag = new byte[TagSize];
                using (var aes = new AesGcm(key, TagSize))
                    aes.Encrypt(nonce, plaintext, ciphertext, tag, AssociatedData(enteredAt));

                byte[] wrappedKey;
                using (var rsa = RSA.Create())
                {
                    rsa.ImportSubjectPublicKeyInfo(publicKey, out _);
                    wrappedKey = rsa.Encrypt(key, RSAEncryptionPadding.Pkcs1);
                }

                Write(new StoredPassword(FormatVersion, publicKey, wrappedKey, nonce, tag, ciphertext, enteredAt));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        private static byte[] AssociatedData(DateTimeOffset enteredAt)
        {
            var data = new byte[sizeof(int) + sizeof(long)];
            BitConverter.TryWriteBytes(data, FormatVersion);
            BitConverter.TryWriteBytes(data.AsSpan(sizeof(int)), enteredAt.UtcTicks);
            return data;
        }

        private static StoredPassword Read()
        {
            if (!File.Exists(FilePath))
                return null;

            var stored = JsonSerializer.Deserialize<StoredPassword>(File.ReadAllBytes(FilePath));
            return stored is { Version: FormatVersion, PublicKey: not null, WrappedKey: not null, Nonce.Length: NonceSize, Tag.Length: TagSize, Ciphertext: not null }
                ? stored
                : throw new JsonException("Unsupported Windows Hello unlock data.");
        }

        private void Write(StoredPassword stored)
        {
            lock (_fileLock)
            {
                var tempPath = $"{FilePath}.{Guid.NewGuid():N}.tmp";
                try
                {
                    File.WriteAllBytes(tempPath, JsonSerializer.SerializeToUtf8Bytes(stored));
                    File.Move(tempPath, FilePath, overwrite: true);
                }
                catch
                {
                    try { File.Delete(tempPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
        }

        private static bool IsCancellation(Win32Exception ex) =>
            ex.NativeErrorCode is NgcKey.UserCancelled or NgcKey.Cancelled;

        // A missing key, or a TPM key that no longer matches the user's Windows Hello container (TPM_20_E_* errors).
        private static bool IsKeyLost(Win32Exception ex) =>
            ex.NativeErrorCode is NgcKey.NoKey or NgcKey.BadKeyset or NgcKey.NotFound ||
            (ex.NativeErrorCode & unchecked((int)0xFFFF0000)) == unchecked((int)0x80280000);

        private sealed record StoredPassword(
            int Version, byte[] PublicKey, byte[] WrappedKey, byte[] Nonce, byte[] Tag, byte[] Ciphertext, DateTimeOffset PasswordEnteredAt);
    }
}
