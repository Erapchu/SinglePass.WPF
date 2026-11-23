using Microsoft.Extensions.Logging;
using NHotkey;
using NHotkey.Wpf;
using SinglePass.WPF.Settings;
using System;
using System.Media;
using System.Windows.Input;

namespace SinglePass.WPF.Hotkeys
{
    public class HotkeysService
    {
        private readonly AppSettingsService _appSettingsService;
        private readonly ILogger<HotkeysService> _logger;

        public HotkeysService(AppSettingsService appSettingsService, ILogger<HotkeysService> logger)
        {
            _appSettingsService = appSettingsService;
            _logger = logger;

            HotkeyManager.Current.IsEnabled = true;
            ApplyPopupHotkey(_appSettingsService.PopupHotkeyEnabled, _appSettingsService.ShowPopupHotkey);
        }

        /// <summary>
        /// Registers the popup hotkey, or removes it when the popup is turned off or has no hotkey.
        /// Returns false when another program already uses the key combination; the popup then has no hotkey.
        /// </summary>
        public bool ApplyPopupHotkey(bool enabled, Hotkey hotkey)
        {
            const string name = nameof(AppSettingsService.ShowPopupHotkey);
            HotkeyManager.Current.Remove(name);

            if (!enabled || hotkey is null || hotkey.IsEmpty)
                return true;

            try
            {
                HotkeyManager.Current.AddOrReplace(name, hotkey.Key, hotkey.Modifiers,
                    (s, args) => HotkeysEventHandler(s, args, HotkeyDelegates.PopupHotkeyHandler));
                return true;
            }
            catch (HotkeyAlreadyRegisteredException ex)
            {
                _logger.LogWarning(ex, "The popup hotkey {Hotkey} is used by another program", hotkey);
                return false;
            }
        }

        private void HotkeysEventHandler(object sender, HotkeyEventArgs args, Action action)
        {
            _logger.LogInformation($"{args.Name} pressed");
            var result = false;

            try
            {
                action.Invoke();
                result = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }

            if (!result)
                SystemSounds.Asterisk.Play();

            args.Handled = true;
        }

        public bool GetHotkeyForKeyPress(KeyEventArgs args, out Hotkey hotkey)
        {
            if (args is null)
                throw new ArgumentNullException(nameof(args));

            var key = args.Key == Key.System ? args.SystemKey : args.Key;
            var modifiers = Keyboard.Modifiers;

            if (key == Key.Escape && modifiers == ModifierKeys.None)
            {
                hotkey = Hotkey.Empty;
                return true;
            }

            if (key == Key.LeftShift || key == Key.RightShift
                || key == Key.LeftCtrl || key == Key.RightCtrl
                || key == Key.LeftAlt || key == Key.RightAlt
                || key == Key.LWin || key == Key.RWin)
            {
                hotkey = null;
                return false;
            }

            hotkey = new Hotkey()
            {
                Key = key,
                Modifiers = modifiers,
            };
            return true;
        }
    }
}
