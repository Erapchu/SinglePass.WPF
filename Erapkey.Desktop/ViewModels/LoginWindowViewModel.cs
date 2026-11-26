using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Erapkey.Desktop.Services;
using Erapkey.Desktop.Services.WindowsHello;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Erapkey.Core.Localization;
using Erapkey.Core.Services;

namespace Erapkey.Desktop.ViewModels
{
    public partial class LoginWindowViewModel : ObservableRecipient
    {
        #region Design time instance
        private static readonly Lazy<LoginWindowViewModel> _lazy = new(() => new LoginWindowViewModel(null, true));
        public static LoginWindowViewModel DesignTimeInstance => _lazy.Value;
        #endregion

        public event Action Accept;

        private readonly Func<Task<IServiceProvider>> _prepareServices;
        private readonly bool _allowCreate;
        private CancellationTokenSource _cancellationTokenSource;

        private AsyncRelayCommand _loadCredentialsCommand;
        public AsyncRelayCommand LoadCredentialsCommand => _loadCredentialsCommand ??= new AsyncRelayCommand(LoadCredentialsAsync);

        private AsyncRelayCommand _unlockWithWindowsHelloCommand;
        public AsyncRelayCommand UnlockWithWindowsHelloCommand => _unlockWithWindowsHelloCommand ??= new AsyncRelayCommand(UnlockWithWindowsHelloAsync);

        private RelayCommand<KeyEventArgs> _refreshCapsLockCommand;
        public RelayCommand<KeyEventArgs> RefreshCapsLockCommand => _refreshCapsLockCommand ??= new RelayCommand<KeyEventArgs>(RefreshCapsLock);

        [ObservableProperty]
        public partial bool Loading { get; set; }

        [ObservableProperty]
        public partial string HelperText { get; set; }

        public string HintText { get; }

        public bool IsCapsLockEnabled => Console.CapsLock;

        public string Password { get; set; }

        /// <summary>Whether the vault can be unlocked with Windows Hello instead of the master password.</summary>
        [ObservableProperty]
        public partial bool WindowsHelloOffered { get; set; }

        /// <summary>The window over which Windows shows the Windows Hello prompt.</summary>
        internal IntPtr OwnerHandle { get; set; }

        /// <param name="windowsHelloOffered">
        /// Offer Windows Hello: it is turned on and windowsHello.dat holds the vault key from a master password typed less than
        /// 14 days ago (<see cref="WindowsHelloUnlock.IsOffered"/>).
        /// </param>
        internal LoginWindowViewModel(Func<Task<IServiceProvider>> prepareServices, bool credentialsFileExists, bool windowsHelloOffered = false)
        {
            _prepareServices = prepareServices;
            _allowCreate = !credentialsFileExists;
            WindowsHelloOffered = credentialsFileExists && windowsHelloOffered;
            HintText = !credentialsFileExists
                ? Erapkey.Language.Properties.Resources.NewPassword
                : Erapkey.Language.Properties.Resources.Password;
        }

        internal void CancelLogin() => _cancellationTokenSource?.Cancel();

        public async Task LoadCredentialsAsync()
        {
            var password = Password;
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                HelperText = Erapkey.Language.Properties.Resources.Minimum8Characters;
                return;
            }
            else
            {
                HelperText = string.Empty;
            }

            await UnlockAsync(service => service.LoadCredentialsAsync(password, _allowCreate), typed: true);
        }

        private async Task UnlockWithWindowsHelloAsync()
        {
            if (Loading)
                return;

            HelperText = string.Empty;
            ILogger<LoginWindowViewModel> logger = null;
            try
            {
                Loading = true;
                var cancellationToken = RestartCancellation();
                var services = await _prepareServices();
                cancellationToken.ThrowIfCancellationRequested();
                logger = services.GetRequiredService<ILogger<LoginWindowViewModel>>();

                var (outcome, vaultKey) = await services.GetRequiredService<WindowsHelloUnlock>().UnlockAsync(OwnerHandle);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (outcome == WindowsHelloOutcome.Unlocked)
                    {
                        Loading = false;
                        await UnlockAsync(service => service.LoadCredentialsAsync(vaultKey), typed: false);
                        return;
                    }
                }
                finally
                {
                    if (vaultKey is not null)
                        System.Security.Cryptography.CryptographicOperations.ZeroMemory(vaultKey);
                }

                switch (outcome)
                {
                    case WindowsHelloOutcome.KeyLost:
                        WindowsHelloOffered = false;
                        HelperText = LocalizationManager.Instance["WindowsHelloKeyLost"];
                        break;
                    case WindowsHelloOutcome.Failed:
                        HelperText = LocalizationManager.Instance["WindowsHelloFailed"];
                        break;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger?.LogError(ex, string.Empty);
            }
            finally
            {
                Loading = false;
            }
        }

        /// <summary>
        /// Opens the vault with <paramref name="load"/>: with the typed master password, or with the vault key from
        /// Windows Hello (<paramref name="typed"/> false).
        /// </summary>
        private async Task<bool> UnlockAsync(Func<CredentialsCryptoService, Task<CredentialLoadResult>> load, bool typed)
        {
            ILogger<LoginWindowViewModel> logger = null;
            try
            {
                Loading = true;
                var cancellationToken = RestartCancellation();

                var services = await _prepareServices();
                cancellationToken.ThrowIfCancellationRequested();
                logger = services.GetRequiredService<ILogger<LoginWindowViewModel>>();
                var credentialsCryptoService = services.GetRequiredService<CredentialsCryptoService>();
                var result = await load(credentialsCryptoService);
                if (cancellationToken.IsCancellationRequested)
                    await credentialsCryptoService.ClearAsync();
                cancellationToken.ThrowIfCancellationRequested();

                switch (result)
                {
                    case CredentialLoadResult.Loaded:
                        Accept?.Invoke();
                        return true;
                    case CredentialLoadResult.NewerVersion:
                        HelperText = LocalizationManager.Instance["VaultNewerVersion"];
                        break;
                    case CredentialLoadResult.WrongKey:
                        // The stored vault key no longer opens the vault: offer only the master password from now on.
                        services.GetRequiredService<WindowsHelloUnlock>().ForgetStoredKey();
                        WindowsHelloOffered = false;
                        HelperText = LocalizationManager.Instance["WindowsHelloPasswordOutdated"];
                        break;
                    default:
                        HelperText = typed
                            ? Erapkey.Language.Properties.Resources.PasswordIsIncorrect
                            : LocalizationManager.Instance["WindowsHelloFailed"];
                        break;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                // Startup failures are reported by App.
                logger?.LogError(ex, string.Empty);
            }
            finally
            {
                Loading = false;
            }

            return false;
        }

        private CancellationToken RestartCancellation()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new();
            return _cancellationTokenSource.Token;
        }

        private void RefreshCapsLock(KeyEventArgs args)
        {
            if (args is null || args.Key == Key.CapsLock)
                OnPropertyChanged(nameof(IsCapsLockEnabled));
        }
    }
}
