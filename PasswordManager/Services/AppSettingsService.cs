using Microsoft.Extensions.Logging;
using PasswordManager.Helpers;
using PasswordManager.Helpers.Threading;
using PasswordManager.Models;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Services
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

        public AppSettingsService(ILogger<AppSettingsService> logger)
        {
            if (File.Exists(_commonSettingsFilePath))
            {
                // Read existing
                using var fileStream = new FileStream(_commonSettingsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                Settings = JsonSerializer.Deserialize<AppSettings>(fileStream);
            }
            else
            {
                // First save
                Save();
            }

            _logger = logger;
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
