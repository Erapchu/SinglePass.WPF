using PasswordManager.Hotkeys;

namespace PasswordManager.Settings
{
    public interface IAppSettings
    {
        public MaterialDesignThemes.Wpf.BaseTheme ThemeMode { get; set; }
        public bool GoogleDriveEnabled { get; set; }
        public Hotkey ShowPopupHotkey { get; set; }
        public WindowSettings MainWindowSettings { get; set; }
        public SortType Sort { get; set; }
        public OrderType Order { get; set; }
    }
}
