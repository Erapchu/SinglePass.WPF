using Microsoft.Extensions.Logging;
using PasswordManager.Helpers;
using PasswordManager.Helpers.Threading;
using PasswordManager.Hotkeys;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Settings
{
    public class AppSettingsService : IAppSettings
    {
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

        public AppSettingsService(ILogger<AppSettingsService> logger)
        {
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

        public Task Save()
        {
            return Task.Run(() =>
            {
                var hashedPath = HashHelper.GetHash(Constants.CommonSettingsFilePath);
                using var waitHandleLocker = EventWaitHandleLocker.MakeWithEventHandle(true, EventResetMode.AutoReset, hashedPath);
                using var fileStream = new FileStream(Constants.CommonSettingsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                JsonSerializer.Serialize(fileStream, Settings);
                _logger.LogInformation("Settings saved to file");
            });
        }
    }
}
