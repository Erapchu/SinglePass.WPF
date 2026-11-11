using SinglePass.WPF.Hotkeys;

namespace SinglePass.WPF.Settings
{
    public class AppSettings : IAppSettings
    {
        public AppTheme ThemeMode { get; set; }
        public bool GoogleDriveEnabled { get; set; }
        public Hotkey ShowPopupHotkey { get; set; }
        public WindowSettings MainWindowSettings { get; set; }
        public SortType Sort { get; set; }
        public OrderType Order { get; set; }
        public PasswordColumnWidths PasswordColumnWidths { get; set; }
        public double DetailsPaneRatio { get; set; } = 0.5;

        public AppSettings()
        {
            ShowPopupHotkey = Hotkey.Empty;
        }
    }
}
