using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SinglePass.WPF.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SinglePass.WPF.ViewModels
{
    public class LoginWindowViewModel : ObservableRecipient
    {
        #region Design time instance
        private static readonly Lazy<LoginWindowViewModel> _lazy = new Lazy<LoginWindowViewModel>(() => new LoginWindowViewModel(null, null));
        public static LoginWindowViewModel DesignTimeInstance => _lazy.Value;
        #endregion

        public event Action Accept;

        private readonly CredentialsCryptoService _credentialsCryptoService;
        private readonly ILogger<LoginWindowViewModel> _logger;
        private CancellationTokenSource _cancellationTokenSource;

        private AsyncRelayCommand _loadCredentialsCommand;
        public AsyncRelayCommand LoadCredentialsCommand => _loadCredentialsCommand ??= new AsyncRelayCommand(LoadCredentialsAsync);

        private RelayCommand<KeyEventArgs> _refreshCapsLockCommand;
        public RelayCommand<KeyEventArgs> RefreshCapsLockCommand => _refreshCapsLockCommand ??= new RelayCommand<KeyEventArgs>(RefreshCapsLock);

        private bool _loading;
        public bool Loading
        {
            get => _loading;
            set => SetProperty(ref _loading, value);
        }

        private string _helperText;
        public string HelperText
        {
            get => _helperText;
            set => SetProperty(ref _helperText, value);
        }

        public string HintText { get; }

        public bool IsCapsLockEnabled => Console.CapsLock;

        public string Password { get; set; }

        public LoginWindowViewModel(
            CredentialsCryptoService credentialsCryptoService,
            ILogger<LoginWindowViewModel> logger)
        {
            _credentialsCryptoService = credentialsCryptoService;
            _logger = logger;
            HintText = credentialsCryptoService is not null && !credentialsCryptoService.CredentialsFileExists()
                ? SinglePass.Language.Properties.Resources.NewPassword
                : SinglePass.Language.Properties.Resources.Password;
        }

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

            try
            {
                Loading = true;
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new();
                var cancellationToken = _cancellationTokenSource.Token;

                var result = await _credentialsCryptoService.LoadCredentialsAsync(password);
                cancellationToken.ThrowIfCancellationRequested();

                if (result is CredentialLoadResult.Loaded or CredentialLoadResult.FileNotFound)
                {
                    _credentialsCryptoService.SetPassword(password);
                    Accept?.Invoke();
                }
                else
                    HelperText = SinglePass.Language.Properties.Resources.PasswordIsIncorrect;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
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
