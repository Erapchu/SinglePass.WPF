using Microsoft.Extensions.Logging;
using SinglePass.Core.Models;
using SinglePass.Core.Services.Browser;
using SinglePass.WPF.Views.Windows;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SinglePass.WPF.Services
{
    /// <summary>
    /// Gives the browser server the entries of the unlocked vault and asks the user to allow a new browser.
    /// While the vault is locked an <c>unlock</c> request shows the unlock window and waits a minute, as the SSH agent does.
    /// </summary>
    internal sealed class BrowserBackend : IBrowserBackend
    {
        private static readonly TimeSpan UnlockWait = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan UnlockPromptInterval = TimeSpan.FromSeconds(30);

        private readonly App _app;
        private readonly ILogger<BrowserBackend> _logger;
        private readonly SemaphoreSlim _pairingPrompt = new(1, 1);
        private DateTime _lastUnlockPrompt = DateTime.MinValue;

        public BrowserBackend(ILogger<BrowserBackend> logger)
        {
            _app = (App)Application.Current;
            _logger = logger;
        }

        private Dispatcher Dispatcher => _app.Dispatcher;

        public async Task<IReadOnlyList<Credential>> GetCredentialsAsync(CancellationToken cancellationToken)
        {
            var vault = await Dispatcher.InvokeAsync(() => _app.UnlockedVault, DispatcherPriority.Normal, cancellationToken);
            return vault?.Credentials;
        }

        public async Task<bool> UnlockAsync(CancellationToken cancellationToken)
        {
            if (await GetCredentialsAsync(cancellationToken) != null)
                return true;

            await Dispatcher.InvokeAsync(() =>
            {
                // An extension that retries must not keep opening the window.
                if (DateTime.UtcNow - _lastUnlockPrompt < UnlockPromptInterval)
                    return;

                _lastUnlockPrompt = DateTime.UtcNow;
                _logger.LogInformation("The browser extension asks to unlock the vault");
                _app.RequestUnlock();
            });

            var deadline = DateTime.UtcNow + UnlockWait;
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(500, cancellationToken);
                if (await GetCredentialsAsync(cancellationToken) != null)
                    return true;
            }
            return false;
        }

        public async Task<bool> ConfirmPairingAsync(BrowserPairingRequest request, CancellationToken cancellationToken)
        {
            // One question at a time.
            await _pairingPrompt.WaitAsync(cancellationToken);
            try
            {
                return await Dispatcher.InvokeAsync(() => AskAsync(request, cancellationToken)).Task.Unwrap();
            }
            finally
            {
                _pairingPrompt.Release();
            }
        }

        public async Task OpenEntryAsync(Guid entryId, CancellationToken cancellationToken)
        {
            await Dispatcher.InvokeAsync(() => _app.ShowEntry(entryId), DispatcherPriority.Normal, cancellationToken);
        }

        private static Task<bool> AskAsync(BrowserPairingRequest request, CancellationToken cancellationToken)
        {
            var result = new TaskCompletionSource<bool>();
            var window = new BrowserPairingWindow(request);
            var registration = cancellationToken.Register(() => window.Dispatcher.BeginInvoke(window.Close));
            window.Closed += (_, _) =>
            {
                registration.Dispose();
                result.TrySetResult(window.Allowed);
            };
            window.Show();
            window.Activate();
            return result.Task;
        }
    }
}
