using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Helpers;
using PasswordManager.Services;
using PasswordManager.Views;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class SettingsViewModel : NavigationItemViewModel
    {
        #region Design time instance
        private static readonly Lazy<SettingsViewModel> _lazy = new(GetDesignTimeVM);
        public static SettingsViewModel DesignTimeInstance => _lazy.Value;

        private static SettingsViewModel GetDesignTimeVM()
        {
            var vm = new SettingsViewModel();
            return vm;
        }
        #endregion

        private readonly ThemeService _themeService;
        private readonly AppSettingsService _appSettingsService;
        private readonly ILogger<SettingsViewModel> _logger;
        private AsyncRelayCommand _googleLoginCommand;

        public BaseTheme ThemeMode
        {
            get => _appSettingsService.ThemeMode;
            set
            {
                _appSettingsService.ThemeMode = value;
                OnPropertyChanged();
                _themeService.ThemeMode = value;
            }
        }

        public AsyncRelayCommand GoogleLoginCommand => _googleLoginCommand ??= new AsyncRelayCommand(GoogleLogin);

        private SettingsViewModel() { }

        public SettingsViewModel(
            ThemeService themeService,
            AppSettingsService appSettingsService,
            ILogger<SettingsViewModel> logger)
        {
            _themeService = themeService;
            _appSettingsService = appSettingsService;
            _logger = logger;

            Name = "Settings";
            ItemIndex = SettingsNavigationItemIndex;
            IconKind = PackIconKind.Settings;
        }

        private async Task GoogleLogin()
        {
            try
            {
                var windowDialogName = MvvmHelper.MainWindowDialogName;
                var processingControl = new ProcessingControl("Authorizing...", windowDialogName);
                var token = processingControl.ViewModel.CancellationToken;
                _ = DialogHost.Show(processingControl, windowDialogName); // Don't await dialog host

                // TODO: Provide secrets with configuration from .json
                var clientSecrets = new ClientSecrets()
                {
                    ClientId = "",
                    ClientSecret = ""
                };

                // TODO: Save userId with DPAPI in separate file
                //userId = userId ?? string.Format("TranquiloPassMan_{0}", Guid.NewGuid().ToString("N"));
                var userId = $"TranquiloPassMan_{Guid.NewGuid():N}";

                _logger.LogInformation("Start authorization process to Google Drive...");

                var userCredential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    clientSecrets,
                    new[] { DriveService.Scope.DriveFile },
                    userId,
                    token);

                _logger.LogInformation("Authorization process to Google Drive has been complete.");

                if (userCredential != null)
                {
                    // Close dialog
                    DialogHost.Close(windowDialogName);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Authorization process to Google Drive has been cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
        }
    }
}
