using System;
using System.Security.Cryptography;

namespace SinglePass.Core.Services
{
    /// <summary>
    /// The random key the vault contents are encrypted with (format version 2), together with the file header that
    /// holds it encrypted under the master password. Both always travel together: a file is written as this header
    /// followed by contents encrypted with this key, so the key in a file and the key of its contents cannot differ.
    /// </summary>
    internal sealed class VaultKey : IDisposable
    {
        internal const int KeySize = 32;
        internal const int IdSize = 16;

        private static ReadOnlySpan<byte> IdLabel => "SinglePass vault key id"u8;

        private readonly byte[] _key;

        internal VaultKey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> header)
        {
            if (key.Length != KeySize)
                throw new ArgumentException("A vault key is 32 bytes long.", nameof(key));

            _key = key.ToArray();
            Id = ComputeId(key);
            Header = header.ToArray();
        }

        private bool _disposed;

        /// <summary>The key itself. Zeroed by <see cref="Dispose"/>; reading it afterwards throws instead of returning zeros.</summary>
        internal ReadOnlySpan<byte> Key
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _key;
            }
        }

        /// <summary>Identifies the key without revealing it; stored in the file header in the clear.</summary>
        internal byte[] Id { get; }

        /// <summary>The file header up to the contents: format, KDF parameters, salt, key id and the password slot.</summary>
        internal byte[] Header { get; }

        internal bool IsSameKey(ReadOnlySpan<byte> id) => CryptographicOperations.FixedTimeEquals(Id, id);

        internal VaultKey Clone() => new(_key, Header);

        internal static byte[] ComputeId(ReadOnlySpan<byte> key)
        {
            Span<byte> mac = stackalloc byte[HMACSHA256.HashSizeInBytes];
            HMACSHA256.HashData(key, IdLabel, mac);
            return mac[..IdSize].ToArray();
        }

        public void Dispose()
        {
            _disposed = true;
            CryptographicOperations.ZeroMemory(_key);
        }
    }
}
