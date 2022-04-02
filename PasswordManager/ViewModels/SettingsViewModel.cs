using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Cloud.Enums;
using PasswordManager.Clouds.Services;
using PasswordManager.Helpers;
using PasswordManager.Models;
using PasswordManager.Services;
using PasswordManager.Views;
using PasswordManager.Views.InputBox;
using PasswordManager.Views.MessageBox;
using System;
using System.Collections.Generic;
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
            vm.FetchingUserInfo = true;
            return vm;
        }
        #endregion

        private readonly ThemeService _themeService;
        private readonly AppSettingsService _appSettingsService;
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly CloudServiceProvider _cloudServiceProvider;
        private readonly CryptoService _cryptoService;
        private readonly CredentialsCryptoService _credentialsCryptoService;
        private readonly SyncService _syncService;

        private AsyncRelayCommand<CloudType> _loginCommand;
        private AsyncRelayCommand<CloudType> _syncCommand;
        private string _googleProfileUrl;
        private string _googleUserName;
        private bool _fetchingUserInfo;
        private bool _syncProcessing;

        public event Action SyncCompleted;

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

        public bool FetchingUserInfo
        {
            get => _fetchingUserInfo;
            set => SetProperty(ref _fetchingUserInfo, value);
        }

        public bool SyncProcessing
        {
            get => _syncProcessing;
            set => SetProperty(ref _syncProcessing, value);
        }

        public AsyncRelayCommand<CloudType> LoginCommand => _loginCommand ??= new AsyncRelayCommand<CloudType>(Login);

        public AsyncRelayCommand<CloudType> SyncCommand => _syncCommand ??= new AsyncRelayCommand<CloudType>(SyncCredentials);

        private SettingsViewModel() { }

        public SettingsViewModel(
            ThemeService themeService,
            AppSettingsService appSettingsService,
            ILogger<SettingsViewModel> logger,
            CloudServiceProvider cloudServiceProvider,
            CryptoService cryptoService,
            SyncService syncService,
            CredentialsCryptoService credentialsCryptoService)
        {
            Name = "Settings";
            ItemIndex = SettingsNavigationItemIndex;
            IconKind = PackIconKind.Settings;

            _themeService = themeService;
            _appSettingsService = appSettingsService;
            _logger = logger;
            _cloudServiceProvider = cloudServiceProvider;
            _cryptoService = cryptoService;
            _credentialsCryptoService = credentialsCryptoService;
            _syncService = syncService;
        }

        internal async Task FetchUserInfoIfRequired()
        {
            try
            {
                if (GoogleDriveEnabled
                    && !FetchingUserInfo
                    && string.IsNullOrWhiteSpace(GoogleProfileUrl)
                    && string.IsNullOrWhiteSpace(GoogleUserName))
                {
                    await FetchUserInfoFromCloud(CloudType.GoogleDrive, CancellationToken.None);
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
            var cloudService = _cloudServiceProvider.GetCloudService(cloudType);

            try
            {
                if (authorizing)
                {
                    // Authorize
                    var processingControl = new ProcessingControl("Authorizing...", "Please, continue authorization or cancel it.", windowDialogName);
                    var token = processingControl.ViewModel.CancellationToken;
                    _ = DialogHost.Show(processingControl, windowDialogName); // Don't await dialog host

                    await cloudService.AuthorizationBroker.AuthorizeAsync(token);
                    _logger.LogInformation($"Authorization process to {cloudType} has been complete.");
                    GoogleDriveEnabled = true;
                    await _appSettingsService.Save();

                    _ = FetchUserInfoFromCloud(cloudType, CancellationToken.None); // Don't await set user info for now
                }
                else
                {
                    // Revoke
                    var processingControl = new ProcessingControl("Signing out...", "Please, wait.", windowDialogName);
                    var token = processingControl.ViewModel.CancellationToken;
                    _ = DialogHost.Show(processingControl, windowDialogName); // Don't await dialog host

                    await cloudService.AuthorizationBroker.RevokeToken(token);

                    GoogleDriveEnabled = false;
                    await _appSettingsService.Save();

                    ClearUserInfo(cloudType);
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

        private void ClearUserInfo(CloudType cloudType)
        {
            switch (cloudType)
            {
                case CloudType.GoogleDrive:
                    GoogleProfileUrl = null;
                    GoogleUserName = null;
                    break;
            }
        }

        private async Task FetchUserInfoFromCloud(CloudType cloudType, CancellationToken cancellationToken)
        {
            try
            {
                FetchingUserInfo = true;
                var cloudService = _cloudServiceProvider.GetCloudService(cloudType);
                var userInfo = await cloudService.GetUserInfo(cancellationToken);
                switch (cloudType)
                {
                    case CloudType.GoogleDrive:
                        GoogleProfileUrl = userInfo.ProfileUrl;
                        GoogleUserName = userInfo.UserName;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
            finally
            {
                FetchingUserInfo = false;
            }
        }

        private async Task SyncCredentials(CloudType cloudType)
        {
            try
            {
                SyncProcessing = true;

                var cloudService = _cloudServiceProvider.GetCloudService(cloudType);
                var windowDialogName = MvvmHelper.MainWindowDialogName;
                using var fileStream = await cloudService.Download(Helpers.Constants.PasswordsFileName, CancellationToken.None);

                if (fileStream != null)
                {
                    // File is here
                    var result = await MaterialMessageBox.ShowAsync(
                        "Merge existing credentials?",
                        "Yes - merge\r\nNo - replace from cloud\r\nOr cancel operation",
                        MaterialMessageBoxButtons.YesNoCancel,
                        windowDialogName,
                        PackIconKind.QuestionMark);

                    if (result == MaterialDialogResult.Cancel)
                        return;

                    var success = false;
                    List<Credential> cloudCredentials = null;
                    do
                    {
                        var cloudPassword = await MaterialInputBox.ShowAsync(
                            "Input password of cloud file",
                            "Password",
                            windowDialogName);

                        if (cloudPassword is null)
                            return;

                        try
                        {
                            cloudCredentials = _cryptoService.DecryptFromStream<List<Credential>>(fileStream, cloudPassword);
                            success = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, string.Empty);
                        }
                    }
                    while (!success);

                    switch (result)
                    {
                        case MaterialDialogResult.Yes:
                            // Merge
                            await _credentialsCryptoService.Merge(cloudCredentials);
                            break;
                        case MaterialDialogResult.No:
                            // Replace
                            await _credentialsCryptoService.Replace(cloudCredentials);
                            break;
                    }
                }
                else
                {
                    // File doesn't exists, just sync
                    await _syncService.Synchronize();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
            finally
            {
                SyncCompleted?.Invoke();
                SyncProcessing = false;
            }
        }
    }
}
