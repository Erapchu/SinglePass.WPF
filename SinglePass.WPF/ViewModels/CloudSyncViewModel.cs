using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SinglePass.WPF.Cloud.Enums;
using SinglePass.WPF.Clouds.Services;
using SinglePass.WPF.Services;
using SinglePass.WPF.Settings;
using SinglePass.WPF.ViewModels.Dialogs;
using SinglePass.WPF.Views.Helpers;
using SinglePass.WPF.Views.Windows;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SinglePass.WPF.ViewModels
{
    public partial class CloudSyncViewModel : ObservableObject
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

        [ObservableProperty]
        public partial bool MergeProcessing { get; set; }

        [ObservableProperty]
        public partial bool UploadProcessing { get; set; }

        [ObservableProperty]
        public partial bool FetchingUserInfo { get; set; }

        [ObservableProperty]
        public partial ImageSource GoogleProfileImage { get; set; }

        [ObservableProperty]
        public partial string GoogleUserName { get; set; }

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

        [RelayCommand]
        private async Task Login(CloudType cloudType)
        {

            try
            {
                // Authorize
                var cloudService = _cloudServiceProvider.GetCloudService(cloudType);
                await ProcessingDialogWindow.RunAsync(
                    SinglePass.Language.Properties.Resources.Authorizing,
                    SinglePass.Language.Properties.Resources.PleaseContinueAuthorizationOrCancelIt,
                    async cancellationToken =>
                    {
                        var oauthInfo = await cloudService.OAuthProvider.AuthorizeAsync(cancellationToken);
                        _logger.LogInformation($"Authorization process to {cloudType} has been complete.");
                        await cloudService.TokenHolder.SetAndSaveToken(oauthInfo, cancellationToken);
                        GoogleDriveEnabled = true;
                        _ = FetchUserInfoFromCloud(cloudType, CancellationToken.None);
                        await _appSettingsService.Save();
                    });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning($"Authorization process to {cloudType} has been cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
        }

        [RelayCommand]
        private async Task Logout(CloudType cloudType)
        {
            try
            {
                // Revoke
                var cloudService = _cloudServiceProvider.GetCloudService(cloudType);
                await ProcessingDialogWindow.RunAsync(
                    SinglePass.Language.Properties.Resources.SigningOut,
                    SinglePass.Language.Properties.Resources.PleaseWait,
                    async cancellationToken =>
                    {
                        var oauthInfo = cloudService.TokenHolder.OAuthInfo;
                        await cloudService.OAuthProvider.RevokeTokenAsync(oauthInfo, cancellationToken);
                        await cloudService.TokenHolder.RemoveToken();
                        GoogleDriveEnabled = false;
                        ClearUserInfo(cloudType);
                        await _appSettingsService.Save();
                    });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning($"Authorization process to {cloudType} has been cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
        }

        private Task FetchUserInfoIfRequired()
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

        [RelayCommand]
        private async Task SyncCredentials(CloudType cloudType)
        {
            if (MergeProcessing)
                return;

            try
            {
                MergeProcessing = true;
                var mergeResult = await _syncService.Synchronize(cloudType, SyncPasswordRequired);

                MessageDialog.ShowDialog(
                    mergeResult.Success
                    ? SinglePass.Language.Properties.Resources.SyncSuccess
                    : SinglePass.Language.Properties.Resources.SyncFailed,
                    mergeResult.ToString(),
                    MessageDialogButtons.OK,
                    mergeResult.Success
                    ? "\uE73E"
                    : "\uEA39");
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

        [RelayCommand]
        private async Task UploadCredentials(CloudType cloudType)
        {
            if (UploadProcessing)
                return;

            try
            {
                UploadProcessing = true;
                var success = await _syncService.Upload(cloudType);

                MessageDialog.ShowDialog(
                    success
                    ? SinglePass.Language.Properties.Resources.Success
                    : SinglePass.Language.Properties.Resources.Error,
                    success
                    ? SinglePass.Language.Properties.Resources.UploadSuccess
                    : SinglePass.Language.Properties.Resources.UploadFailed,
                    MessageDialogButtons.OK,
                    success
                    ? "\uE73E"
                    : "\uEA39");
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

        private static Task<string> SyncPasswordRequired()
        {
            var dialog = new SyncPasswordDialogWindow { Owner = Application.Current.MainWindow };
            return Task.FromResult(dialog.ShowDialog() == true ? dialog.Password : null);
        }

        [RelayCommand]
        private Task Loading()
        {
            return Task.Run(FetchUserInfoIfRequired);
        }
    }
}
