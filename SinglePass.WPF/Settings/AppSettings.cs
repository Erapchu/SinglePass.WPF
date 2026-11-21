using SinglePass.Core.Clouds.Enums;
using SinglePass.WPF.Hotkeys;
using System;
using System.Collections.Generic;

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
        public bool CheckForUpdates { get; set; } = true;

        /// <summary>Time of the last successful sync on this device, per cloud.</summary>
        public Dictionary<CloudType, DateTimeOffset> LastSyncTimes { get; set => field = value ?? new(); } = new();

        public AppSettings()
        {
            ShowPopupHotkey = Hotkey.Empty;
        }
    }
}
