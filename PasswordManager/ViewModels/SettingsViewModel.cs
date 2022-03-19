using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Cloud.Enums;
using PasswordManager.Clouds.Services;
using PasswordManager.Helpers;
using PasswordManager.Models;
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
        private readonly CloudServiceProvider _cloudServiceProvider;
        private AsyncRelayCommand<CloudType> _loginCommand;
        private string _googleProfileUrl;
        private string _googleUserName;

        public BaseTheme ThemeMode
        {
            get => _appSettingsService.ThemeMode;
            set
            {
                _appSettingsService.ThemeMode = value;
                _appSettingsService.Save();
                OnPropertyChanged();
                _themeService.ThemeMode = value;
            }
        }

        public bool GoogleDriveEnabled
        {
            get => _appSettingsService.GoogleDriveEnabled;
            set
            {
                _appSettingsService.GoogleDriveEnabled = value;
                OnPropertyChanged();
            }
        }

        public string GoogleProfileUrl
        {
            get => _googleProfileUrl;
            set => SetProperty(ref _googleProfileUrl, value);
        }
        public string GoogleUserName
        {
            get => _googleUserName;
            set => SetProperty(ref _googleUserName, value);
        }

        public AsyncRelayCommand<CloudType> LoginCommand => _loginCommand ??= new AsyncRelayCommand<CloudType>(Login);

        private SettingsViewModel() { }

        public SettingsViewModel(
            ThemeService themeService,
            AppSettingsService appSettingsService,
            ILogger<SettingsViewModel> logger,
            CloudServiceProvider cloudServiceProvider)
        {
            _themeService = themeService;
            _appSettingsService = appSettingsService;
            _logger = logger;
            _cloudServiceProvider = cloudServiceProvider;

            Name = "Settings";
            ItemIndex = SettingsNavigationItemIndex;
            IconKind = PackIconKind.Settings;

            Task.Run(FetchStoragesUserInfo);
        }

        private async Task FetchStoragesUserInfo()
        {
            try
            {
                if (GoogleDriveEnabled)
                {
                    var cloudService = _cloudServiceProvider.GetCloudService(CloudType.GoogleDrive);
                    var googleUserInfo = await cloudService.GetUserInfo(CancellationToken.None);
                    GoogleProfileUrl = googleUserInfo.ProfileUrl;
                    GoogleUserName = googleUserInfo.UserName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
        }

        private async Task Login(CloudType cloudType)
        {
            var windowDialogName = MvvmHelper.MainWindowDialogName;
            var authorizing = false;
            switch (cloudType)
            {
                case CloudType.GoogleDrive:
                    authorizing = !GoogleDriveEnabled;
                    break;
            }

            try
            {
                if (authorizing)
                {
                    // Authorize
                    var processingControl = new ProcessingControl("Authorizing...", "Please, continue authorization or cancel it...", windowDialogName);
                    var token = processingControl.ViewModel.CancellationToken;
                    _ = DialogHost.Show(processingControl, windowDialogName); // Don't await dialog host

                    var cloudService = _cloudServiceProvider.GetCloudService(cloudType);
                    await cloudService.AuthorizationBroker.AuthorizeAsync(token);

                    _logger.LogInformation($"Authorization process to {cloudType} has been complete.");

                    // TODO: Check file after authorization?? and notify user about ability to download?
                    var googleUserInfo = await cloudService.GetUserInfo(token);
                    GoogleDriveEnabled = true;
                    await _appSettingsService.Save();

                    GoogleProfileUrl = googleUserInfo.ProfileUrl;
                    GoogleUserName = googleUserInfo.UserName;
                }
                else
                {
                    // TODO: Revoke

                    GoogleDriveEnabled = false;
                    await _appSettingsService.Save();
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
            finally
            {
                if (DialogHost.IsDialogOpen(windowDialogName))
                    DialogHost.Close(windowDialogName);
            }
        }
    }
}
