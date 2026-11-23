using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace SinglePass.Core.Services.Browser
{
    /// <summary>
    /// A browser extension the user allowed. <see cref="AppPrivateKey"/> is the static key SinglePass made for this pairing.
    /// </summary>
    public sealed record BrowserPairing(
        Guid Id,
        string Name,
        string Browser,
        string ExtensionPublicKey,
        string AppPrivateKey,
        DateTimeOffset CreatedAt,
        DateTimeOffset LastUsedAt);

    /// <summary>
    /// Paired extensions of this device, in a file protected with DPAPI for the current user. Not synced: a pairing
    /// belongs to one browser profile on one machine.
    /// </summary>
    public sealed class BrowserPairingStore
    {
        /// <summary>How often <see cref="BrowserPairing.LastUsedAt"/> is written; every connection would be too often.</summary>
        private static readonly TimeSpan LastUsedPrecision = TimeSpan.FromHours(1);

        private readonly string _path;
        private readonly ILogger _logger;
        private readonly object _sync = new();
        private List<BrowserPairing> _pairings;

        /// <summary>Raised after a pairing is added or removed, on the thread that changed it.</summary>
        public event Action Changed;

        public BrowserPairingStore(string path, ILogger logger)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _logger = logger;
        }

        public IReadOnlyList<BrowserPairing> GetAll()
        {
            lock (_sync)
                return Load().ToList();
        }

        public BrowserPairing Find(Guid id)
        {
            lock (_sync)
                return Load().Find(pairing => pairing.Id == id);
        }

        public void Add(BrowserPairing pairing)
        {
            ArgumentNullException.ThrowIfNull(pairing);
            lock (_sync)
            {
                var pairings = Load();
                pairings.RemoveAll(item => item.Id == pairing.Id);
                pairings.Add(pairing);
                Save(pairings);
            }
            Changed?.Invoke();
        }

        public bool Remove(Guid id)
        {
            bool removed;
            lock (_sync)
            {
                var pairings = Load();
                removed = pairings.RemoveAll(item => item.Id == id) > 0;
                if (removed)
                    Save(pairings);
            }
            if (removed)
                Changed?.Invoke();
            return removed;
        }

        /// <summary>Records a connection; written at most once per hour per pairing.</summary>
        public void MarkUsed(Guid id, DateTimeOffset now)
        {
            lock (_sync)
            {
                var pairings = Load();
                var index = pairings.FindIndex(item => item.Id == id);
                if (index < 0 || now - pairings[index].LastUsedAt < LastUsedPrecision)
                    return;

                pairings[index] = pairings[index] with { LastUsedAt = now };
                Save(pairings);
            }
        }

        private List<BrowserPairing> Load()
        {
            if (_pairings != null)
                return _pairings;

            try
            {
                var encrypted = File.ReadAllBytes(_path);
                var plaintext = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                try
                {
                    _pairings = JsonSerializer.Deserialize<List<BrowserPairing>>(plaintext) ?? [];
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(plaintext);
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                _pairings = [];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
            {
                // The extensions pair again; nothing else depends on this file.
                _logger?.LogWarning(ex, "Unable to read the browser pairings from {Path}", _path);
                _pairings = [];
            }
            return _pairings;
        }

        private void Save(List<BrowserPairing> pairings)
        {
            var plaintext = JsonSerializer.SerializeToUtf8Bytes(pairings);
            byte[] encrypted;
            try
            {
                encrypted = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            File.WriteAllBytes(temporary, encrypted);
            File.Move(temporary, _path, overwrite: true);
            _pairings = pairings;
        }
    }
}
