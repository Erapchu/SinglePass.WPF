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

        private bool _loading;
        /// <summary>
        /// Indicates loading of some content.
        /// </summary>
        public bool Loading
        {
            get => _loading;
            set => SetProperty(ref _loading, value);
        }

        public bool Success { get; private set; }

        public LoginWindowViewModel(
            SettingsService settingsService,
            ILogger<LoginWindowViewModel> logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        private async Task LoadingAsync(CancellationToken cancellationToken)
        {
            try
            {
                Loading = true;
                await _settingsService.LoadCredentialsAsync();
                cancellationToken.ThrowIfCancellationRequested();
                Success = true;
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
        public AsyncRelayCommand LoadingCommand => _loadingCommand ??= new AsyncRelayCommand(LoadingAsync);
    }
}
