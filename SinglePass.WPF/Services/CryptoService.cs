using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using SinglePass.WPF.Helpers;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SinglePass.WPF.Services
{
    public class CryptoService
    {
        private static ReadOnlySpan<byte> Magic => "SPVAULT!"u8;
        private const byte FormatVersion = 1;
        private const int SaltSize = 16;
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int KeySize = 32;

        private static ReadOnlySpan<byte> LegacyKeyTemplate =>
        [
            97, 238, 238, 23, 235, 212, 131, 197, 191, 5, 236, 111, 81, 47, 125, 191,
            211, 41, 121, 148, 132, 70, 204, 94, 133, 220, 255, 225, 169, 242, 67, 114
        ];

        public T DecryptFromStream<T>(Stream stream, string password) =>
            DecryptFromStream<T>(stream, password, out _);

        internal T DecryptFromStream<T>(Stream stream, string password, out bool isLegacy)
        {
            stream.Position = 0;
            Span<byte> magic = stackalloc byte[Magic.Length];
            stream.ReadExactly(magic);
            isLegacy = !magic.SequenceEqual(Magic);
            return isLegacy ? DecryptLegacy<T>(stream, password) : DecryptCurrent<T>(stream, password);
        }

        public void EncryptToStream<T>(T obj, Stream stream, string password)
        {
            // Version 1 fixes the KDF parameters. The entire header is authenticated by AES-GCM.
            var header = new byte[Magic.Length + 1 + SaltSize + NonceSize];
            Magic.CopyTo(header);
            header[Magic.Length] = FormatVersion;
            var salt = header.AsSpan(Magic.Length + 1, SaltSize);
            var nonce = header.AsSpan(Magic.Length + 1 + SaltSize, NonceSize);
            RandomNumberGenerator.Fill(salt);
            RandomNumberGenerator.Fill(nonce);

            var plaintext = JsonSerializer.SerializeToUtf8Bytes(obj);
            var key = new byte[KeySize];
            try
            {
                DeriveKey(password, salt, key);
                using var aes = new AesGcm(key, TagSize);
                var ciphertext = new byte[plaintext.Length];
                Span<byte> tag = stackalloc byte[TagSize];
                aes.Encrypt(nonce, plaintext, ciphertext, tag, header);

                stream.Position = 0;
                stream.Write(header);
                stream.Write(tag);
                stream.Write(ciphertext);
                stream.SetLength(stream.Position);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        private static T DecryptCurrent<T>(Stream stream, string password)
        {
            stream.Position = 0;
            Span<byte> header = stackalloc byte[Magic.Length + 1 + SaltSize + NonceSize];
            stream.ReadExactly(header);
            if (header[Magic.Length] != FormatVersion)
                throw new InvalidDataException("Unsupported encrypted vault format version.");

            Span<byte> tag = stackalloc byte[TagSize];
            stream.ReadExactly(tag);
            var ciphertext = new byte[checked((int)(stream.Length - stream.Position))];
            stream.ReadExactly(ciphertext);

            var salt = header.Slice(Magic.Length + 1, SaltSize);
            var nonce = header.Slice(Magic.Length + 1 + SaltSize, NonceSize);
            var plaintext = new byte[ciphertext.Length];
            var key = new byte[KeySize];
            try
            {
                DeriveKey(password, salt, key);
                using var aes = new AesGcm(key, TagSize);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, header);
                return JsonSerializer.Deserialize<T>(plaintext.AsSpan());
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        private static void DeriveKey(string password, ReadOnlySpan<byte> salt, byte[] key)
        {
            var parameters = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
                .WithVersion(Argon2Parameters.Version13)
                .WithMemoryAsKB(64 * 1024)
                .WithIterations(3)
                .WithParallelism(4)
                .WithSalt(salt.ToArray())
                .Build();
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            try
            {
                var generator = new Argon2BytesGenerator();
                generator.Init(parameters);
                generator.GenerateBytes(passwordBytes, key);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(passwordBytes);
            }
        }

        private static T DecryptLegacy<T>(Stream stream, string password)
        {
            const int blockSize = AesCryptographyHelper.IVLength;
            if (stream.Length < 2 * blockSize || stream.Length % blockSize != 0)
                throw new InvalidDataException("Invalid legacy vault file length.");

            if (Encoding.UTF8.GetByteCount(password) > LegacyKeyTemplate.Length)
                throw new CryptographicException("Invalid password for a legacy vault.");

            var key = LegacyKeyTemplate.ToArray();
            try
            {
                Encoding.UTF8.GetBytes(password.AsSpan(), key);
                stream.Position = 0;
                var iv = new byte[blockSize];
                stream.ReadExactly(iv);
                var ciphertext = new byte[checked((int)(stream.Length - blockSize))];
                stream.ReadExactly(ciphertext);

                var plaintext = AesCryptographyHelper.DecryptStringFromBytes(ciphertext, key, iv);
                return JsonSerializer.Deserialize<T>(plaintext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }
}
