using PasswordManager.Helpers;
using PasswordManager.Models;
using System.IO;
using System.Text.Json;

namespace PasswordManager.Services
{
    public class AppSettingsService
    {
        private readonly string _commonSettingsFilePath = Constants.CommonSettingsFilePath;

        public AppSettings Settings { get; } = new();

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

        public void Save()
        {
            using var fileStream = new FileStream(_commonSettingsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(fileStream, Settings);
        }
    }
}
