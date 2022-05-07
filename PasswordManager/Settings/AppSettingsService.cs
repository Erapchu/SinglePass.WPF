using Microsoft.Extensions.Logging;
using PasswordManager.Helpers;
using PasswordManager.Helpers.Threading;
using PasswordManager.Hotkeys;
using PasswordManager.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Settings
{
    public class AppSettingsService
    {
        private readonly string _commonSettingsFilePath = Constants.CommonSettingsFilePath;
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

        public AppSettingsService(ILogger<AppSettingsService> logger)
        {
            _logger = logger;

            if (File.Exists(_commonSettingsFilePath))
            {
                // Read existing
                try
                {
                    using var fileStream = new FileStream(_commonSettingsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    Settings = JsonSerializer.Deserialize<AppSettings>(fileStream);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, null);
                }
            }
            else
            {
                // First save
                Save();
            }
        }

        public Task Save()
        {
            return Task.Run(() =>
            {
                var hashedPath = HashHelper.GetHash(_commonSettingsFilePath);
                using var waitHandleLocker = EventWaitHandleLocker.MakeWithEventHandle(true, EventResetMode.AutoReset, hashedPath);
                using var fileStream = new FileStream(_commonSettingsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                JsonSerializer.Serialize(fileStream, Settings);
                _logger.LogInformation("Settings saved to file");
            });
        }
    }
}
