using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SinglePass.WPF.Services;
using SinglePass.WPF.Services.WindowsHello;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using SinglePass.Core.Localization;
using SinglePass.Core.Services;

namespace SinglePass.WPF.ViewModels
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

        internal LoginWindowViewModel(Func<Task<IServiceProvider>> prepareServices, bool credentialsFileExists)
        {
            _prepareServices = prepareServices;
            _allowCreate = !credentialsFileExists;
            WindowsHelloOffered = credentialsFileExists && WindowsHelloUnlock.IsOffered();
            HintText = !credentialsFileExists
                ? SinglePass.Language.Properties.Resources.NewPassword
                : SinglePass.Language.Properties.Resources.Password;
        }

        internal void CancelLogin() => _cancellationTokenSource?.Cancel();

        public async Task LoadCredentialsAsync()
        {
            var password = Password;
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                HelperText = SinglePass.Language.Properties.Resources.Minimum8Characters;
                return;
            }
            else
            {
                HelperText = string.Empty;
            }

            await UnlockAsync(password, typed: true);
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

                var (outcome, password) = await services.GetRequiredService<WindowsHelloUnlock>().UnlockAsync(OwnerHandle);
                cancellationToken.ThrowIfCancellationRequested();
                switch (outcome)
                {
                    case WindowsHelloOutcome.Unlocked:
                        Loading = false;
                        if (!await UnlockAsync(password, typed: false))
                            HelperText = LocalizationManager.Instance["WindowsHelloPasswordOutdated"];
                        break;
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
        /// Opens the vault with <paramref name="password"/>. A password the user typed also refreshes the copy
        /// kept for Windows Hello unlock, if it is turned on.
        /// </summary>
        private async Task<bool> UnlockAsync(string password, bool typed)
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
                var result = await credentialsCryptoService.LoadCredentialsAsync(password, _allowCreate);
                if (cancellationToken.IsCancellationRequested)
                    await credentialsCryptoService.ClearAsync();
                cancellationToken.ThrowIfCancellationRequested();

                if (result == CredentialLoadResult.Loaded)
                {
                    credentialsCryptoService.SetPassword(password);
                    if (typed)
                        services.GetRequiredService<WindowsHelloUnlock>().Remember(password);
                    Accept?.Invoke();
                    return true;
                }

                if (typed)
                    HelperText = SinglePass.Language.Properties.Resources.PasswordIsIncorrect;
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
