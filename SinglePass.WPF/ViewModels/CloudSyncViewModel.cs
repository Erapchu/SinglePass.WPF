using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using SinglePass.WPF.Cloud.Enums;
using SinglePass.WPF.Clouds.Services;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Services;
using SinglePass.WPF.Settings;
using SinglePass.WPF.Views;
using SinglePass.WPF.Views.InputBox;
using SinglePass.WPF.Views.MessageBox;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SinglePass.WPF.ViewModels
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
        }

        private async Task Login(CloudType cloudType)
        {
            var windowDialogName = DialogIdentifiers.MainWindowName;
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
                    var processingControl = new ProcessingControl(
                        SinglePass.Language.Properties.Resources.Authorizing,
                        SinglePass.Language.Properties.Resources.PleaseContinueAuthorizationOrCancelIt,
                        windowDialogName);
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
                    var processingControl = new ProcessingControl(
                        SinglePass.Language.Properties.Resources.SigningOut,
                        SinglePass.Language.Properties.Resources.PleaseWait,
                        windowDialogName);
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
                _logger.LogWarning($"Authorization process to {cloudType} has been cancelled.");
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
                var mergeResult = await _syncService.Synchronize(cloudType, SyncPasswordRequired);

                await MaterialMessageBox.ShowAsync(
                    mergeResult.Success
                    ? SinglePass.Language.Properties.Resources.SyncSuccess
                    : SinglePass.Language.Properties.Resources.SyncFailed,
                    mergeResult.ToString(),
                    MaterialMessageBoxButtons.OK,
                    DialogIdentifiers.MainWindowName);
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
                var success = await _syncService.Upload(cloudType);

                await MaterialMessageBox.ShowAsync(
                    success
                    ? SinglePass.Language.Properties.Resources.Success
                    : SinglePass.Language.Properties.Resources.Error,
                    success
                    ? SinglePass.Language.Properties.Resources.UploadSuccess
                    : SinglePass.Language.Properties.Resources.UploadFailed,
                    MaterialMessageBoxButtons.OK,
                    DialogIdentifiers.MainWindowName);
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
                SinglePass.Language.Properties.Resources.InputPasswordOfFile,
                SinglePass.Language.Properties.Resources.Password,
                DialogIdentifiers.MainWindowName,
                true);
            return password;
        }

        private Task LoadingAsync()
        {
            return FetchUserInfoIfRequired();
        }
    }
}
