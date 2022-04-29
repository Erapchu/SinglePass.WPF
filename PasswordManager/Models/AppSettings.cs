using PasswordManager.Cloud.Enums;
using PasswordManager.Hotkeys;

namespace PasswordManager.Models
{
    public class AppSettings
    {
        public MaterialDesignThemes.Wpf.BaseTheme ThemeMode { get; set; }
        public bool GoogleDriveEnabled { get; set; }
        public Hotkey ShowPopupHotkey { get; set; }

        public AppSettings()
        {
            ShowPopupHotkey = Hotkey.Empty;
        }
    }
}
