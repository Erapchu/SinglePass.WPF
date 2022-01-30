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

        public AppSettings Settings { get; } = new();

        public bool IsDarkMode
        {
            get => Settings.IsDarkMode;
            set
            {
                if (Settings.IsDarkMode == value)
                    return;

                Settings.IsDarkMode = value;
                Save();
            }
        }

        public AppSettingsService()
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
        }

        public Task Save()
        {
            return Task.Run(() =>
            {
                var hashedPath = HashHelper.GetHash(_commonSettingsFilePath);
                using var waitHandleLocker = EventWaitHandleLocker.MakeWithEventHandle(true, EventResetMode.AutoReset, hashedPath);
                using var fileStream = new FileStream(_commonSettingsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                JsonSerializer.Serialize(fileStream, Settings);
            });
        }
    }
}
