using Microsoft.Extensions.Logging;
using NHotkey;
using NHotkey.Wpf;
using PasswordManager.Settings;
using System;
using System.Media;
using System.Windows.Input;

namespace PasswordManager.Hotkeys
{
    public class HotkeysService
    {
        private readonly AppSettingsService _appSettingsService;
        private readonly ILogger<HotkeysService> _logger;

        public bool IsEnabled
        {
            get => HotkeyManager.Current.IsEnabled;
            set => HotkeyManager.Current.IsEnabled = value;
        }

        public HotkeysService(AppSettingsService appSettingsService, ILogger<HotkeysService> logger)
        {
            _appSettingsService = appSettingsService;
            _logger = logger;

            UpdateKey(_appSettingsService.ShowPopupHotkey, nameof(_appSettingsService.ShowPopupHotkey), HotkeyDelegates.PopupHotkeyHandler);
        }

        public void UpdateKey(Hotkey hotkey, string hotkeyName, Action action)
        {
            if (string.IsNullOrWhiteSpace(hotkeyName))
                throw new ArgumentException($"'{nameof(hotkeyName)}' cannot be null or whitespace.", nameof(hotkeyName));

            if (action is null)
                throw new ArgumentNullException(nameof(action));

            HotkeyManager.Current.Remove(hotkeyName);

            if (hotkey is null || hotkey.Key == Key.None)
                return;

            var handler = new EventHandler<HotkeyEventArgs>((s, args) => HotkeysEventHandler(s, args, action));

            HotkeyManager.Current.AddOrReplace(
                hotkeyName,
                hotkey.Key,
                hotkey.Modifiers,
                handler);
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
