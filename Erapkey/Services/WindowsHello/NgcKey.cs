using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Erapkey.Services.WindowsHello
{
    /// <summary>
    /// An RSA key of the current user kept by Windows Hello (the "Microsoft Passport" key storage provider).
    /// The private key lives in the TPM, or in software where there is none, and every decryption asks for
    /// the user's Windows Hello gesture: PIN, fingerprint or face. Encrypting with the public key needs no gesture.
    /// </summary>
    internal static class NgcKey
    {
        private const string ProviderName = "Microsoft Passport Key Storage Provider";
        private const string RsaAlgorithm = "RSA";
        private const string RsaPublicBlob = "RSAPUBLICBLOB";

        private const string LengthProperty = "Length";
        private const string KeyUsageProperty = "Key Usage";
        private const string CacheTypeProperty = "NgcCacheType";
        private const string CacheTypePropertyDeprecated = "NgcCacheTypeProperty";
        private const string WindowHandleProperty = "HWND Handle";
        private const string UseContextProperty = "Use Context";

        private const int KeyLength = 2048;
        private const int AllowDecrypt = 0x1;
        private const int AllowSigning = 0x2;
        private const int CacheAuthMandatory = 0x1; // ask for the gesture on every use
        private const int OverwriteKey = 0x80;
        private const int Silent = 0x40; // NCRYPT_SILENT_FLAG: fail instead of showing UI
        private const int PadPkcs1 = 0x2;
        private const int RsaPublicMagic = 0x31415352; // "RSA1"

        internal const int UserCancelled = unchecked((int)0x80090036);   // NTE_USER_CANCELLED
        internal const int Cancelled = unchecked((int)0x800704C7);       // HRESULT_FROM_WIN32(ERROR_CANCELLED)
        internal const int NoKey = unchecked((int)0x8009000D);           // NTE_NO_KEY
        internal const int BadKeyset = unchecked((int)0x80090016);       // NTE_BAD_KEYSET
        internal const int NotFound = unchecked((int)0x80090011);        // NTE_NOT_FOUND

        /// <summary>Name of this application's key; Windows Hello keys are named "SID//domain/subdomain/name".</summary>
        internal static string KeyName { get; } = $"{WindowsIdentity.GetCurrent().User!.Value}//Erapkey/Unlock/MasterPassword";

        /// <summary>Whether the current user has set up Windows Hello (at least a PIN) on this computer.</summary>
        internal static bool IsAvailable()
        {
            try
            {
                var sid = WindowsIdentity.GetCurrent().User!.Value;
                return NgcGetDefaultDecryptionKeyName(sid, 0, 0, out var defaultKey) >= 0 && !string.IsNullOrEmpty(defaultKey);
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                return false;
            }
        }

        /// <summary>
        /// Creates the key, replacing an earlier one, and returns its public key (SubjectPublicKeyInfo).
        /// Windows shows the Windows Hello prompt over <paramref name="owner"/>.
        /// </summary>
        internal static byte[] Create(IntPtr owner, string prompt)
        {
            using var provider = OpenProvider();
            Check(NCryptCreatePersistedKey(provider, out var key, RsaAlgorithm, KeyName, 0, OverwriteKey));
            using (key)
            {
                SetInt(key, LengthProperty, KeyLength);
                SetInt(key, KeyUsageProperty, AllowDecrypt | AllowSigning);
                if (NCryptSetProperty(key, CacheTypeProperty, BitConverter.GetBytes(CacheAuthMandatory), sizeof(int), 0) < 0)
                    SetInt(key, CacheTypePropertyDeprecated, CacheAuthMandatory); // Windows 10 before 1903
                SetPromptContext(key, owner, prompt);
                Check(NCryptFinalizeKey(key, 0));
                return ExportPublicKey(key);
            }
        }

        /// <summary>Returns the public key (SubjectPublicKeyInfo) of the existing key without any prompt.</summary>
        internal static byte[] GetPublicKey()
        {
            using var provider = OpenProvider();
            Check(NCryptOpenKey(provider, out var key, KeyName, 0, Silent));
            using (key)
                return ExportPublicKey(key);
        }

        /// <summary>Decrypts PKCS#1 v1.5 data with the private key, which asks for the Windows Hello gesture.</summary>
        internal static byte[] Decrypt(byte[] data, IntPtr owner, string prompt)
        {
            using var provider = OpenProvider();
            Check(NCryptOpenKey(provider, out var key, KeyName, 0, 0));
            using (key)
            {
                SetPromptContext(key, owner, prompt);
                Check(NCryptDecrypt(key, data, data.Length, IntPtr.Zero, null, 0, out var size, PadPkcs1));
                var output = new byte[size];
                Check(NCryptDecrypt(key, data, data.Length, IntPtr.Zero, output, output.Length, out size, PadPkcs1));
                return output.AsSpan(0, size).ToArray();
            }
        }

        /// <summary>Deletes the key if it exists. Never throws.</summary>
        internal static void Delete()
        {
            try
            {
                using var provider = OpenProvider();
                if (NCryptOpenKey(provider, out var key, KeyName, 0, 0) < 0)
                {
                    key.Dispose();
                    return;
                }

                // NCryptDeleteKey frees the handle when it succeeds.
                if (NCryptDeleteKey(key, 0) >= 0)
                    key.SetHandleAsInvalid();
                key.Dispose();
            }
            catch (Exception ex) when (ex is Win32Exception or DllNotFoundException or EntryPointNotFoundException)
            {
            }
        }

        private static SafeNCryptProviderHandle OpenProvider()
        {
            Check(NCryptOpenStorageProvider(out var provider, ProviderName, 0));
            return provider;
        }

        private static void SetPromptContext(SafeNCryptKeyHandle key, IntPtr owner, string prompt)
        {
            if (owner != IntPtr.Zero)
            {
                var handle = IntPtr.Size == 8 ? BitConverter.GetBytes(owner.ToInt64()) : BitConverter.GetBytes(owner.ToInt32());
                Check(NCryptSetProperty(key, WindowHandleProperty, handle, handle.Length, 0));
            }

            var context = Encoding.Unicode.GetBytes(prompt + '\0');
            Check(NCryptSetProperty(key, UseContextProperty, context, context.Length, 0));
        }

        private static void SetInt(SafeNCryptKeyHandle key, string property, int value)
        {
            Check(NCryptSetProperty(key, property, BitConverter.GetBytes(value), sizeof(int), 0));
        }

        private static byte[] ExportPublicKey(SafeNCryptKeyHandle key)
        {
            Check(NCryptExportKey(key, IntPtr.Zero, RsaPublicBlob, IntPtr.Zero, null, 0, out var size, 0));
            var blob = new byte[size];
            Check(NCryptExportKey(key, IntPtr.Zero, RsaPublicBlob, IntPtr.Zero, blob, blob.Length, out size, 0));

            // BCRYPT_RSAKEY_BLOB: Magic, BitLength, cbPublicExp, cbModulus, cbPrime1, cbPrime2, then exponent and modulus (big-endian).
            var span = blob.AsSpan(0, size);
            if (BinaryPrimitives.ReadInt32LittleEndian(span) != RsaPublicMagic)
                throw new CryptographicException("Unexpected Windows Hello public key format.");
            var exponentSize = BinaryPrimitives.ReadInt32LittleEndian(span[8..]);
            var modulusSize = BinaryPrimitives.ReadInt32LittleEndian(span[12..]);
            const int headerSize = 24;
            using var rsa = RSA.Create(new RSAParameters
            {
                Exponent = span.Slice(headerSize, exponentSize).ToArray(),
                Modulus = span.Slice(headerSize + exponentSize, modulusSize).ToArray(),
            });
            return rsa.ExportSubjectPublicKeyInfo();
        }

        private static void Check(int status)
        {
            if (status < 0)
                throw new Win32Exception(status);
        }

        [DllImport("cryptngc.dll", CharSet = CharSet.Unicode)]
        private static extern int NgcGetDefaultDecryptionKeyName(string pszSid, int dwReserved1, int dwReserved2, out string ppszKeyName);

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        private static extern int NCryptOpenStorageProvider(out SafeNCryptProviderHandle phProvider, string pszProviderName, int dwFlags);

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        private static extern int NCryptOpenKey(SafeNCryptProviderHandle hProvider, out SafeNCryptKeyHandle phKey, string pszKeyName, int dwLegacyKeySpec, int dwFlags);

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        private static extern int NCryptCreatePersistedKey(SafeNCryptProviderHandle hProvider, out SafeNCryptKeyHandle phKey, string pszAlgId, string pszKeyName, int dwLegacyKeySpec, int dwFlags);

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        private static extern int NCryptSetProperty(SafeNCryptHandle hObject, string pszProperty, byte[] pbInput, int cbInput, int dwFlags);

        [DllImport("ncrypt.dll")]
        private static extern int NCryptFinalizeKey(SafeNCryptKeyHandle hKey, int dwFlags);

        [DllImport("ncrypt.dll")]
        private static extern int NCryptDecrypt(SafeNCryptKeyHandle hKey, byte[] pbInput, int cbInput, IntPtr pPaddingInfo, byte[] pbOutput, int cbOutput, out int pcbResult, int dwFlags);

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        private static extern int NCryptExportKey(SafeNCryptKeyHandle hKey, IntPtr hExportKey, string pszBlobType, IntPtr pParameterList, byte[] pbOutput, int cbOutput, out int pcbResult, int dwFlags);

        [DllImport("ncrypt.dll")]
        private static extern int NCryptDeleteKey(SafeNCryptKeyHandle hKey, int dwFlags);
    }
}
