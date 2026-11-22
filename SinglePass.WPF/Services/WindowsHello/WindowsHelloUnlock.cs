using Microsoft.Extensions.Logging;
using SinglePass.Core.Localization;
using SinglePass.Core.Services;
using SinglePass.WPF.Settings;
using System;
using System.ComponentModel;
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
    /// Reopens a locked vault with Windows Hello instead of the master password.
    /// <para>
    /// Nothing is written to disk and the master password is not kept. After the user types the master password,
    /// the vault key (see <see cref="VaultKey"/>) is kept in memory only encrypted with the public key of a Windows Hello
    /// key (<see cref="NgcKey"/>). Only that key can decrypt it, and only after the user's PIN, fingerprint or face.
    /// Encrypting with the public key needs no prompt, so the copy follows key changes silently.
    /// </para>
    /// <para>
    /// The copy lives as long as the process: after SinglePass or Windows restarts, the master password is required again.
    /// It is also required <see cref="MasterPasswordInterval"/> after it was last typed, so that the user keeps remembering it
    /// even when SinglePass runs for weeks.
    /// </para>
    /// </summary>
    public sealed class WindowsHelloUnlock : IVaultKeyObserver
    {
        public static readonly TimeSpan MasterPasswordInterval = TimeSpan.FromDays(14);

        private const int VaultKeySize = 32;

        private readonly AppSettingsService _settings;
        private readonly ILogger<WindowsHelloUnlock> _logger;
        private readonly object _sync = new();
        private SealedKey _sealed;

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
        public bool IsOffered
        {
            get
            {
                SealedKey current;
                lock (_sync)
                    current = _sealed;
                return IsEnabled && current is not null && DateTimeOffset.UtcNow - current.EnteredAt < MasterPasswordInterval;
            }
        }

        /// <summary>
        /// Creates the Windows Hello key, which shows the Windows Hello prompt over <paramref name="owner"/>,
        /// keeps the vault key for the next unlock and saves the setting. <paramref name="copyVaultKey"/> is called after
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
                Seal(publicKey, vaultKey);
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

        /// <summary>Turns Windows Hello unlock off: forgets the kept key, deletes the Windows Hello key and saves the setting.</summary>
        public async Task DisableAsync()
        {
            lock (_sync)
                _sealed = null;
            await Task.Run(NgcKey.Delete);
            _settings.Settings.WindowsHelloEnabled = false;
            await _settings.Save();
            _logger.LogInformation("Windows Hello unlock was turned off");
        }

        /// <summary>
        /// Keeps the vault key after the user typed the master password, which restarts <see cref="MasterPasswordInterval"/>.
        /// Does nothing while Windows Hello unlock is off.
        /// </summary>
        void IVaultKeyObserver.OnMasterPasswordEntered(ReadOnlySpan<byte> vaultKey)
        {
            if (!IsEnabled)
                return;

            try
            {
                Seal(NgcKey.GetPublicKey(), vaultKey);
            }
            catch (Exception ex) when (ex is Win32Exception or CryptographicException)
            {
                // Without the key the vault reopens with the master password; unlocking with Windows Hello reports why.
                lock (_sync)
                    _sealed = null;
                _logger.LogError(ex, "Failed to keep the vault key for Windows Hello unlock");
            }
        }

        /// <summary>
        /// Asks for the Windows Hello gesture over <paramref name="owner"/> and returns the vault key. The caller zeroes it.
        /// </summary>
        internal async Task<(WindowsHelloOutcome Outcome, byte[] VaultKey)> UnlockAsync(IntPtr owner)
        {
            SealedKey current;
            lock (_sync)
                current = _sealed;
            if (current is null)
                return (WindowsHelloOutcome.Failed, null);

            try
            {
                var key = await Task.Run(() => NgcKey.Decrypt(current.WrappedKey, owner, Prompt));
                if (key.Length != VaultKeySize)
                {
                    CryptographicOperations.ZeroMemory(key);
                    throw new CryptographicException("Unexpected vault key size.");
                }
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

        private static string Prompt => LocalizationManager.Instance["WindowsHelloPrompt"];

        private void Seal(byte[] publicKey, ReadOnlySpan<byte> vaultKey)
        {
            if (vaultKey.Length != VaultKeySize)
                throw new CryptographicException("Unexpected vault key size.");

            // A 32-byte key fits into one RSA-2048 block, so it is encrypted with the Windows Hello key directly.
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            var plain = vaultKey.ToArray();
            byte[] wrappedKey;
            try
            {
                wrappedKey = rsa.Encrypt(plain, RSAEncryptionPadding.Pkcs1);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plain);
            }
            lock (_sync)
                _sealed = new SealedKey(wrappedKey, DateTimeOffset.UtcNow);
        }

        private static bool IsCancellation(Win32Exception ex) =>
            ex.NativeErrorCode is NgcKey.UserCancelled or NgcKey.Cancelled;

        // A missing key, or a TPM key that no longer matches the user's Windows Hello container (TPM_20_E_* errors).
        private static bool IsKeyLost(Win32Exception ex) =>
            ex.NativeErrorCode is NgcKey.NoKey or NgcKey.BadKeyset or NgcKey.NotFound ||
            (ex.NativeErrorCode & unchecked((int)0xFFFF0000)) == unchecked((int)0x80280000);

        private sealed record SealedKey(byte[] WrappedKey, DateTimeOffset EnteredAt);
    }
}
