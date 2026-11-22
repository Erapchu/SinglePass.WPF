using Microsoft.Extensions.Logging;
using SinglePass.Core.Localization;
using SinglePass.Core.Services;
using SinglePass.WPF.Settings;
using System;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
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
    /// Nothing is written to disk. After the user types the master password, it is kept in memory only in encrypted form:
    /// AES-256-GCM under a random key, which is encrypted with the public key of a Windows Hello key (<see cref="NgcKey"/>).
    /// Only that key can decrypt it, and only after the user's PIN, fingerprint or face. Encrypting with the public key
    /// needs no prompt, so the copy follows password changes silently.
    /// </para>
    /// <para>
    /// The copy lives as long as the process: after SinglePass or Windows restarts, the master password is required again.
    /// It is also required <see cref="MasterPasswordInterval"/> after it was last typed, so that the user keeps remembering it
    /// even when SinglePass runs for weeks.
    /// </para>
    /// </summary>
    public sealed class WindowsHelloUnlock : IMasterPasswordObserver
    {
        public static readonly TimeSpan MasterPasswordInterval = TimeSpan.FromDays(14);

        private const int KeySize = 32;
        private const int NonceSize = 12;
        private const int TagSize = 16;

        private readonly AppSettingsService _settings;
        private readonly ILogger<WindowsHelloUnlock> _logger;
        private readonly object _sync = new();
        private SealedPassword _sealed;

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
                SealedPassword current;
                lock (_sync)
                    current = _sealed;
                return IsEnabled && current is not null && DateTimeOffset.UtcNow - current.EnteredAt < MasterPasswordInterval;
            }
        }

        /// <summary>
        /// Creates the Windows Hello key, which shows the Windows Hello prompt over <paramref name="owner"/>,
        /// keeps <paramref name="password"/> for the next unlock and saves the setting.
        /// Returns false when the user cancelled the prompt.
        /// </summary>
        public async Task<bool> EnableAsync(string password, IntPtr owner)
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

            Seal(publicKey, password);
            _settings.Settings.WindowsHelloEnabled = true;
            await _settings.Save();
            _logger.LogInformation("Windows Hello unlock was turned on");
            return true;
        }

        /// <summary>Turns Windows Hello unlock off: forgets the kept password, deletes the key and saves the setting.</summary>
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
        /// Keeps the master password the user just typed for the next unlock and restarts <see cref="MasterPasswordInterval"/>.
        /// Does nothing while Windows Hello unlock is off.
        /// </summary>
        public void Remember(string password)
        {
            if (!IsEnabled)
                return;

            try
            {
                Seal(NgcKey.GetPublicKey(), password);
            }
            catch (Exception ex) when (ex is Win32Exception or CryptographicException)
            {
                // Without the key the vault reopens with the master password; unlocking with Windows Hello reports why.
                lock (_sync)
                    _sealed = null;
                _logger.LogError(ex, "Failed to keep the master password for Windows Hello unlock");
            }
        }

        // A password change or a password adopted by sync: the user typed it on this device.
        void IMasterPasswordObserver.OnMasterPasswordChanged(string password) => Remember(password);

        /// <summary>Asks for the Windows Hello gesture over <paramref name="owner"/> and returns the master password.</summary>
        internal async Task<(WindowsHelloOutcome Outcome, string Password)> UnlockAsync(IntPtr owner)
        {
            SealedPassword current;
            lock (_sync)
                current = _sealed;
            if (current is null)
                return (WindowsHelloOutcome.Failed, null);

            try
            {
                var password = await Task.Run(() => Unseal(current, owner));
                return (WindowsHelloOutcome.Unlocked, password);
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

        private void Seal(byte[] publicKey, string password)
        {
            var key = RandomNumberGenerator.GetBytes(KeySize);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var plaintext = Encoding.UTF8.GetBytes(password);
            try
            {
                var ciphertext = new byte[plaintext.Length];
                var tag = new byte[TagSize];
                using (var aes = new AesGcm(key, TagSize))
                    aes.Encrypt(nonce, plaintext, ciphertext, tag);

                byte[] wrappedKey;
                using (var rsa = RSA.Create())
                {
                    rsa.ImportSubjectPublicKeyInfo(publicKey, out _);
                    wrappedKey = rsa.Encrypt(key, RSAEncryptionPadding.Pkcs1);
                }

                lock (_sync)
                    _sealed = new SealedPassword(wrappedKey, nonce, tag, ciphertext, DateTimeOffset.UtcNow);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        private static string Unseal(SealedPassword sealedPassword, IntPtr owner)
        {
            byte[] key = null;
            var plaintext = new byte[sealedPassword.Ciphertext.Length];
            try
            {
                key = NgcKey.Decrypt(sealedPassword.WrappedKey, owner, Prompt);
                if (key.Length != KeySize)
                    throw new CryptographicException("Unexpected Windows Hello unlock key size.");

                using var aes = new AesGcm(key, TagSize);
                aes.Decrypt(sealedPassword.Nonce, sealedPassword.Ciphertext, sealedPassword.Tag, plaintext);
                return Encoding.UTF8.GetString(plaintext);
            }
            finally
            {
                if (key is not null)
                    CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        private static bool IsCancellation(Win32Exception ex) =>
            ex.NativeErrorCode is NgcKey.UserCancelled or NgcKey.Cancelled;

        // A missing key, or a TPM key that no longer matches the user's Windows Hello container (TPM_20_E_* errors).
        private static bool IsKeyLost(Win32Exception ex) =>
            ex.NativeErrorCode is NgcKey.NoKey or NgcKey.BadKeyset or NgcKey.NotFound ||
            (ex.NativeErrorCode & unchecked((int)0xFFFF0000)) == unchecked((int)0x80280000);

        private sealed record SealedPassword(byte[] WrappedKey, byte[] Nonce, byte[] Tag, byte[] Ciphertext, DateTimeOffset EnteredAt);
    }
}
