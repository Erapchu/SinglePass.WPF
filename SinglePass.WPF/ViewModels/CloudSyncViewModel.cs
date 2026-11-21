using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SinglePass.Core.Authorization.Brokers;
using SinglePass.Core.Clouds.Enums;
using SinglePass.Core.Clouds.Services;
using SinglePass.Core.Localization;
using SinglePass.WPF.Services;
using SinglePass.WPF.ViewModels.Dialogs;
using SinglePass.WPF.Views.Controls;
using SinglePass.WPF.Views.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using SinglePass.Core.Services;

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
            CloudServiceProvider cloudServiceProvider,
            ImageService imageService,
            SyncService syncService,
            ILogger<CloudSyncViewModel> logger,
            GoogleOAuthProvider googleOAuthProvider,
            YandexOAuthProvider yandexOAuthProvider)
        {
            _cloudServiceProvider = cloudServiceProvider;
            _imageService = imageService;
            _syncService = syncService;
            _logger = logger;
            GoogleDrive = new CloudAccount(CloudType.GoogleDrive) { CanSignIn = googleOAuthProvider.IsConfigured };
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
                await ProcessingDialog.RunAsync(
                    SinglePass.Language.Properties.Resources.Authorizing,
                    SinglePass.Language.Properties.Resources.PleaseContinueAuthorizationOrCancelIt,
                    async cancellationToken =>
                    {
                        var oauthInfo = await cloudService.OAuthProvider.AuthorizeAsync(cancellationToken);
                        _logger.LogInformation($"Authorization process to {cloudType} has been complete.");
                        await cloudService.TokenHolder.SetAndSaveToken(oauthInfo, cancellationToken);
                        account.IsConnected = true;
                    });
                await FetchUserInfoFromCloud(account);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning($"Authorization process to {cloudType} has been cancelled.");
            }
            catch (OAuthCallbackUnavailableException ex) when (cloudType == CloudType.YandexDisk)
            {
                // Only Yandex uses a fixed, user-configurable callback port.
                _logger.LogError(ex, "Yandex Disk sign-in could not open the local callback");
                if (_syncService.IsLocked)
                    return;
                var message = string.Format(LocalizationManager.Instance["YandexDiskCallbackUnavailable"], ex.Port);
                await MessageDialog.ShowAsync(SinglePass.Language.Properties.Resources.Error, message, MessageDialogButtons.OK, "\uEA39");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
                if (!_syncService.IsLocked)
                    ShowAccountError("CloudSignInFailed");
            }
        }

        [RelayCommand]
        private async Task Logout(CloudType cloudType)
        {
            var account = GetAccount(cloudType);
            if (!account.IsConnected || account.SigningOut || TransferInProgress)
                return;

            account.SigningOut = true;
            try
            {
                // Revoke
                var cloudService = _cloudServiceProvider.GetCloudService(cloudType);
                await ProcessingDialog.RunAsync(
                    SinglePass.Language.Properties.Resources.SigningOut,
                    SinglePass.Language.Properties.Resources.PleaseWait,
                    async cancellationToken =>
                    {
                        account.ProfileCancellation?.Cancel();
                        // Let any in-flight token refresh settle before revoking the current token.
                        await account.ProfileRequest;
                        cancellationToken.ThrowIfCancellationRequested();
                        var oauthInfo = cloudService.TokenHolder.OAuthInfo;
                        await cloudService.OAuthProvider.RevokeTokenAsync(oauthInfo, cancellationToken);
                        await cloudService.TokenHolder.RemoveToken();
                        account.ProfileImage = null;
                        account.UserName = null;
                        account.ProfileLoaded = false;
                        account.IsConnected = false;
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
            finally
            {
                account.SigningOut = false;
            }
        }

        private static void ShowAccountError(string messageKey) => _ = MessageDialog.ShowAsync(
            SinglePass.Language.Properties.Resources.Error, LocalizationManager.Instance[messageKey],
            MessageDialogButtons.OK, "\uEA39");

        private Task FetchUserInfoFromCloud(CloudAccount account)
        {
            if (!account.IsConnected || account.SigningOut || account.FetchingUserInfo || account.ProfileLoaded)
                return Task.CompletedTask;

            return account.ProfileRequest = LoadProfileAsync(account);
        }

        private async Task LoadProfileAsync(CloudAccount account)
        {
            using var cancellation = new CancellationTokenSource();
            account.ProfileCancellation = cancellation;
            var token = cancellation.Token;
            try
            {
                account.FetchingUserInfo = true;
                var cloudService = _cloudServiceProvider.GetCloudService(account.Type);
                var userInfo = await cloudService.GetUserInfo(token);
                token.ThrowIfCancellationRequested();
                account.UserName = userInfo.UserName;
                if (!string.IsNullOrEmpty(userInfo.ProfileUrl))
                {
                    var image = await _imageService.GetProfileImageAsync(userInfo.ProfileUrl, token);
                    token.ThrowIfCancellationRequested();
                    account.ProfileImage = image;
                }
                account.ProfileLoaded = true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
            finally
            {
                account.ProfileCancellation = null;
                account.FetchingUserInfo = false;
            }
        }

        [RelayCommand]
        private async Task SyncCredentials(CloudType cloudType)
        {
            var account = GetAccount(cloudType);
            if (!account.IsConnected || account.SigningOut || TransferInProgress)
                return;

            try
            {
                TransferInProgress = account.Transferring = true;
                var mergeResult = await _syncService.Synchronize(cloudType, SyncPasswordRequired);
                if (_syncService.IsLocked)
                    return;

                await MessageDialog.ShowAsync(
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
                if (!_syncService.IsLocked)
                    SyncCompleted?.Invoke();
                TransferInProgress = account.Transferring = false;
            }
        }

        private static async Task<string> SyncPasswordRequired()
        {
            var dialog = new SyncPasswordDialog();
            await ModalDialog.ShowAsync(dialog);
            return dialog.Password;
        }

        [RelayCommand]
        private Task Loading()
        {
            var googleToken = _cloudServiceProvider.GetCloudService(CloudType.GoogleDrive).TokenHolder.OAuthInfo;
            GoogleDrive.IsConnected = !string.IsNullOrEmpty(googleToken.AccessToken)
                || !string.IsNullOrEmpty(googleToken.RefreshToken);

            var yandexToken = _cloudServiceProvider.GetCloudService(CloudType.YandexDisk).TokenHolder.OAuthInfo;
            YandexDisk.IsConnected = !string.IsNullOrEmpty(yandexToken.AccessToken);
            return Task.WhenAll(FetchUserInfoFromCloud(GoogleDrive), FetchUserInfoFromCloud(YandexDisk));
        }
    }

    public partial class CloudAccount(CloudType type) : ObservableObject
    {
        public CloudType Type { get; } = type;
        public bool CanSignIn { get; init; } = true;
        internal bool ProfileLoaded { get; set; }
        internal bool SigningOut { get; set; }
        internal CancellationTokenSource ProfileCancellation { get; set; }
        internal Task ProfileRequest { get; set; } = Task.CompletedTask;

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
