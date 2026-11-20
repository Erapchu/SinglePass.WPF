using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SinglePass.Core.Localization;
using SinglePass.WPF.Settings;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Velopack;
using Velopack.Sources;

namespace SinglePass.WPF.Services
{
    public enum UpdateState
    {
        NotSupported,
        Idle,
        Checking,
        UpToDate,
        Downloading,
        ReadyToRestart,
        Failed
    }

    /// <summary>
    /// Checks GitHub releases for a newer version, downloads it in the background
    /// and applies it on restart. Works only in a Velopack-installed (Setup.exe or Portable.zip) copy.
    /// </summary>
    public sealed partial class UpdateService : ObservableObject, IDisposable
    {
        private const string RepositoryUrl = "https://github.com/Erapchu/SinglePass.WPF";
        private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);

        private readonly UpdateManager _manager;
        private readonly AppSettingsService _appSettingsService;
        private readonly ILogger<UpdateService> _logger;
        private CancellationTokenSource _loopCts;
        private UpdateInfo _downloaded;

        /// <summary>Raised on the UI thread once an update has been downloaded and can be installed.</summary>
        public event EventHandler UpdateReady;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(IsBusy))]
        [NotifyCanExecuteChangedFor(nameof(CheckCommand))]
        [NotifyCanExecuteChangedFor(nameof(RestartCommand))]
        public partial UpdateState State { get; private set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        public partial string AvailableVersion { get; private set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        public partial int Progress { get; private set; }

        public bool IsSupported => State != UpdateState.NotSupported;

        public bool IsBusy => State is UpdateState.Checking or UpdateState.Downloading;

        public bool AutoCheck
        {
            get => _appSettingsService.Settings.CheckForUpdates;
            set
            {
                if (_appSettingsService.Settings.CheckForUpdates == value)
                    return;

                _appSettingsService.Settings.CheckForUpdates = value;
                OnPropertyChanged();
                _ = SaveSettingsAsync();
            }
        }

        public string StatusText
        {
            get
            {
                var loc = LocalizationManager.Instance;
                return State switch
                {
                    UpdateState.NotSupported => loc["UpdateNotSupported"],
                    UpdateState.Checking => loc["UpdateChecking"],
                    UpdateState.UpToDate => loc["UpdateUpToDate"],
                    UpdateState.Downloading => string.Format(loc.Culture, loc["UpdateDownloading"], AvailableVersion, Progress),
                    UpdateState.ReadyToRestart => string.Format(loc.Culture, loc["UpdateReadyToInstall"], AvailableVersion),
                    UpdateState.Failed => loc["UpdateFailed"],
                    _ => string.Empty
                };
            }
        }

        public UpdateService(AppSettingsService appSettingsService, ILogger<UpdateService> logger)
        {
            _appSettingsService = appSettingsService;
            _logger = logger;

            try
            {
                _manager = new UpdateManager(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));
                State = _manager.IsInstalled ? UpdateState.Idle : UpdateState.NotSupported;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Velopack update manager is unavailable");
                State = UpdateState.NotSupported;
            }

            LocalizationManager.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(StatusText));
        }

        /// <summary>Starts periodic background checks. Call on the UI thread.</summary>
        public void Start()
        {
            if (!IsSupported || _loopCts != null)
                return;

            _logger.LogInformation("Automatic updates enabled, current version {Version}", _manager.CurrentVersion);
            _loopCts = new CancellationTokenSource();
            RunLoop(_loopCts.Token);
        }

        private async void RunLoop(CancellationToken token)
        {
            try
            {
                await Task.Delay(FirstCheckDelay, token);
                using var timer = new PeriodicTimer(CheckInterval);
                do
                {
                    if (AutoCheck)
                        await CheckAndDownloadAsync();
                }
                while (await timer.WaitForNextTickAsync(token));
            }
            catch (OperationCanceledException)
            {
            }
        }

        private bool CanCheck() => State is UpdateState.Idle or UpdateState.UpToDate or UpdateState.Failed;

        [RelayCommand(CanExecute = nameof(CanCheck))]
        private Task Check() => CheckAndDownloadAsync();

        private async Task CheckAndDownloadAsync()
        {
            if (!CanCheck())
                return;

            State = UpdateState.Checking;
            try
            {
                var info = await _manager.CheckForUpdatesAsync();
                if (info is null)
                {
                    State = UpdateState.UpToDate;
                    return;
                }

                AvailableVersion = info.TargetFullRelease.Version.ToString();
                Progress = 0;
                State = UpdateState.Downloading;
                _logger.LogInformation("Downloading update {Version}", AvailableVersion);

                var dispatcher = Application.Current.Dispatcher;
                await _manager.DownloadUpdatesAsync(info, p => dispatcher.BeginInvoke(() => Progress = p));

                _downloaded = info;
                State = UpdateState.ReadyToRestart;
                _logger.LogInformation("Update {Version} downloaded", AvailableVersion);
                UpdateReady?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Update check failed");
                State = UpdateState.Failed;
            }
        }

        private bool CanRestart() => State == UpdateState.ReadyToRestart;

        /// <summary>Shuts the app down normally; Velopack installs the update and starts the new version.</summary>
        [RelayCommand(CanExecute = nameof(CanRestart))]
        public void Restart()
        {
            if (_downloaded is null)
                return;

            try
            {
                _manager.WaitExitThenApplyUpdates(_downloaded, silent: false, restart: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not start the updater");
                State = UpdateState.Failed;
                return;
            }

            Application.Current.Shutdown();
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                await _appSettingsService.Save();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save update settings");
            }
        }

        public void Dispose()
        {
            _loopCts?.Cancel();
            _loopCts?.Dispose();
            _loopCts = null;
        }
    }
}
