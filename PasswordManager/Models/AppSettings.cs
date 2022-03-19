using PasswordManager.Cloud.Enums;

namespace PasswordManager.Models
{
    public class AppSettings
    {
        public MaterialDesignThemes.Wpf.BaseTheme ThemeMode { get; set; }
        public CloudSettings GoogleCloudSettings { get; set; }

        public AppSettings()
        {
            GoogleCloudSettings = new CloudSettings(CloudType.GoogleDrive);
        }
    }
}
