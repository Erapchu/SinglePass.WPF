using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Helpers;
using PasswordManager.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class LoginWindowViewModel : ObservableRecipient
    {
        #region Design time instance
        private static readonly Lazy<LoginWindowViewModel> _lazy = new Lazy<LoginWindowViewModel>(() => new LoginWindowViewModel(null, null));
        public static LoginWindowViewModel DesignTimeInstance => _lazy.Value;
        #endregion

        private readonly SettingsService _settingsService;
        private readonly ILogger<LoginWindowViewModel> _logger;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _credentialsFileExist;

        public event Action Accept;

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

        private string _hintText = "Password";
        public string HintText
        {
            get => _hintText;
            set => SetProperty(ref _hintText, value);
        }

        public string Password { get; set; }

        public LoginWindowViewModel(
            SettingsService settingsService,
            ILogger<LoginWindowViewModel> logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        public async Task LoadCredentialsAsync()
        {
            if (string.IsNullOrWhiteSpace(Password) || Password.Length < 8)
            {
                HelperText = "Minimum 8 characters";
                return;
            }
            else
            {
                HelperText = string.Empty;
            }

            try
            {
                Loading = true;
                if (!_credentialsFileExist)
                {
                    await _settingsService.SetNewPassword(Password);
                    Accept?.Invoke();
                }
                else
                {
                    _cancellationTokenSource?.Cancel();
                    _cancellationTokenSource = new();
                    var cancellationToken = _cancellationTokenSource.Token;

                    var loadingResult = await _settingsService.LoadCredentialsAsync(Password);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!loadingResult)
                    {
                        HelperText = "Password is incorrect";
                    }
                    else
                    {
                        Accept?.Invoke();
                    }
                }
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

        private async Task LoadingAsync()
        {
            _credentialsFileExist = await _settingsService.IsCredentialsFileExistAsync();
            if (!_credentialsFileExist)
            {
                HintText = "New password";
            }
        }

        private AsyncRelayCommand _loadCredentialsCommand;
        public AsyncRelayCommand LoadCredentialsCommand => _loadCredentialsCommand ??= new AsyncRelayCommand(LoadCredentialsAsync);

        private AsyncRelayCommand _loadingCommand;
        public AsyncRelayCommand LoadingCommand => _loadingCommand ??= new AsyncRelayCommand(LoadingAsync);
    }
}
