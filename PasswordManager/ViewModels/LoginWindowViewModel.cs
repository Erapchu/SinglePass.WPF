using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Services;
using System;
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

        public bool Success { get; private set; }
        public string Password { get; set; }

        public LoginWindowViewModel(
            SettingsService settingsService,
            ILogger<LoginWindowViewModel> logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        public async Task LoadingCredentialsAsync()
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
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new();
                var cancellationToken = _cancellationTokenSource.Token;

                Loading = true;
                await _settingsService.LoadCredentialsAsync(Password);
                cancellationToken.ThrowIfCancellationRequested();
                Success = true;
                Accept?.Invoke();
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

        private AsyncRelayCommand _loadingCommand;
        public AsyncRelayCommand LoadingCommand => _loadingCommand ??= new AsyncRelayCommand(LoadingCredentialsAsync);
    }
}
