using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace SinglePass.Core.Services.Browser
{
    /// <summary>
    /// Key agreement and message encryption between SinglePass and the extension (docs/browser-extension.md#session).
    /// Everything here has a WebCrypto counterpart: ECDH P-256 with SPKI public keys, HKDF-SHA256, AES-256-GCM.
    /// </summary>
    internal static class BrowserCrypto
    {
        public const int KeySize = 32;
        public const int NonceSize = 16;
        public const int TagSize = 16;
        private static readonly byte[] PairingLabel = Encoding.ASCII.GetBytes("singlepass-pair");
        private static readonly byte[] SessionInfo = Encoding.ASCII.GetBytes("singlepass-browser-v1");

        public static ECDiffieHellman CreateKey() => ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        /// <summary>Reads a P-256 public key in SPKI form; throws <see cref="CryptographicException"/> for anything else.</summary>
        public static ECDiffieHellman ImportPublicKey(byte[] spki)
        {
            var key = ECDiffieHellman.Create();
            try
            {
                key.ImportSubjectPublicKeyInfo(spki, out var read);
                if (read != spki.Length || key.KeySize != 256)
                    throw new CryptographicException("Not a P-256 public key.");
                return key;
            }
            catch
            {
                key.Dispose();
                throw;
            }
        }

        /// <summary>
        /// The six digits both sides show during pairing: the first 4 bytes of
        /// SHA-256("singlepass-pair" || appPublic || extensionPublic), big-endian, modulo 1 000 000.
        /// </summary>
        public static string PairingCode(byte[] appPublicKey, byte[] extensionPublicKey)
        {
            var input = new byte[PairingLabel.Length + appPublicKey.Length + extensionPublicKey.Length];
            PairingLabel.CopyTo(input, 0);
            appPublicKey.CopyTo(input, PairingLabel.Length);
            extensionPublicKey.CopyTo(input, PairingLabel.Length + appPublicKey.Length);
            var hash = SHA256.HashData(input);
            var value = BinaryPrimitives.ReadUInt32BigEndian(hash) % 1_000_000;
            return value.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// secret = ECDH(static) || ECDH(ephemeral); HKDF-SHA256 with salt nonceExt || nonceApp gives
        /// 32 bytes for extension→app, then 32 bytes for app→extension.
        /// </summary>
        public static (byte[] ExtensionToApp, byte[] AppToExtension) DeriveSessionKeys(
            ECDiffieHellman appStatic, ECDiffieHellman extensionStatic,
            ECDiffieHellman appEphemeral, ECDiffieHellman extensionEphemeral,
            byte[] extensionNonce, byte[] appNonce)
        {
            var staticSecret = appStatic.DeriveRawSecretAgreement(extensionStatic.PublicKey);
            var ephemeralSecret = appEphemeral.DeriveRawSecretAgreement(extensionEphemeral.PublicKey);
            var secret = new byte[staticSecret.Length + ephemeralSecret.Length];
            staticSecret.CopyTo(secret, 0);
            ephemeralSecret.CopyTo(secret, staticSecret.Length);

            var salt = new byte[extensionNonce.Length + appNonce.Length];
            extensionNonce.CopyTo(salt, 0);
            appNonce.CopyTo(salt, extensionNonce.Length);

            var keys = HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, 2 * KeySize, salt, SessionInfo);
            CryptographicOperations.ZeroMemory(staticSecret);
            CryptographicOperations.ZeroMemory(ephemeralSecret);
            CryptographicOperations.ZeroMemory(secret);
            var result = (keys[..KeySize], keys[KeySize..]);
            CryptographicOperations.ZeroMemory(keys);
            return result;
        }

        /// <summary>AES-256-GCM; the nonce is 4 zero bytes and the 64-bit big-endian message counter. Returns ciphertext || tag.</summary>
        public static byte[] Seal(byte[] key, ulong counter, byte[] plaintext)
        {
            var result = new byte[plaintext.Length + TagSize];
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(Nonce(counter), plaintext, result.AsSpan(0, plaintext.Length), result.AsSpan(plaintext.Length));
            return result;
        }

        /// <summary>Reverses <see cref="Seal"/>; throws <see cref="CryptographicException"/> when the message was altered.</summary>
        public static byte[] Open(byte[] key, ulong counter, byte[] sealedMessage)
        {
            if (sealedMessage.Length < TagSize)
                throw new CryptographicException("The message is too short.");

            var length = sealedMessage.Length - TagSize;
            var plaintext = new byte[length];
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(Nonce(counter), sealedMessage.AsSpan(0, length), sealedMessage.AsSpan(length), plaintext);
            return plaintext;
        }

        private static byte[] Nonce(ulong counter)
        {
            var nonce = new byte[12];
            BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(4), counter);
            return nonce;
        }
    }
}
