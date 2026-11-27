using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Erapkey.Core.Services.Browser
{
    /// <summary>
    /// Answers the browser extension through <c>Erapkey.NativeHost.exe</c> on <c>\\.\pipe\erapkey-browser-&lt;SID&gt;</c>
    /// (docs/browser-extension.md). Connections come only from the native host of this application; requests are
    /// answered only inside a session of a paired extension.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class BrowserPipeServer : NamedPipeServer
    {
        public const int ProtocolVersion = 1;
        public const string NativeHostFileName = "Erapkey.NativeHost.exe";

        private readonly IBrowserBackend _backend;
        private readonly BrowserPairingStore _pairings;
        private readonly WebsiteMatcher _matcher;
        private readonly Func<PipeClient, bool> _isAllowedClient;
        private readonly ConcurrentDictionary<BrowserConnection, byte> _connections = new();

        protected override string DisplayName => "Browser server";

        public BrowserPipeServer(IBrowserBackend backend, BrowserPairingStore pairings, ILogger<BrowserPipeServer> logger)
            : this(backend, pairings, new WebsiteMatcher(PublicSuffixList.Default), logger,
                UserPipeName("erapkey-browser-"), IsNativeHostOfThisApplication)
        {
        }

        internal BrowserPipeServer(IBrowserBackend backend, BrowserPairingStore pairings, WebsiteMatcher matcher,
            ILogger logger, string pipeName, Func<PipeClient, bool> isAllowedClient)
            : base(pipeName, logger)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _pairings = pairings ?? throw new ArgumentNullException(nameof(pairings));
            _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            _isAllowedClient = isAllowedClient ?? throw new ArgumentNullException(nameof(isAllowedClient));
        }

        internal IBrowserBackend Backend => _backend;
        internal BrowserPairingStore Pairings => _pairings;
        internal WebsiteMatcher Matcher => _matcher;
        internal ILogger Log => Logger;

        /// <summary>Tells every connected extension that the vault was locked; they drop what they showed.</summary>
        public void NotifyLocked() => Broadcast("locked");

        public void NotifyUnlocked() => Broadcast("unlocked");

        public void NotifyVaultChanged() => Broadcast("vaultChanged");

        /// <summary>Closes the sessions of a removed pairing at once.</summary>
        public void Disconnect(Guid pairingId)
        {
            foreach (var connection in _connections.Keys.Where(c => c.PairingId == pairingId))
                connection.Close();
        }

        protected override async Task ServeAsync(Stream pipe, PipeClient client, CancellationToken cancellationToken)
        {
            if (!_isAllowedClient(client))
            {
                Logger?.LogWarning("Browser server refused {Client}: not the native host of this application", client.DisplayName);
                return;
            }

            var connection = new BrowserConnection(this, pipe, client);
            _connections.TryAdd(connection, 0);
            try
            {
                await connection.RunAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _connections.TryRemove(connection, out _);
            }
        }

        protected override void OnStopped()
        {
            foreach (var connection in _connections.Keys)
                connection.Close();
        }

        private void Broadcast(string name)
        {
            foreach (var connection in _connections.Keys)
                _ = connection.SendEventAsync(name);
        }

        /// <summary>The browser that started the native host: the first process above it that is not cmd.exe.</summary>
        internal static string GetBrowserName(PipeClient client) =>
            client.Chain.Skip(1).FirstOrDefault(p => !string.Equals(p.Name, "cmd.exe", StringComparison.OrdinalIgnoreCase))?.Name ?? "?";

        private static bool IsNativeHostOfThisApplication(PipeClient client)
        {
            var path = client.Chain.Count > 0 ? client.Chain[0].Path : null;
            var expected = Path.Combine(AppContext.BaseDirectory, NativeHostFileName);
            return path != null && string.Equals(Path.GetFullPath(path), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase);
        }
    }
}
