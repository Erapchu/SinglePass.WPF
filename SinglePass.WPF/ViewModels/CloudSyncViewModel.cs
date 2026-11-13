using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SinglePass.WPF.Authorization.Brokers;
using SinglePass.WPF.Cloud.Enums;
using SinglePass.WPF.Clouds.Services;
using SinglePass.WPF.Localization;
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

        public event Action SyncCompleted;

        public CloudAccount GoogleDrive { get; } = new(CloudType.GoogleDrive);
        public CloudAccount YandexDisk { get; } = new(CloudType.YandexDisk);

        [ObservableProperty]
        public partial bool TransferInProgress { get; set; }

        private CloudAccount GetAccount(CloudType cloudType) => cloudType switch
        {
            CloudType.GoogleDrive => GoogleDrive,
            CloudType.YandexDisk => YandexDisk,
            _ => throw new ArgumentOutOfRangeException(nameof(cloudType))
        };

        private CloudSyncViewModel() { }

        public CloudSyncViewModel(
            AppSettingsService appSettingsService,
            CloudServiceProvider cloudServiceProvider,
            ImageService imageService,
            SyncService syncService,
            ILogger<CloudSyncViewModel> logger,
            YandexOAuthProvider yandexOAuthProvider)
        {
            _appSettingsService = appSettingsService;
            _cloudServiceProvider = cloudServiceProvider;
            _imageService = imageService;
            _syncService = syncService;
            _logger = logger;
            GoogleDrive.IsConnected = _appSettingsService.GoogleDriveEnabled;
            YandexDisk = new CloudAccount(CloudType.YandexDisk) { CanSignIn = yandexOAuthProvider.IsConfigured };
        }

        [RelayCommand]
        private async Task Login(CloudType cloudType)
        {
            var account = GetAccount(cloudType);
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
                        await SetConnectedAsync(account, true);
                    });
                await FetchUserInfoFromCloud(account);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning($"Authorization process to {cloudType} has been cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
                ShowAccountError("CloudSignInFailed");
            }
        }

        [RelayCommand]
        private async Task Logout(CloudType cloudType)
        {
            var account = GetAccount(cloudType);
            if (account.FetchingUserInfo || TransferInProgress)
                return;

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
                        account.ProfileImage = null;
                        account.UserName = null;
                        await SetConnectedAsync(account, false);
                    });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning($"Authorization process to {cloudType} has been cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
                ShowAccountError("CloudSignOutFailed");
            }
        }

        private async Task SetConnectedAsync(CloudAccount account, bool connected)
        {
            account.IsConnected = connected;
            if (account.Type == CloudType.GoogleDrive)
            {
                _appSettingsService.GoogleDriveEnabled = connected;
                await _appSettingsService.Save();
            }
        }

        private static void ShowAccountError(string messageKey) => MessageDialog.ShowDialog(
            SinglePass.Language.Properties.Resources.Error, LocalizationManager.Instance[messageKey],
            MessageDialogButtons.OK, "\uEA39");

        private async Task FetchUserInfoFromCloud(CloudAccount account)
        {
            if (!account.IsConnected || account.FetchingUserInfo || !string.IsNullOrEmpty(account.UserName))
                return;

            try
            {
                account.FetchingUserInfo = true;
                var cloudService = _cloudServiceProvider.GetCloudService(account.Type);
                var userInfo = await cloudService.GetUserInfo(CancellationToken.None);
                account.UserName = userInfo.UserName;
                if (!string.IsNullOrEmpty(userInfo.ProfileUrl))
                    account.ProfileImage = await _imageService.GetImageAsync(userInfo.ProfileUrl, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
            finally
            {
                account.FetchingUserInfo = false;
            }
        }

        [RelayCommand]
        private async Task SyncCredentials(CloudType cloudType)
        {
            if (TransferInProgress)
                return;

            var account = GetAccount(cloudType);
            try
            {
                TransferInProgress = account.Transferring = true;
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
                TransferInProgress = account.Transferring = false;
            }
        }

        [RelayCommand]
        private async Task UploadCredentials(CloudType cloudType)
        {
            if (TransferInProgress)
                return;

            var account = GetAccount(cloudType);
            try
            {
                TransferInProgress = account.Transferring = true;
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
                TransferInProgress = account.Transferring = false;
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
            var yandexToken = _cloudServiceProvider.GetCloudService(CloudType.YandexDisk).TokenHolder.OAuthInfo;
            YandexDisk.IsConnected = !string.IsNullOrEmpty(yandexToken.AccessToken);
            return Task.WhenAll(FetchUserInfoFromCloud(GoogleDrive), FetchUserInfoFromCloud(YandexDisk));
        }
    }

    public partial class CloudAccount(CloudType type) : ObservableObject
    {
        public CloudType Type { get; } = type;
        public bool CanSignIn { get; init; } = true;

        [ObservableProperty]
        public partial bool IsConnected { get; set; }

        [ObservableProperty]
        public partial bool FetchingUserInfo { get; set; }

        [ObservableProperty]
        public partial bool Transferring { get; set; }

        [ObservableProperty]
        public partial ImageSource ProfileImage { get; set; }

        [ObservableProperty]
        public partial string UserName { get; set; }
    }
}
