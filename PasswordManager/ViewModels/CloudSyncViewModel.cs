using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Cloud.Enums;
using PasswordManager.Clouds.Services;
using PasswordManager.Helpers;
using PasswordManager.Services;
using PasswordManager.Settings;
using PasswordManager.Views;
using PasswordManager.Views.InputBox;
using PasswordManager.Views.MessageBox;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PasswordManager.ViewModels
{
    public class CloudSyncViewModel : ObservableRecipient
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
        private readonly ILogger<CloudSyncViewModel> _logger;
        private readonly ImageService _imageService;
        private readonly SyncService _syncService;

        private bool _mergeProcessing;
        private bool _uploadProcessing;
        private bool _fetchingUserInfo;
        private ImageSource _googleProfileImage;
        private string _googleUserName;
        private string _syncStage;

        private AsyncRelayCommand<CloudType> _syncCommand;
        private AsyncRelayCommand<CloudType> _loginCommand;
        private AsyncRelayCommand<CloudType> _uploadCommand;
        private AsyncRelayCommand _loadingCommand;

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

        public bool MergeProcessing
        {
            get => _mergeProcessing;
            set => SetProperty(ref _mergeProcessing, value);
        }

        public bool UploadProcessing
        {
            get => _uploadProcessing;
            set => SetProperty(ref _uploadProcessing, value);
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

        public string SyncState
        {
            get => _syncStage;
            set => SetProperty(ref _syncStage, value);
        }

        public AsyncRelayCommand<CloudType> LoginCommand => _loginCommand ??= new AsyncRelayCommand<CloudType>(Login);
        public AsyncRelayCommand<CloudType> SyncCommand => _syncCommand ??= new AsyncRelayCommand<CloudType>(SyncCredentials);
        public AsyncRelayCommand<CloudType> UploadCommand => _uploadCommand ??= new AsyncRelayCommand<CloudType>(UploadCredentials);
        public AsyncRelayCommand LoadingCommand => _loadingCommand ??= new AsyncRelayCommand(LoadingAsync);

        private CloudSyncViewModel() { }

        public CloudSyncViewModel(
            AppSettingsService appSettingsService,
            CloudServiceProvider cloudServiceProvider,
            ImageService imageService,
            SyncService syncService,
            ILogger<CloudSyncViewModel> logger)
        {
            _appSettingsService = appSettingsService;
            _cloudServiceProvider = cloudServiceProvider;
            _imageService = imageService;
            _syncService = syncService;
            _logger = logger;

            _syncService.SyncStateChanged += SyncService_SyncStateChanged;
        }

        private void SyncService_SyncStateChanged(string syncState)
        {
            SyncState = syncState;
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

        internal Task FetchUserInfoIfRequired()
        {
            try
            {
                if (GoogleDriveEnabled
                    && !FetchingUserInfo
                    && GoogleProfileImage is null
                    && string.IsNullOrWhiteSpace(GoogleUserName))
                {
                    return FetchUserInfoFromCloud(CloudType.GoogleDrive, CancellationToken.None);
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
                return Task.CompletedTask;
            }
        }

        private async Task FetchUserInfoFromCloud(CloudType cloudType, CancellationToken cancellationToken)
        {
            if (FetchingUserInfo)
                return;

            try
            {
                FetchingUserInfo = true;
                var cloudService = _cloudServiceProvider.GetCloudService(cloudType);
                var userInfo = await cloudService.GetUserInfo(cancellationToken);

                switch (cloudType)
                {
                    case CloudType.GoogleDrive:
                        GoogleUserName = userInfo.UserName;
                        GoogleProfileImage = await _imageService.GetImageAsync(userInfo.ProfileUrl, cancellationToken);
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
            if (MergeProcessing)
                return;

            try
            {
                MergeProcessing = true;
                var windowDialogName = MvvmHelper.MainWindowDialogName;
                var mergeResult = await _syncService.Synchronize(cloudType, SyncPasswordRequired);

                await MaterialMessageBox.ShowAsync(
                    mergeResult.Success
                    ? PasswordManager.Language.Properties.Resources.SyncSuccess
                    : PasswordManager.Language.Properties.Resources.SyncFailed,
                    mergeResult.ToString(),
                    MaterialMessageBoxButtons.OK,
                    windowDialogName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
            finally
            {
                SyncCompleted?.Invoke();
                MergeProcessing = false;
            }
        }

        private async Task UploadCredentials(CloudType cloudType)
        {
            if (UploadProcessing)
                return;

            try
            {
                UploadProcessing = true;
                var windowDialogName = MvvmHelper.MainWindowDialogName;
                var success = await _syncService.Upload(cloudType);

                await MaterialMessageBox.ShowAsync(
                    success
                    ? PasswordManager.Language.Properties.Resources.Success
                    : PasswordManager.Language.Properties.Resources.Error,
                    success
                    ? PasswordManager.Language.Properties.Resources.UploadSuccess
                    : PasswordManager.Language.Properties.Resources.UploadFailed,
                    MaterialMessageBoxButtons.OK,
                    windowDialogName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
            finally
            {
                SyncCompleted?.Invoke();
                UploadProcessing = false;
            }
        }

        private async Task<string> SyncPasswordRequired()
        {
            var password = await MaterialInputBox.ShowAsync(
                PasswordManager.Language.Properties.Resources.InputPasswordOfFile,
                PasswordManager.Language.Properties.Resources.Password,
                MvvmHelper.MainWindowDialogName,
                true);
            return password;
        }

        private Task LoadingAsync()
        {
            return FetchUserInfoIfRequired();
        }
    }
}
