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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PasswordManager.ViewModels
{
    public class CloudSyncViewModel : NavigationItemViewModel
    {
        #region Design time instance
        private static readonly Lazy<CloudSyncViewModel> _lazy = new(GetDesignTimeVM);
        public static CloudSyncViewModel DesignTimeInstance => _lazy.Value;

        private static CloudSyncViewModel GetDesignTimeVM()
        {
            var vm = new CloudSyncViewModel();
            return vm;
        }
        #endregion

        private readonly AppSettingsService _appSettingsService;
        private readonly CloudServiceProvider _cloudServiceProvider;
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly CredentialsCryptoService _credentialsCryptoService;
        private readonly CryptoService _cryptoService;
        private readonly SyncService _syncService;

        private bool _syncProcessing;
        private bool _fetchingUserInfo;
        private ImageSource _googleProfileImage;
        private string _googleUserName;

        private AsyncRelayCommand<CloudType> _syncCommand;
        private AsyncRelayCommand<CloudType> _loginCommand;

        public event Action SyncCompleted;

        public bool GoogleDriveEnabled
        {
            get => _appSettingsService.GoogleDriveEnabled;
            set
            {
                _appSettingsService.GoogleDriveEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool SyncProcessing
        {
            get => _syncProcessing;
            set => SetProperty(ref _syncProcessing, value);
        }

        public bool FetchingUserInfo
        {
            get => _fetchingUserInfo;
            set => SetProperty(ref _fetchingUserInfo, value);
        }

        public ImageSource GoogleProfileImage
        {
            get => _googleProfileImage;
            set => SetProperty(ref _googleProfileImage, value);
        }

        public string GoogleUserName
        {
            get => _googleUserName;
            set => SetProperty(ref _googleUserName, value);
        }

        public AsyncRelayCommand<CloudType> LoginCommand => _loginCommand ??= new AsyncRelayCommand<CloudType>(Login);
        public AsyncRelayCommand<CloudType> SyncCommand => _syncCommand ??= new AsyncRelayCommand<CloudType>(SyncCredentials);

        private CloudSyncViewModel() { }

        public CloudSyncViewModel(
            AppSettingsService appSettingsService,
            CloudServiceProvider cloudServiceProvider,
            IHttpClientFactory httpClientFactory,
            CredentialsCryptoService credentialsCryptoService,
            CryptoService cryptoService,
            SyncService syncService)
        {
            Name = "Cloud sync";
            IconKind = PackIconKind.CloudOutline;

            _appSettingsService = appSettingsService;
            _cloudServiceProvider = cloudServiceProvider;
            _httpClientFactory = httpClientFactory;
            _credentialsCryptoService = credentialsCryptoService;
            _cryptoService = cryptoService;
            _syncService = syncService;
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

        internal async Task FetchUserInfoIfRequired()
        {
            try
            {
                if (GoogleDriveEnabled
                    && !FetchingUserInfo
                    && GoogleProfileImage is null
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

        private async Task FetchUserInfoFromCloud(CloudType cloudType, CancellationToken cancellationToken)
        {
            try
            {
                FetchingUserInfo = true;
                var cloudService = _cloudServiceProvider.GetCloudService(cloudType);
                var userInfo = await cloudService.GetUserInfo(cancellationToken);

                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Get, userInfo.ProfileUrl);
                using var response = await client.SendAsync(request, cancellationToken);
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                switch (cloudType)
                {
                    case CloudType.GoogleDrive:
                        GoogleProfileImage = bitmapImage;
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

        private void ClearUserInfo(CloudType cloudType)
        {
            switch (cloudType)
            {
                case CloudType.GoogleDrive:
                    GoogleProfileImage = null;
                    GoogleUserName = null;
                    break;
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
                            windowDialogName,
                            true);

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
                            var mergeResult = await _credentialsCryptoService.Merge(cloudCredentials);
                            await MaterialMessageBox.ShowAsync(
                                "Credentials successfully merged",
                                mergeResult.ToString(),
                                MaterialMessageBoxButtons.OK,
                                windowDialogName);
                            break;
                        case MaterialDialogResult.No:
                            // Replace
                            await _credentialsCryptoService.Replace(cloudCredentials);
                            await MaterialMessageBox.ShowAsync(
                                "Credentials successfully replaced",
                                "Please, check out them",
                                MaterialMessageBoxButtons.OK,
                                windowDialogName);
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
