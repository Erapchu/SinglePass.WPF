using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SinglePass.WPF.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

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
        private CancellationTokenSource _cancellationTokenSource;

        private AsyncRelayCommand _loadCredentialsCommand;
        public AsyncRelayCommand LoadCredentialsCommand => _loadCredentialsCommand ??= new AsyncRelayCommand(LoadCredentialsAsync);

        private RelayCommand<KeyEventArgs> _refreshCapsLockCommand;
        public RelayCommand<KeyEventArgs> RefreshCapsLockCommand => _refreshCapsLockCommand ??= new RelayCommand<KeyEventArgs>(RefreshCapsLock);

        [ObservableProperty]
        public partial bool Loading { get; set; }

        [ObservableProperty]
        public partial string HelperText { get; set; }

        public string HintText { get; }

        public bool IsCapsLockEnabled => Console.CapsLock;

        public string Password { get; set; }

        internal LoginWindowViewModel(Func<Task<IServiceProvider>> prepareServices, bool credentialsFileExists)
        {
            _prepareServices = prepareServices;
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

            ILogger<LoginWindowViewModel> logger = null;
            try
            {
                Loading = true;
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new();
                var cancellationToken = _cancellationTokenSource.Token;

                var services = await _prepareServices();
                cancellationToken.ThrowIfCancellationRequested();
                logger = services.GetRequiredService<ILogger<LoginWindowViewModel>>();
                var credentialsCryptoService = services.GetRequiredService<CredentialsCryptoService>();
                var result = await credentialsCryptoService.LoadCredentialsAsync(password);
                cancellationToken.ThrowIfCancellationRequested();

                if (result is CredentialLoadResult.Loaded or CredentialLoadResult.FileNotFound)
                {
                    credentialsCryptoService.SetPassword(password);
                    Accept?.Invoke();
                }
                else
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
        }

        private void RefreshCapsLock(KeyEventArgs args)
        {
            if (args is null || args.Key == Key.CapsLock)
                OnPropertyChanged(nameof(IsCapsLockEnabled));
        }
    }
}
