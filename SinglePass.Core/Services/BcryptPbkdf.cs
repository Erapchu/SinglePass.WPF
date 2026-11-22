using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SinglePass.Core.Services
{
    /// <summary>
    /// <c>bcrypt_pbkdf</c> from OpenBSD: the key derivation OpenSSH uses for private keys with a passphrase.
    /// BouncyCastle has bcrypt itself but not this variant, which runs a modified bcrypt over SHA-512 hashes.
    /// </summary>
    internal static class BcryptPbkdf
    {
        private const int HashSize = 32;
        private static readonly byte[] MagicText = Encoding.ASCII.GetBytes("OxychromaticBlowfishSwatDynamite");
        private static readonly Lazy<uint[]> PiWords = new(ComputePiWords);

        public static byte[] Derive(byte[] password, byte[] salt, int rounds, int length)
        {
            if (rounds < 1)
                throw new ArgumentOutOfRangeException(nameof(rounds));
            if (length < 1 || length > 1024)
                throw new ArgumentOutOfRangeException(nameof(length));

            var key = new byte[length];
            var stride = (length + HashSize - 1) / HashSize;
            var amount = (length + stride - 1) / stride;
            var passwordHash = SHA512.HashData(password);
            var countSalt = new byte[salt.Length + 4];
            salt.CopyTo(countSalt, 0);
            var output = new byte[HashSize];
            var hash = new byte[HashSize];

            try
            {
                var remaining = length;
                for (var count = 1; remaining > 0; count++)
                {
                    BinaryPrimitives.WriteUInt32BigEndian(countSalt.AsSpan(salt.Length), (uint)count);
                    var saltHash = SHA512.HashData(countSalt);
                    Hash(passwordHash, saltHash, hash);
                    hash.CopyTo(output, 0);

                    for (var round = 1; round < rounds; round++)
                    {
                        saltHash = SHA512.HashData(hash);
                        Hash(passwordHash, saltHash, hash);
                        for (var i = 0; i < HashSize; i++)
                            output[i] ^= hash[i];
                    }

                    amount = Math.Min(amount, remaining);
                    int written;
                    for (written = 0; written < amount; written++)
                    {
                        var target = written * stride + (count - 1);
                        if (target >= length)
                            break;

                        key[target] = output[written];
                    }
                    remaining -= written;
                }

                return key;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(passwordHash);
                CryptographicOperations.ZeroMemory(output);
                CryptographicOperations.ZeroMemory(hash);
            }
        }

        private static void Hash(byte[] passwordHash, byte[] saltHash, byte[] result)
        {
            var state = new Blowfish(PiWords.Value);
            state.ExpandState(saltHash, passwordHash);
            for (var i = 0; i < 64; i++)
            {
                state.ExpandKey(saltHash);
                state.ExpandKey(passwordHash);
            }

            var data = new uint[HashSize / 4];
            for (var i = 0; i < data.Length; i++)
                data[i] = BinaryPrimitives.ReadUInt32BigEndian(MagicText.AsSpan(i * 4));

            for (var i = 0; i < 64; i++)
                for (var block = 0; block < data.Length; block += 2)
                    state.Encrypt(ref data[block], ref data[block + 1]);

            // The words go out little-endian, unlike the rest of bcrypt.
            for (var i = 0; i < data.Length; i++)
                BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(i * 4), data[i]);
        }

        /// <summary>
        /// The initial Blowfish state: the first 1042 32-bit words of the fractional part of pi
        /// (18 for the P-array, then four S-boxes of 256). Computed once instead of a constant table.
        /// </summary>
        private static uint[] ComputePiWords()
        {
            const int words = 18 + 4 * 256;
            const int bits = words * 32;
            const int guard = 64;
            var scale = BigInteger.One << (bits + guard);

            // Machin's formula: pi = 16 atan(1/5) - 4 atan(1/239).
            var pi = 16 * ArcTanInverse(5, scale) - 4 * ArcTanInverse(239, scale);
            var fraction = (pi >> guard) & ((BigInteger.One << bits) - 1);
            var bytes = fraction.ToByteArray(isUnsigned: true, isBigEndian: true);

            var result = new uint[words];
            var offset = words * 4 - bytes.Length;
            var padded = new byte[words * 4];
            bytes.CopyTo(padded, offset);
            for (var i = 0; i < words; i++)
                result[i] = BinaryPrimitives.ReadUInt32BigEndian(padded.AsSpan(i * 4));
            return result;
        }

        /// <summary>atan(1/x) scaled by <paramref name="scale"/>, from its Taylor series.</summary>
        private static BigInteger ArcTanInverse(int x, BigInteger scale)
        {
            var xSquared = x * x;
            var power = scale / x;
            var sum = power;
            for (var n = 3; !power.IsZero; n += 2)
            {
                power /= xSquared;
                var term = power / n;
                sum = (n & 2) != 0 ? sum - term : sum + term;
            }
            return sum;
        }

        internal static uint[] InitialStateForTests => PiWords.Value;

        private sealed class Blowfish
        {
            private readonly uint[] _p = new uint[18];
            private readonly uint[] _s = new uint[4 * 256];

            public Blowfish(uint[] initial)
            {
                Array.Copy(initial, 0, _p, 0, _p.Length);
                Array.Copy(initial, _p.Length, _s, 0, _s.Length);
            }

            /// <summary>The expensive key schedule of bcrypt with a salt (<c>Blowfish_expandstate</c>).</summary>
            public void ExpandState(byte[] data, byte[] key)
            {
                var position = 0;
                for (var i = 0; i < _p.Length; i++)
                    _p[i] ^= StreamToWord(key, ref position);

                position = 0;
                uint left = 0, right = 0;
                for (var i = 0; i < _p.Length; i += 2)
                {
                    left ^= StreamToWord(data, ref position);
                    right ^= StreamToWord(data, ref position);
                    Encrypt(ref left, ref right);
                    _p[i] = left;
                    _p[i + 1] = right;
                }

                for (var i = 0; i < _s.Length; i += 2)
                {
                    left ^= StreamToWord(data, ref position);
                    right ^= StreamToWord(data, ref position);
                    Encrypt(ref left, ref right);
                    _s[i] = left;
                    _s[i + 1] = right;
                }
            }

            /// <summary>The key schedule without a salt (<c>Blowfish_expand0state</c>).</summary>
            public void ExpandKey(byte[] key)
            {
                var position = 0;
                for (var i = 0; i < _p.Length; i++)
                    _p[i] ^= StreamToWord(key, ref position);

                uint left = 0, right = 0;
                for (var i = 0; i < _p.Length; i += 2)
                {
                    Encrypt(ref left, ref right);
                    _p[i] = left;
                    _p[i + 1] = right;
                }

                for (var i = 0; i < _s.Length; i += 2)
                {
                    Encrypt(ref left, ref right);
                    _s[i] = left;
                    _s[i + 1] = right;
                }
            }

            public void Encrypt(ref uint left, ref uint right)
            {
                var l = left ^ _p[0];
                var r = right;
                for (var i = 1; i <= 16; i += 2)
                {
                    r ^= F(l) ^ _p[i];
                    l ^= F(r) ^ _p[i + 1];
                }
                left = r ^ _p[17];
                right = l;
            }

            private uint F(uint x) =>
                ((_s[x >> 24] + _s[256 + ((x >> 16) & 0xFF)]) ^ _s[512 + ((x >> 8) & 0xFF)]) + _s[768 + (x & 0xFF)];

            /// <summary>Next big-endian word of <paramref name="data"/>, wrapping around at its end.</summary>
            private static uint StreamToWord(byte[] data, ref int position)
            {
                uint word = 0;
                for (var i = 0; i < 4; i++)
                {
                    word = (word << 8) | data[position];
                    position = (position + 1) % data.Length;
                }
                return word;
            }
        }
    }
}
