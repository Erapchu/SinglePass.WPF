using SinglePass.WPF.Hotkeys;

namespace SinglePass.WPF.Settings
{
    public class AppSettings : IAppSettings
    {
        public AppTheme ThemeMode { get; set; }
        public string Language { get; set; } = "system";
        public bool GoogleDriveEnabled { get; set; }
        public Hotkey ShowPopupHotkey { get; set; }
        public WindowSettings MainWindowSettings { get; set; }
        public SortType Sort { get; set; }
        public OrderType Order { get; set; }
        public double DetailsPaneRatio { get; set; } = 0.5;
        public PasswordGroupSettings PasswordGroups { get; set; } = new();

        internal AppSettings WithPasswordGroups(PasswordGroupSettings passwordGroups)
        {
            var copy = (AppSettings)MemberwiseClone();
            copy.PasswordGroups = passwordGroups;
            return copy;
        }

        public AppSettings()
        {
            ShowPopupHotkey = Hotkey.Empty;
        }
    }
}
