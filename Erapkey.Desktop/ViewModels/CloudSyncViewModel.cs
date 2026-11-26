using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Erapkey.Core.Authorization.Brokers;
using Erapkey.Core.Clouds.Enums;
using Erapkey.Core.Clouds.Services;
using Erapkey.Core.Localization;
using Erapkey.Desktop.Services;
using Erapkey.Desktop.Settings;
using Erapkey.Desktop.ViewModels.Dialogs;
using Erapkey.Desktop.Views.Controls;
using Erapkey.Desktop.Views.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Erapkey.Core.Services;

namespace Erapkey.Desktop.ViewModels
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
        private readonly AppSettingsService _appSettingsService;

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
            YandexOAuthProvider yandexOAuthProvider,
            AppSettingsService appSettingsService)
        {
            _cloudServiceProvider = cloudServiceProvider;
            _imageService = imageService;
            _syncService = syncService;
            _logger = logger;
            _appSettingsService = appSettingsService;
            GoogleDrive = new CloudAccount(CloudType.GoogleDrive) { CanSignIn = googleOAuthProvider.IsConfigured };
            YandexDisk = new CloudAccount(CloudType.YandexDisk) { CanSignIn = yandexOAuthProvider.IsConfigured };
            // Both live in the vault scope and are disposed together, so the subscription needs no removal.
            _syncService.SyncStateChanged += (cloudType, state) => GetAccount(cloudType).SyncState = state;
            foreach (var account in new[] { GoogleDrive, YandexDisk })
            {
                if (_appSettingsService.Settings.LastSyncTimes.TryGetValue(account.Type, out var time))
                    account.LastSyncTime = time.LocalDateTime;
            }
        }

        /// <summary>Remembers (or forgets, with null) the last successful sync of the account on this device.</summary>
        private async Task SaveLastSyncTime(CloudAccount account, DateTimeOffset? time)
        {
            account.LastSyncTime = time?.LocalDateTime;
            var times = _appSettingsService.Settings.LastSyncTimes;
            if (time.HasValue)
                times[account.Type] = time.Value;
            else if (!times.Remove(account.Type))
                return;

            try
            {
                await _appSettingsService.Save();
            }
            catch (Exception ex)
            {
                // Only an informational value: keep it in memory and carry on.
                _logger.LogError(ex, "Failed to save the last sync time");
            }
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
                    Erapkey.Language.Properties.Resources.Authorizing,
                    Erapkey.Language.Properties.Resources.PleaseContinueAuthorizationOrCancelIt,
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
                await MessageDialog.ShowAsync(Erapkey.Language.Properties.Resources.Error, message, MessageDialogButtons.OK, "\uEA39");
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
                    Erapkey.Language.Properties.Resources.SigningOut,
                    Erapkey.Language.Properties.Resources.PleaseWait,
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
                        await SaveLastSyncTime(account, null);
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
            Erapkey.Language.Properties.Resources.Error, LocalizationManager.Instance[messageKey],
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

                if (mergeResult.Success)
                    await SaveLastSyncTime(account, DateTimeOffset.UtcNow);

                await MessageDialog.ShowAsync(
                    mergeResult.Success
                    ? Erapkey.Language.Properties.Resources.SyncSuccess
                    : Erapkey.Language.Properties.Resources.SyncFailed,
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

        /// <summary>Local time of the last successful sync on this device; null if unknown.</summary>
        [ObservableProperty]
        public partial DateTime? LastSyncTime { get; set; }

        /// <summary>Current sync stage shown under the progress bar.</summary>
        [ObservableProperty]
        public partial string SyncState { get; set; }

        [ObservableProperty]
        public partial ImageSource ProfileImage { get; set; }

        [ObservableProperty]
        public partial string UserName { get; set; }
    }
}
