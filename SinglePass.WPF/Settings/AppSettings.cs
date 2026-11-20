using SinglePass.WPF.Hotkeys;

namespace SinglePass.WPF.Settings
{
    public class AppSettings
    {
        private IdleLockTimeout _idleLockTimeout = IdleLockTimeout.FiveMinutes;

        public IdleLockTimeout IdleLockTimeout
        {
            get => _idleLockTimeout;
            set => _idleLockTimeout = System.Enum.IsDefined(value) ? value : IdleLockTimeout.FiveMinutes;
        }

        public bool LockOnWindowsLock { get; set; } = true;

        public AppTheme ThemeMode { get; set; }
        public bool AnimationsEnabled { get; set; } = true;
        public string Language { get; set; } = "system";
        public Hotkey ShowPopupHotkey { get; set; }
        public WindowSettings MainWindowSettings { get; set; }
        public SortType Sort { get; set; }
        public OrderType Order { get; set; }
        public double DetailsPaneRatio { get; set; } = 0.5;

        public AppSettings()
        {
            ShowPopupHotkey = Hotkey.Empty;
        }
    }
}
