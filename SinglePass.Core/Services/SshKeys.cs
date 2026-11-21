using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Utilities;
using Org.BouncyCastle.Security;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SinglePass.Core.Services
{
    /// <summary>Reading and creating SSH keys in the OpenSSH text formats.</summary>
    public static class SshKeys
    {
        private const string OpenSshPemLabel = "OPENSSH PRIVATE KEY";
        private static readonly byte[] OpenSshMagic = Encoding.ASCII.GetBytes("openssh-key-v1\0");

        /// <summary>
        /// Gets the public key of a private key file in <c>authorized_keys</c> format, without a comment.
        /// OpenSSH keys keep the public key unencrypted, so this works for keys with a passphrase too.
        /// PEM keys (RSA, ECDSA, PKCS#8) are read only when they are not encrypted.
        /// </summary>
        public static bool TryGetPublicKey(string privateKey, out string publicKey)
        {
            publicKey = null;
            if (string.IsNullOrWhiteSpace(privateKey))
                return false;

            try
            {
                var blob = TryReadOpenSshPrivateKey(privateKey, out _) ?? TryReadPemPublicBlob(privateKey);
                if (blob == null)
                    return false;

                publicKey = FormatPublicKey(blob, comment: null);
                return true;
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException
                                       or EndOfStreamException or InvalidDataException)
            {
                return false;
            }
        }

        /// <summary>True when the private key needs a passphrase to be used.</summary>
        public static bool IsEncrypted(string privateKey)
        {
            if (string.IsNullOrWhiteSpace(privateKey))
                return false;

            try
            {
                if (TryReadOpenSshPrivateKey(privateKey, out var cipher) != null)
                    return cipher != "none";
            }
            catch (Exception ex) when (ex is FormatException or EndOfStreamException or InvalidDataException)
            {
                return false;
            }

            return privateKey.Contains("ENCRYPTED", StringComparison.Ordinal);
        }

        /// <summary>
        /// Parses an <c>authorized_keys</c> line: <c>type base64 [comment]</c>. The type must match the
        /// one inside the key data. Returns the SHA-256 fingerprint as <c>ssh-keygen -l</c> shows it.
        /// </summary>
        public static bool TryParsePublicKey(string publicKey, out string keyType, out string fingerprint, out string comment)
        {
            keyType = fingerprint = comment = null;
            if (string.IsNullOrWhiteSpace(publicKey))
                return false;

            var parts = publicKey.Trim().Split((char[])null, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return false;

            try
            {
                var blob = Convert.FromBase64String(parts[1]);
                var reader = new SshReader(blob);
                if (reader.ReadString() != parts[0])
                    return false;

                keyType = parts[0];
                fingerprint = "SHA256:" + Convert.ToBase64String(SHA256.HashData(blob)).TrimEnd('=');
                comment = parts.Length > 2 ? parts[2].Trim() : null;
                return true;
            }
            catch (Exception ex) when (ex is FormatException or EndOfStreamException or InvalidDataException)
            {
                return false;
            }
        }

        /// <summary>Creates an Ed25519 key without a passphrase: the vault itself protects it.</summary>
        public static (string PrivateKey, string PublicKey) GenerateEd25519(string comment)
        {
            var generator = new Ed25519KeyPairGenerator();
            generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
            var pair = generator.GenerateKeyPair();

            var privateBlob = OpenSshPrivateKeyUtilities.EncodePrivateKey(pair.Private);
            var publicBlob = OpenSshPublicKeyUtilities.EncodePublicKey((Ed25519PublicKeyParameters)pair.Public);
            try
            {
                return (FormatPem(OpenSshPemLabel, privateBlob), FormatPublicKey(publicBlob, comment));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(privateBlob);
            }
        }

        /// <summary>
        /// Private key text as OpenSSH expects it in a file: LF line endings and a final newline.
        /// Without the final newline <c>ssh</c> rejects the key as invalid.
        /// </summary>
        public static string NormalizePrivateKey(string privateKey)
        {
            var text = privateKey.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            return text + "\n";
        }

        private static string FormatPublicKey(byte[] blob, string comment)
        {
            var type = new SshReader(blob).ReadString();
            var line = type + " " + Convert.ToBase64String(blob);
            return string.IsNullOrWhiteSpace(comment) ? line : line + " " + comment.Trim();
        }

        private static string FormatPem(string label, byte[] data)
        {
            var base64 = Convert.ToBase64String(data);
            var builder = new StringBuilder();
            builder.Append("-----BEGIN ").Append(label).Append("-----\n");
            for (var i = 0; i < base64.Length; i += 70)
                builder.Append(base64, i, Math.Min(70, base64.Length - i)).Append('\n');
            builder.Append("-----END ").Append(label).Append("-----\n");
            return builder.ToString();
        }

        /// <summary>Public key blob of the first key in an OpenSSH private key file, or null for other formats.</summary>
        private static byte[] TryReadOpenSshPrivateKey(string privateKey, out string cipher)
        {
            cipher = null;
            var data = TryDecodePem(privateKey, OpenSshPemLabel);
            if (data == null)
                return null;

            if (!data.AsSpan().StartsWith(OpenSshMagic))
                throw new InvalidDataException("Not an OpenSSH private key.");

            var reader = new SshReader(data, OpenSshMagic.Length);
            cipher = reader.ReadString();
            reader.ReadBytes(); // KDF name
            reader.ReadBytes(); // KDF options
            if (reader.ReadUInt32() < 1)
                throw new InvalidDataException("The key file holds no keys.");

            return reader.ReadBytes();
        }

        private static byte[] TryDecodePem(string text, string label)
        {
            var begin = "-----BEGIN " + label + "-----";
            var end = "-----END " + label + "-----";
            var start = text.IndexOf(begin, StringComparison.Ordinal);
            if (start < 0)
                return null;

            start += begin.Length;
            var stop = text.IndexOf(end, start, StringComparison.Ordinal);
            if (stop < 0)
                throw new FormatException("The key has no end marker.");

            return Convert.FromBase64String(text[start..stop]);
        }

        private static byte[] TryReadPemPublicBlob(string privateKey)
        {
            if (!privateKey.Contains("-----BEGIN", StringComparison.Ordinal) ||
                privateKey.Contains("ENCRYPTED", StringComparison.Ordinal))
                return null;

            if (privateKey.Contains("-----BEGIN EC PRIVATE KEY-----", StringComparison.Ordinal))
                return ReadEcdsa(privateKey);

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(privateKey);
                var parameters = rsa.ExportParameters(false);
                var writer = new SshWriter();
                writer.WriteString("ssh-rsa");
                writer.WriteMpint(parameters.Exponent);
                writer.WriteMpint(parameters.Modulus);
                return writer.ToArray();
            }
            catch (CryptographicException)
            {
                // A PKCS#8 "PRIVATE KEY" may hold an EC key instead.
                return ReadEcdsa(privateKey);
            }
        }

        private static byte[] ReadEcdsa(string privateKey)
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(privateKey);
            var parameters = ecdsa.ExportParameters(false);
            var curve = parameters.Curve.Oid?.FriendlyName switch
            {
                "nistP256" or "ECDSA_P256" => "nistp256",
                "nistP384" or "ECDSA_P384" => "nistp384",
                "nistP521" or "ECDSA_P521" => "nistp521",
                _ => throw new CryptographicException("Unsupported elliptic curve.")
            };

            var point = new byte[1 + parameters.Q.X.Length + parameters.Q.Y.Length];
            point[0] = 0x04;
            parameters.Q.X.CopyTo(point, 1);
            parameters.Q.Y.CopyTo(point, 1 + parameters.Q.X.Length);

            var writer = new SshWriter();
            writer.WriteString("ecdsa-sha2-" + curve);
            writer.WriteString(curve);
            writer.WriteBytes(point);
            return writer.ToArray();
        }

        private sealed class SshReader(byte[] data, int position = 0)
        {
            private int _position = position;

            public uint ReadUInt32()
            {
                if (data.Length - _position < 4)
                    throw new EndOfStreamException();

                var value = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(_position));
                _position += 4;
                return value;
            }

            public byte[] ReadBytes()
            {
                var length = ReadUInt32();
                if (length > data.Length - _position)
                    throw new EndOfStreamException();

                var value = data.AsSpan(_position, (int)length).ToArray();
                _position += (int)length;
                return value;
            }

            public string ReadString() => Encoding.ASCII.GetString(ReadBytes());
        }

        private sealed class SshWriter
        {
            private readonly MemoryStream _stream = new();

            public void WriteBytes(ReadOnlySpan<byte> value)
            {
                Span<byte> length = stackalloc byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(length, (uint)value.Length);
                _stream.Write(length);
                _stream.Write(value);
            }

            public void WriteString(string value) => WriteBytes(Encoding.ASCII.GetBytes(value));

            /// <summary>Unsigned big-endian integer: no leading zeros, plus one zero byte if the top bit is set.</summary>
            public void WriteMpint(byte[] value)
            {
                var span = value.AsSpan().TrimStart((byte)0);
                if (span.Length > 0 && (span[0] & 0x80) != 0)
                {
                    var padded = new byte[span.Length + 1];
                    span.CopyTo(padded.AsSpan(1));
                    WriteBytes(padded);
                }
                else
                    WriteBytes(span);
            }

            public byte[] ToArray() => _stream.ToArray();
        }
    }
}
