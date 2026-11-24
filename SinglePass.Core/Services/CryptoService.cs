using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using SinglePass.Core.Helpers;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SinglePass.Core.Services
{
    /// <summary>
    /// Encryption of the vault file. Format version 2 (written now) encrypts the contents with a random vault key
    /// (<see cref="VaultKey"/>), which the header keeps encrypted with a key derived from the master password.
    /// Format version 1 and the legacy format are only read. See docs/vault.md.
    /// </summary>
    public class CryptoService
    {
        private static ReadOnlySpan<byte> Magic => "SPVAULT!"u8;
        private const byte Format1 = 1;
        private const byte Format2 = 2;
        private const int SaltSize = 16;
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int KeySize = 32;

        // Argon2id parameters for new password slots. Format 2 stores them, so they can be raised without a new format.
        private const int MemoryKiB = 64 * 1024;
        private const int Iterations = 3;
        private const int Parallelism = 4;

        // Accepted when reading, so that a damaged or hostile file cannot make unlocking exhaust memory or time.
        private const int MinMemoryKiB = 8 * 1024;
        private const int MaxMemoryKiB = 1024 * 1024;
        private const int MaxIterations = 32;
        private const int MaxParallelism = 16;

        // Format 2 layout. Everything before ContentNonceOffset is the header (VaultKey.Header).
        private const int VersionOffset = 8;
        private const int MemoryOffset = 9;
        private const int IterationsOffset = 13;
        private const int ParallelismOffset = 17;
        private const int SaltOffset = 18;
        private const int KeyIdOffset = SaltOffset + SaltSize;                       // 34
        private const int SlotNonceOffset = KeyIdOffset + VaultKey.IdSize;          // 50, end of the data the slot authenticates
        private const int WrappedKeyOffset = SlotNonceOffset + NonceSize;           // 62
        private const int SlotTagOffset = WrappedKeyOffset + KeySize;               // 94
        private const int HeaderSize = SlotTagOffset + TagSize;                      // 110
        private const int ContentTagOffset = HeaderSize + NonceSize;                // 122
        private const int ContentOffset = ContentTagOffset + TagSize;               // 138

        private static ReadOnlySpan<byte> LegacyKeyTemplate =>
        [
            97, 238, 238, 23, 235, 212, 131, 197, 191, 5, 236, 111, 81, 47, 125, 191,
            211, 41, 121, 148, 132, 70, 204, 94, 133, 220, 255, 225, 169, 242, 67, 114
        ];

        /// <summary>Decrypts a vault file of any supported format with the master password.</summary>
        public T DecryptFromStream<T>(Stream stream, string password)
        {
            var result = DecryptFromStream<T>(stream, password, out var key);
            key?.Dispose();
            return result;
        }

        /// <summary>Writes <paramref name="obj"/> in format 2 under a new vault key for <paramref name="password"/>.</summary>
        public void EncryptToStream<T>(T obj, Stream stream, string password)
        {
            using var key = CreateKey(password);
            EncryptToStream(obj, stream, key);
        }

        /// <summary>
        /// Creates a random vault key and its password slot. Runs Argon2, so it takes about half a second.
        /// </summary>
        internal VaultKey CreateKey(string password)
        {
            var header = new byte[HeaderSize];
            Magic.CopyTo(header);
            header[VersionOffset] = Format2;
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(MemoryOffset), MemoryKiB);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(IterationsOffset), Iterations);
            header[ParallelismOffset] = Parallelism;
            RandomNumberGenerator.Fill(header.AsSpan(SaltOffset, SaltSize));
            RandomNumberGenerator.Fill(header.AsSpan(SlotNonceOffset, NonceSize));

            var vaultKey = new byte[KeySize];
            var passwordKey = new byte[KeySize];
            try
            {
                RandomNumberGenerator.Fill(vaultKey);
                VaultKey.ComputeId(vaultKey).CopyTo(header.AsSpan(KeyIdOffset));
                DeriveKey(password, header.AsSpan(SaltOffset, SaltSize), MemoryKiB, Iterations, Parallelism, passwordKey);
                using var aes = new AesGcm(passwordKey, TagSize);
                aes.Encrypt(header.AsSpan(SlotNonceOffset, NonceSize), vaultKey,
                    header.AsSpan(WrappedKeyOffset, KeySize), header.AsSpan(SlotTagOffset, TagSize), header.AsSpan(0, SlotNonceOffset));
                return new VaultKey(vaultKey, header);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(vaultKey);
                CryptographicOperations.ZeroMemory(passwordKey);
            }
        }

        /// <summary>Writes <paramref name="obj"/> in format 2: the header of <paramref name="key"/> and the contents encrypted with it.</summary>
        internal void EncryptToStream<T>(T obj, Stream stream, VaultKey key)
        {
            var plaintext = JsonSerializer.SerializeToUtf8Bytes(obj);
            try
            {
                var file = new byte[ContentOffset + plaintext.Length];
                key.Header.CopyTo(file, 0);
                var nonce = file.AsSpan(HeaderSize, NonceSize);
                RandomNumberGenerator.Fill(nonce);
                using (var aes = new AesGcm(key.Key, TagSize))
                    aes.Encrypt(nonce, plaintext, file.AsSpan(ContentOffset), file.AsSpan(ContentTagOffset, TagSize), key.Header);

                stream.Position = 0;
                stream.Write(file);
                stream.SetLength(stream.Position);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        /// <summary>
        /// Decrypts a vault file with the master password. <paramref name="key"/> is the vault key of a format 2 file,
        /// or null for format 1 and legacy files, which have none and are converted when written.
        /// Throws <see cref="CryptographicException"/> for a wrong password and
        /// <see cref="UnsupportedVaultVersionException"/> for a file of a newer SinglePass.
        /// </summary>
        internal T DecryptFromStream<T>(Stream stream, string password, out VaultKey key)
        {
            key = null;
            switch (ReadFormat(stream))
            {
                case 0:
                    return DecryptLegacy<T>(stream, password);
                case Format1:
                    return DecryptFormat1<T>(stream, password);
            }

            var file = ReadAll(stream);
            if (file.Length < ContentOffset)
                throw new InvalidDataException("The vault file is too short.");

            var header = file.AsSpan(0, HeaderSize);
            var vaultKey = new byte[KeySize];
            var passwordKey = new byte[KeySize];
            try
            {
                var (memory, iterations, parallelism) = ReadKdfParameters(header);
                DeriveKey(password, header.Slice(SaltOffset, SaltSize), memory, iterations, parallelism, passwordKey);
                using (var aes = new AesGcm(passwordKey, TagSize))
                {
                    aes.Decrypt(header.Slice(SlotNonceOffset, NonceSize), header.Slice(WrappedKeyOffset, KeySize),
                        header.Slice(SlotTagOffset, TagSize), vaultKey, header[..SlotNonceOffset]);
                }

                var candidate = new VaultKey(vaultKey, header);
                if (!candidate.IsSameKey(header.Slice(KeyIdOffset, VaultKey.IdSize)))
                {
                    candidate.Dispose();
                    throw new InvalidDataException("The vault key does not match its id.");
                }

                try
                {
                    var result = DecryptContents<T>(file, candidate.Key);
                    key = candidate;
                    return result;
                }
                catch
                {
                    candidate.Dispose();
                    throw;
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(vaultKey);
                CryptographicOperations.ZeroMemory(passwordKey);
            }
        }

        /// <summary>
        /// Decrypts a format 2 file that is encrypted with the vault key <paramref name="key"/>, without the password.
        /// Returns false when the file uses another key or an older format. <paramref name="fileKey"/> is the key with
        /// the header of this file. Throws for a damaged file or one of a newer SinglePass.
        /// </summary>
        internal bool TryDecryptWithKey<T>(Stream stream, ReadOnlySpan<byte> key, out T result, out VaultKey fileKey)
        {
            result = default;
            fileKey = null;
            if (ReadFormat(stream) != Format2)
                return false;

            var file = ReadAll(stream);
            if (file.Length < ContentOffset)
                throw new InvalidDataException("The vault file is too short.");

            var candidate = new VaultKey(key, file.AsSpan(0, HeaderSize));
            if (!candidate.IsSameKey(file.AsSpan(KeyIdOffset, VaultKey.IdSize)))
            {
                candidate.Dispose();
                return false;
            }

            try
            {
                result = DecryptContents<T>(file, candidate.Key);
                fileKey = candidate;
                return true;
            }
            catch
            {
                candidate.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Checks that a written file is exactly <paramref name="key"/>'s header followed by contents that decrypt
        /// with that key. Cheap: no Argon2 and no JSON.
        /// </summary>
        internal bool IsWrittenWith(Stream stream, VaultKey key)
        {
            var file = ReadAll(stream);
            if (file.Length < ContentOffset || !file.AsSpan(0, HeaderSize).SequenceEqual(key.Header) ||
                !key.IsSameKey(VaultKey.ComputeId(key.Key)) || !key.IsSameKey(file.AsSpan(KeyIdOffset, VaultKey.IdSize)))
                return false;

            var plaintext = new byte[file.Length - ContentOffset];
            try
            {
                using var aes = new AesGcm(key.Key, TagSize);
                aes.Decrypt(file.AsSpan(HeaderSize, NonceSize), file.AsSpan(ContentOffset),
                    file.AsSpan(ContentTagOffset, TagSize), plaintext, file.AsSpan(0, HeaderSize));
                return true;
            }
            catch (CryptographicException)
            {
                return false;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        /// <summary>Whether <paramref name="password"/> opens the password slot of <paramref name="key"/>. Runs Argon2.</summary>
        internal bool OpensWithPassword(VaultKey key, string password)
        {
            var header = key.Header.AsSpan();
            var (memory, iterations, parallelism) = ReadKdfParameters(header);
            var vaultKey = new byte[KeySize];
            var passwordKey = new byte[KeySize];
            try
            {
                DeriveKey(password, header.Slice(SaltOffset, SaltSize), memory, iterations, parallelism, passwordKey);
                using var aes = new AesGcm(passwordKey, TagSize);
                aes.Decrypt(header.Slice(SlotNonceOffset, NonceSize), header.Slice(WrappedKeyOffset, KeySize),
                    header.Slice(SlotTagOffset, TagSize), vaultKey, header[..SlotNonceOffset]);
                return CryptographicOperations.FixedTimeEquals(vaultKey, key.Key);
            }
            catch (CryptographicException)
            {
                return false;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(vaultKey);
                CryptographicOperations.ZeroMemory(passwordKey);
            }
        }

        /// <summary>Writes format 1, as SinglePass 1.x did. Only for tests of reading older files.</summary>
        internal void EncryptToStreamFormat1<T>(T obj, Stream stream, string password)
        {
            var header = new byte[Magic.Length + 1 + SaltSize + NonceSize];
            Magic.CopyTo(header);
            header[Magic.Length] = Format1;
            var salt = header.AsSpan(Magic.Length + 1, SaltSize);
            var nonce = header.AsSpan(Magic.Length + 1 + SaltSize, NonceSize);
            RandomNumberGenerator.Fill(salt);
            RandomNumberGenerator.Fill(nonce);

            var plaintext = JsonSerializer.SerializeToUtf8Bytes(obj);
            var key = new byte[KeySize];
            try
            {
                DeriveKey(password, salt, MemoryKiB, Iterations, Parallelism, key);
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

        /// <summary>
        /// Format of a vault file: 0 for legacy, otherwise the format version.
        /// Throws <see cref="UnsupportedVaultVersionException"/> for a format of a newer SinglePass.
        /// </summary>
        internal static int ReadFormat(Stream stream)
        {
            stream.Position = 0;
            Span<byte> start = stackalloc byte[Magic.Length + 1];
            if (stream.ReadAtLeast(start, start.Length, throwOnEndOfStream: false) < start.Length || !start[..Magic.Length].SequenceEqual(Magic))
                return 0;

            var format = start[Magic.Length];
            if (format > Format2)
                throw new UnsupportedVaultVersionException($"The vault file uses encryption format {format}, which this version does not know.");
            if (format < Format1)
                throw new InvalidDataException("Invalid vault file format version.");
            return format;
        }

        private static T DecryptContents<T>(byte[] file, ReadOnlySpan<byte> key)
        {
            var plaintext = new byte[file.Length - ContentOffset];
            try
            {
                using (var aes = new AesGcm(key, TagSize))
                {
                    aes.Decrypt(file.AsSpan(HeaderSize, NonceSize), file.AsSpan(ContentOffset),
                        file.AsSpan(ContentTagOffset, TagSize), plaintext, file.AsSpan(0, HeaderSize));
                }
                return JsonSerializer.Deserialize<T>(plaintext.AsSpan());
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        private static (int Memory, int Iterations, int Parallelism) ReadKdfParameters(ReadOnlySpan<byte> header)
        {
            var memory = BinaryPrimitives.ReadInt32LittleEndian(header[MemoryOffset..]);
            var iterations = BinaryPrimitives.ReadInt32LittleEndian(header[IterationsOffset..]);
            int parallelism = header[ParallelismOffset];
            if (memory < MinMemoryKiB || iterations < 1 || parallelism < 1)
                throw new InvalidDataException("Invalid key derivation parameters in the vault file.");

            // Stronger than this version allows: a newer SinglePass may have raised them, so ask to update, not for a password.
            if (memory > MaxMemoryKiB || iterations > MaxIterations || parallelism > MaxParallelism)
                throw new UnsupportedVaultVersionException("The vault file uses key derivation parameters above what this version accepts.");
            return (memory, iterations, parallelism);
        }

        private static byte[] ReadAll(Stream stream)
        {
            stream.Position = 0;
            var file = new byte[checked((int)stream.Length)];
            stream.ReadExactly(file);
            return file;
        }

        private static T DecryptFormat1<T>(Stream stream, string password)
        {
            stream.Position = 0;
            Span<byte> header = stackalloc byte[Magic.Length + 1 + SaltSize + NonceSize];
            stream.ReadExactly(header);

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
                // Format 1 fixes the KDF parameters.
                DeriveKey(password, salt, MemoryKiB, Iterations, Parallelism, key);
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

        private static void DeriveKey(string password, ReadOnlySpan<byte> salt, int memoryKiB, int iterations, int parallelism, byte[] key)
        {
            var parameters = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
                .WithVersion(Argon2Parameters.Version13)
                .WithMemoryAsKB(memoryKiB)
                .WithIterations(iterations)
                .WithParallelism(parallelism)
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
