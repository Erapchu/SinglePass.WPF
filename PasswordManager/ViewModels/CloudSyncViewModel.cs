using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Cloud.Enums;
using PasswordManager.Clouds.Services;
using PasswordManager.Helpers;
using PasswordManager.Services;
using PasswordManager.Views;
using PasswordManager.Views.InputBox;
using PasswordManager.Views.MessageBox;
using System;
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
        private readonly ILogger<CloudSyncViewModel> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
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
            SyncService syncService,
            ILogger<CloudSyncViewModel> logger)
        {
            Name = "Cloud sync";
            IconKind = PackIconKind.Cloud;

            _appSettingsService = appSettingsService;
            _cloudServiceProvider = cloudServiceProvider;
            _httpClientFactory = httpClientFactory;
            _syncService = syncService;
            _logger = logger;
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
                        GoogleProfileImage = await GetImageAsync(userInfo.ProfileUrl, cancellationToken);
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

        private async Task<ImageSource> GetImageAsync(string imageUrl, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = stream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
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
            if (SyncProcessing)
                return;

            try
            {
                SyncProcessing = true;
                var windowDialogName = MvvmHelper.MainWindowDialogName;
                var mergeResult = await _syncService.Synchronize(cloudType, SyncPasswordRequired);

                await MaterialMessageBox.ShowAsync(
                    mergeResult.Success ? "Credentials successfully merged" : "Credentials not merged",
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
                SyncProcessing = false;
            }
        }

        private async Task<string> SyncPasswordRequired()
        {
            var password = await MaterialInputBox.ShowAsync(
                "Input password of cloud file",
                "Password",
                MvvmHelper.MainWindowDialogName,
                true);
            return password;
        }
    }
}
