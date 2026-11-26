using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Crypto.Utilities;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Erapkey.Core.Services
{
    /// <summary>
    /// A private key decrypted for signing, as an SSH agent does. Reads OpenSSH keys (also with a
    /// passphrase) and PEM keys: RSA, ECDSA (NIST curves) and Ed25519.
    /// </summary>
    internal sealed class SshSigningKey
    {
        // Flags of an agent sign request that choose the RSA signature hash (RFC 8332).
        internal const uint RsaSha256Flag = 2;
        internal const uint RsaSha512Flag = 4;

        private const string OpenSshPemLabel = "OPENSSH PRIVATE KEY";
        private static readonly byte[] OpenSshMagic = Encoding.ASCII.GetBytes("openssh-key-v1\0");

        private readonly AsymmetricKeyParameter _privateKey;

        /// <summary>Public key in the SSH wire format, as an agent lists it.</summary>
        public byte[] PublicKeyBlob { get; }

        public string KeyType { get; }

        private SshSigningKey(AsymmetricKeyParameter privateKey)
        {
            _privateKey = privateKey;
            (KeyType, PublicKeyBlob) = EncodePublicKey(privateKey);
        }

        /// <exception cref="SshKeyException">The key cannot be read, the passphrase is wrong or the type is not supported.</exception>
        public static SshSigningKey Load(string privateKey, string passphrase)
        {
            if (string.IsNullOrWhiteSpace(privateKey))
                throw new SshKeyException("The entry has no private key.");

            try
            {
                return new SshSigningKey(privateKey.Contains("-----BEGIN " + OpenSshPemLabel + "-----", StringComparison.Ordinal)
                    ? ReadOpenSshKey(privateKey, passphrase)
                    : ReadPemKey(privateKey, passphrase));
            }
            catch (Exception ex) when (ex is FormatException or EndOfStreamException or InvalidDataException
                                       or CryptographicException or IOException or InvalidCipherTextException
                                       or ArgumentException or InvalidOperationException or PemException
                                       or SecurityUtilityException or NotSupportedException)
            {
                throw new SshKeyException(ex.Message, ex);
            }
        }

        /// <summary>Signs <paramref name="data"/> and returns the signature in the SSH wire format.</summary>
        public byte[] Sign(byte[] data, uint flags)
        {
            var writer = new SshWriter();
            switch (_privateKey)
            {
                case Ed25519PrivateKeyParameters:
                    {
                        var signer = new Ed25519Signer();
                        signer.Init(true, _privateKey);
                        signer.BlockUpdate(data, 0, data.Length);
                        writer.WriteString("ssh-ed25519");
                        writer.WriteBytes(signer.GenerateSignature());
                        break;
                    }
                case RsaKeyParameters:
                    {
                        var (name, digest) = (flags & RsaSha512Flag) != 0 ? ("rsa-sha2-512", (IDigest)new Sha512Digest())
                            : (flags & RsaSha256Flag) != 0 ? ("rsa-sha2-256", new Sha256Digest())
                            : ("ssh-rsa", new Sha1Digest());
                        var signer = new RsaDigestSigner(digest);
                        signer.Init(true, _privateKey);
                        signer.BlockUpdate(data, 0, data.Length);
                        writer.WriteString(name);
                        writer.WriteBytes(signer.GenerateSignature());
                        break;
                    }
                case ECPrivateKeyParameters ec:
                    {
                        var digest = CreateEcDigest(ec);
                        var hash = new byte[digest.GetDigestSize()];
                        digest.BlockUpdate(data, 0, data.Length);
                        digest.DoFinal(hash, 0);

                        var signer = new ECDsaSigner(new HMacDsaKCalculator(CreateEcDigest(ec)));
                        signer.Init(true, ec);
                        var rs = signer.GenerateSignature(hash);
                        var signature = new SshWriter();
                        signature.WriteBytes(rs[0].ToByteArray());
                        signature.WriteBytes(rs[1].ToByteArray());
                        writer.WriteString(KeyType);
                        writer.WriteBytes(signature.ToArray());
                        break;
                    }
                default:
                    throw new SshKeyException("Unsupported key type.");
            }
            return writer.ToArray();
        }

        private static AsymmetricKeyParameter ReadOpenSshKey(string text, string passphrase)
        {
            var data = DecodePem(text, OpenSshPemLabel);
            if (!data.AsSpan().StartsWith(OpenSshMagic))
                throw new InvalidDataException("Not an OpenSSH private key.");

            var reader = new SshReader(data, OpenSshMagic.Length);
            var cipher = reader.ReadString();
            if (cipher == "none")
                return OpenSshPrivateKeyUtilities.ParsePrivateKeyBlob(data);

            var kdf = reader.ReadString();
            var kdfOptions = new SshReader(reader.ReadBytes());
            if (reader.ReadUInt32() != 1)
                throw new InvalidDataException("Files with several keys are not supported.");

            var publicKey = reader.ReadBytes();
            var encrypted = reader.ReadBytes();
            if (kdf != "bcrypt")
                throw new NotSupportedException($"Key derivation {kdf} is not supported.");
            if (string.IsNullOrEmpty(passphrase))
                throw new SshKeyException("The key is encrypted and the entry has no passphrase.");

            var (keySize, ivSize, mode) = cipher switch
            {
                "aes128-ctr" => (16, 16, "CTR"),
                "aes192-ctr" => (24, 16, "CTR"),
                "aes256-ctr" => (32, 16, "CTR"),
                "aes128-cbc" => (16, 16, "CBC"),
                "aes192-cbc" => (24, 16, "CBC"),
                "aes256-cbc" => (32, 16, "CBC"),
                "aes128-gcm@openssh.com" => (16, 12, "GCM"),
                "aes256-gcm@openssh.com" => (32, 12, "GCM"),
                _ => throw new NotSupportedException($"Cipher {cipher} is not supported.")
            };

            var salt = kdfOptions.ReadBytes();
            var rounds = kdfOptions.ReadUInt32();
            if (rounds is 0 or > 10_000)
                throw new InvalidDataException("Invalid number of key derivation rounds.");

            var passphraseBytes = Encoding.UTF8.GetBytes(passphrase);
            var keyAndIv = BcryptPbkdf.Derive(passphraseBytes, salt, (int)rounds, keySize + ivSize);
            byte[] plain = null;
            try
            {
                var key = new KeyParameter(keyAndIv, 0, keySize);
                var iv = keyAndIv.AsSpan(keySize, ivSize).ToArray();
                IBufferedCipher aes;
                ICipherParameters parameters;
                byte[] input = encrypted;
                if (mode == "GCM")
                {
                    // The tag follows the encrypted section, outside of its length.
                    var tag = reader.ReadRaw(16);
                    input = [.. encrypted, .. tag];
                    aes = CipherUtilities.GetCipher("AES/GCM/NoPadding");
                    parameters = new AeadParameters(key, 128, iv);
                }
                else
                {
                    aes = CipherUtilities.GetCipher($"AES/{mode}/NoPadding");
                    parameters = new ParametersWithIV(key, iv);
                }

                aes.Init(false, parameters);
                try
                {
                    plain = aes.DoFinal(input);
                }
                catch (InvalidCipherTextException)
                {
                    throw new SshKeyException("The passphrase of the key is wrong.");
                }

                // Two equal random words start the section; they differ when the passphrase is wrong.
                var check = new SshReader(plain);
                if (check.ReadUInt32() != check.ReadUInt32())
                    throw new SshKeyException("The passphrase of the key is wrong.");

                // Rebuild the file as an unencrypted one, which BouncyCastle can read.
                var writer = new SshWriter();
                writer.WriteRaw(OpenSshMagic);
                writer.WriteString("none");
                writer.WriteString("none");
                writer.WriteBytes([]);
                writer.WriteUInt32(1);
                writer.WriteBytes(publicKey);
                writer.WriteBytes(plain);
                var unencrypted = writer.ToArray();
                try
                {
                    return OpenSshPrivateKeyUtilities.ParsePrivateKeyBlob(unencrypted);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(unencrypted);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(passphraseBytes);
                CryptographicOperations.ZeroMemory(keyAndIv);
                if (plain != null)
                    CryptographicOperations.ZeroMemory(plain);
            }
        }

        private static AsymmetricKeyParameter ReadPemKey(string text, string passphrase)
        {
            using var reader = new StringReader(text);
            var pem = new PemReader(reader, new PasswordFinder(passphrase));
            var value = pem.ReadObject();
            return value switch
            {
                AsymmetricCipherKeyPair pair => pair.Private,
                AsymmetricKeyParameter { IsPrivate: true } key => key,
                null => throw new InvalidDataException("The text is not a private key."),
                _ => throw new NotSupportedException("The PEM block is not a supported private key.")
            };
        }

        private static (string KeyType, byte[] Blob) EncodePublicKey(AsymmetricKeyParameter privateKey)
        {
            var writer = new SshWriter();
            string keyType;
            switch (privateKey)
            {
                case Ed25519PrivateKeyParameters ed:
                    keyType = "ssh-ed25519";
                    writer.WriteString(keyType);
                    writer.WriteBytes(ed.GeneratePublicKey().GetEncoded());
                    break;
                case RsaPrivateCrtKeyParameters rsa:
                    keyType = "ssh-rsa";
                    writer.WriteString(keyType);
                    writer.WriteBytes(rsa.PublicExponent.ToByteArray());
                    writer.WriteBytes(rsa.Modulus.ToByteArray());
                    break;
                case ECPrivateKeyParameters ec:
                    var curve = CurveName(ec);
                    keyType = "ecdsa-sha2-" + curve;
                    var point = ec.Parameters.G.Multiply(ec.D).Normalize();
                    writer.WriteString(keyType);
                    writer.WriteString(curve);
                    writer.WriteBytes(point.GetEncoded(false));
                    break;
                default:
                    throw new NotSupportedException("Only RSA, ECDSA and Ed25519 keys are supported.");
            }
            return (keyType, writer.ToArray());
        }

        private static string CurveName(ECPrivateKeyParameters key) => key.Parameters.Curve.FieldSize switch
        {
            256 => "nistp256",
            384 => "nistp384",
            521 => "nistp521",
            _ => throw new NotSupportedException("Only NIST P-256, P-384 and P-521 curves are supported.")
        };

        private static IDigest CreateEcDigest(ECPrivateKeyParameters key) => key.Parameters.Curve.FieldSize switch
        {
            256 => new Sha256Digest(),
            384 => new Sha384Digest(),
            _ => new Sha512Digest()
        };

        private static byte[] DecodePem(string text, string label)
        {
            var begin = "-----BEGIN " + label + "-----";
            var end = "-----END " + label + "-----";
            var start = text.IndexOf(begin, StringComparison.Ordinal) + begin.Length;
            var stop = text.IndexOf(end, start, StringComparison.Ordinal);
            if (stop < 0)
                throw new FormatException("The key has no end marker.");

            return Convert.FromBase64String(text[start..stop]);
        }

        private sealed class PasswordFinder(string passphrase) : IPasswordFinder
        {
            public char[] GetPassword() =>
                string.IsNullOrEmpty(passphrase)
                    ? throw new SshKeyException("The key is encrypted and the entry has no passphrase.")
                    : passphrase.ToCharArray();
        }
    }

    /// <summary>A private key that cannot be used: unreadable, wrong passphrase or unsupported type.</summary>
    public sealed class SshKeyException : Exception
    {
        public SshKeyException(string message) : base(message) { }
        public SshKeyException(string message, Exception inner) : base(message, inner) { }
    }
}
