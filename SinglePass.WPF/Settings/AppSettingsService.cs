using AsyncKeyedLock;
using Microsoft.Extensions.Logging;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Hotkeys;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SinglePass.WPF.Settings
{
    public class AppSettingsService : IAppSettings
    {
        private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
        private readonly ILogger<AppSettingsService> _logger;

        public AppSettings Settings { get; } = new();

        public MaterialDesignThemes.Wpf.BaseTheme ThemeMode
        {
            get => Settings.ThemeMode;
            set => Settings.ThemeMode = value;
        }

        public bool GoogleDriveEnabled
        {
            get => Settings.GoogleDriveEnabled;
            set => Settings.GoogleDriveEnabled = value;
        }

        public Hotkey ShowPopupHotkey
        {
            get => Settings.ShowPopupHotkey;
            set => Settings.ShowPopupHotkey = value;
        }

        public WindowSettings MainWindowSettings
        {
            get => Settings.MainWindowSettings;
            set => Settings.MainWindowSettings = value;
        }

        public SortType Sort
        {
            get => Settings.Sort;
            set => Settings.Sort = value;
        }

        public OrderType Order
        {
            get => Settings.Order;
            set => Settings.Order = value;
        }

        public AppSettingsService(AsyncKeyedLocker<string> asyncKeyedLocker, ILogger<AppSettingsService> logger)
        {
            _asyncKeyedLocker = asyncKeyedLocker;
            _logger = logger;

            if (File.Exists(Constants.CommonSettingsFilePath))
            {
                // Read existing
                try
                {
                    using var fileStream = new FileStream(Constants.CommonSettingsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    Settings = JsonSerializer.Deserialize<AppSettings>(fileStream);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, null);
                }
            }
        }

        public async Task Save()
        {
            // Use local lock instead of interprocess lock - only one instance of app will work with this file
            using (await _asyncKeyedLocker.LockAsync(Constants.CommonSettingsFilePath).ConfigureAwait(false))
            {
                using var fileStream = new FileStream(Constants.CommonSettingsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                await JsonSerializer.SerializeAsync(fileStream, Settings).ConfigureAwait(false);
            }
            _logger.LogInformation("Settings saved to file");
        }
    }
}
