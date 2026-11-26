using Erapkey.Core.Clouds.Enums;
using Erapkey.Desktop.Hotkeys;
using System;
using System.Collections.Generic;

namespace Erapkey.Desktop.Settings
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

        /// <summary>
        /// Whether <see cref="ShowPopupHotkey"/> is registered. Off by default: logins are mostly filled in browsers,
        /// where the extension does it. Null in settings saved before the switch existed; see
        /// <see cref="AppSettingsService.PopupHotkeyEnabled"/>.
        /// </summary>
        public bool? PopupHotkeyEnabled { get; set; }
        public WindowSettings MainWindowSettings { get; set; }
        public SortType Sort { get; set; }
        public OrderType Order { get; set; }
        public double DetailsPaneRatio { get; set; } = 0.5;
        public bool CheckForUpdates { get; set; } = true;

        /// <summary>Serve the SSH keys of the vault to ssh, git and other programs (see SshAgentServer).</summary>
        public bool SshAgentEnabled { get; set; } = true;

        /// <summary>Reopen a locked vault with Windows Hello (see WindowsHelloUnlock).</summary>
        public bool WindowsHelloEnabled { get; set; }

        /// <summary>
        /// Answer the Erapkey browser extension (see BrowserPipeServer). Off by default until the extension is published.
        /// </summary>
        public bool BrowserIntegrationEnabled { get; set; }

        /// <summary>Time of the last successful sync on this device, per cloud.</summary>
        public Dictionary<CloudType, DateTimeOffset> LastSyncTimes { get; set => field = value ?? new(); } = new();

        public AppSettings()
        {
            ShowPopupHotkey = Hotkey.Empty;
        }
    }
}
