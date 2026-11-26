using Microsoft.Extensions.Logging;
using Erapkey.Core.Models;
using Erapkey.Core.Services;
using Erapkey.Views.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Erapkey.Services
{
    /// <summary>
    /// Gives the SSH agent the SSH key entries of the unlocked vault and asks the user before each use.
    /// While the vault is locked the agent has no keys: a request then shows the unlock window and waits
    /// a minute for the user. An answer "remember" lasts until the vault is locked.
    /// </summary>
    internal sealed class SshAgentKeyStore : ISshAgentKeyStore
    {
        private static readonly TimeSpan UnlockWait = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan UnlockPromptInterval = TimeSpan.FromSeconds(30);

        private readonly App _app;
        private readonly ILogger<SshAgentKeyStore> _logger;
        private readonly SemaphoreSlim _prompt = new(1, 1);
        private readonly object _sync = new();
        private readonly HashSet<string> _approvals = new(StringComparer.OrdinalIgnoreCase);
        private CredentialsCryptoService _approvalsVault;
        private DateTime _lastUnlockPrompt = DateTime.MinValue;

        public SshAgentKeyStore(ILogger<SshAgentKeyStore> logger)
        {
            _app = (App)Application.Current;
            _logger = logger;
        }

        private Dispatcher Dispatcher => _app.Dispatcher;

        public async Task<IReadOnlyList<SshAgentIdentity>> GetIdentitiesAsync(PipeClient client, CancellationToken cancellationToken)
        {
            var vault = await GetVaultAsync(waitForUnlock: true, cancellationToken);
            if (vault == null)
                return [];

            return vault.Credentials
                .Where(credential => credential.Type == CredentialType.SshKey)
                .Select(credential => (credential, blob: SshAgent.TryGetPublicKeyBlob(credential.SshKey)))
                .Where(item => item.blob != null)
                .Select(item => new SshAgentIdentity(item.credential.Id, item.credential.NameField.Value, item.blob))
                .ToList();
        }

        public async Task<SshKeyData> AuthorizeAsync(SshAgentIdentity identity, PipeClient client, CancellationToken cancellationToken)
        {
            var vault = await GetVaultAsync(waitForUnlock: false, cancellationToken);
            if (vault?.Credentials.Find(item => item.Id == identity.CredentialId && item.Type == CredentialType.SshKey) is not { } credential)
                return null;

            // The program and the programs that started it: allowing "ssh.exe" alone would allow any program running ssh.
            var approval = identity.CredentialId + "|" + string.Join("|", client.Chain.Select(process => process.Path ?? process.Name));
            if (IsApproved(vault, approval))
                return credential.SshKey.Clone();

            // One question at a time; a second request for the same key may be answered by the first.
            await _prompt.WaitAsync(cancellationToken);
            try
            {
                if (IsApproved(vault, approval))
                    return credential.SshKey.Clone();

                var (allowed, remember) = await Dispatcher.InvokeAsync(() => AskAsync(credential.NameField.Value, client)).Task.Unwrap();
                if (!allowed)
                    return null;

                if (remember)
                {
                    lock (_sync)
                    {
                        if (ReferenceEquals(_approvalsVault, vault))
                            _approvals.Add(approval);
                    }
                }
                return credential.SshKey.Clone();
            }
            finally
            {
                _prompt.Release();
            }
        }

        private bool IsApproved(CredentialsCryptoService vault, string approval)
        {
            lock (_sync)
            {
                // Each unlock creates a new vault service, so approvals end with the lock.
                if (!ReferenceEquals(_approvalsVault, vault))
                {
                    _approvalsVault = vault;
                    _approvals.Clear();
                }
                return _approvals.Contains(approval);
            }
        }

        private static Task<(bool Allowed, bool Remember)> AskAsync(string keyName, PipeClient client)
        {
            var result = new TaskCompletionSource<(bool, bool)>();
            var window = new SshAgentConfirmWindow(keyName, client);
            window.Closed += (_, _) => result.TrySetResult((window.Allowed, window.Remember));
            window.Show();
            window.Activate();
            return result.Task;
        }

        private async Task<CredentialsCryptoService> GetVaultAsync(bool waitForUnlock, CancellationToken cancellationToken)
        {
            var vault = await Dispatcher.InvokeAsync(() => _app.UnlockedVault);
            if (vault != null || !waitForUnlock)
                return vault;

            await Dispatcher.InvokeAsync(() =>
            {
                // A program that retries in a loop must not keep opening the window.
                if (DateTime.UtcNow - _lastUnlockPrompt < UnlockPromptInterval)
                    return;

                _lastUnlockPrompt = DateTime.UtcNow;
                _logger.LogInformation("SSH agent request while the vault is locked; asking to unlock");
                _app.RequestUnlock();
            });

            var deadline = DateTime.UtcNow + UnlockWait;
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(500, cancellationToken);
                vault = await Dispatcher.InvokeAsync(() => _app.UnlockedVault);
                if (vault != null)
                    return vault;
            }
            return null;
        }
    }
}
