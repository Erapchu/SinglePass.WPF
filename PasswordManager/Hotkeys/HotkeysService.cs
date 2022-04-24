using Microsoft.Extensions.Logging;
using NHotkey;
using NHotkey.Wpf;
using PasswordManager.Helpers;
using PasswordManager.Services;
using PasswordManager.Views;
using System;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using UIAutomationClient;

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

            UpdateKey(_appSettingsService.ShowPopupHotkey, nameof(_appSettingsService.ShowPopupHotkey), PopupHotkeyHandler);
        }

        private static void HotkeysEventHandler(object sender, HotkeyEventArgs args, Func<bool> func)
        {
            var result = func.Invoke();

            if (!result)
                SystemSounds.Asterisk.Play();

            args.Handled = true;
        }

        public void UpdateKey(Hotkey hotkey, string hotkeyName, Func<bool> func)
        {
            if (string.IsNullOrWhiteSpace(hotkeyName))
                throw new ArgumentException($"'{nameof(hotkeyName)}' cannot be null or whitespace.", nameof(hotkeyName));

            if (func is null)
                throw new ArgumentNullException(nameof(func));

            HotkeyManager.Current.Remove(hotkeyName);

            if (hotkey is null || hotkey.Key == Key.None)
                return;

            var handler = new EventHandler<HotkeyEventArgs>((s, args) => HotkeysEventHandler(s, args, func));

            HotkeyManager.Current.AddOrReplace(
                hotkeyName,
                hotkey.Key,
                hotkey.Modifiers,
                handler);
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

        private bool PopupHotkeyHandler()
        {
            try
            {
                _logger.LogInformation("Popup hotkey pressed");

                var info = new WinApiProvider.GUITHREADINFO();
                info.cbSize = Marshal.SizeOf(info);
                if (WinApiProvider.GetGUIThreadInfo(0, ref info))
                {
                    var hwndFocus = info.hwndFocus;
                    var caretRect = GetAccessibleCaretRect(hwndFocus);

                    var popup = new PopupControl
                    {
                        IsOpen = true
                    };
                    // Obtain popup handle for placement
                    var popupHandle = ((HwndSource)PresentationSource.FromVisual(popup.Child)).Handle;
                    var popupRect = new WinApiProvider.RECT();
                    WinApiProvider.GetWindowRect(popupHandle, ref popupRect);
                    if (!RectValid(caretRect))
                    {
                        // Can't accquire caret placement
                        caretRect = GetWinApiCaretRect(hwndFocus);
                        if (!RectValid(caretRect))
                        {
                            caretRect = new WinApiProvider.RECT()
                            {
                                left = (int)SystemParameters.PrimaryScreenWidth - (popupRect.right - popupRect.left),
                                top = (int)SystemParameters.PrimaryScreenHeight - (popupRect.bottom - popupRect.top),
                                right = (int)SystemParameters.PrimaryScreenWidth,
                                bottom = (int)SystemParameters.PrimaryScreenHeight
                            };
                        }
                    }
                    
                    // OK caret placement
                    WinApiProvider.SetWindowPos(
                        popupHandle,
                        IntPtr.Zero,
                        caretRect.right,
                        caretRect.bottom,
                        popupRect.right - popupRect.left,
                        popupRect.bottom - popupRect.top,
                        WinApiProvider.SWP_NOZORDER);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
                return false;
            }
        }

        private WinApiProvider.RECT GetAccessibleCaretRect(IntPtr hwnd)
        {
            var guid = typeof(IAccessible).GUID;
            object accessibleObject = null;
            var retVal = WinApiProvider.AccessibleObjectFromWindow(hwnd, WinApiProvider.OBJID_CARET, ref guid, ref accessibleObject);
            var accessible = accessibleObject as IAccessible;
            accessible.accLocation(out int left, out int top, out int width, out int height, WinApiProvider.CHILDID_SELF);
            return new WinApiProvider.RECT() { bottom = top + height, left = left, right = left + width, top = top };
        }

        private WinApiProvider.RECT GetWinApiCaretRect(IntPtr hwnd)
        {
            // Try WinAPI
            uint idAttach = 0;
            uint curThreadId = 0;
            WinApiProvider.POINT caretPoint;
            try
            {
                idAttach = WinApiProvider.GetWindowThreadProcessId(hwnd, out uint id);
                curThreadId = WinApiProvider.GetCurrentThreadId();
                // To attach to current thread
                var sa = WinApiProvider.AttachThreadInput(idAttach, curThreadId, true);
                var caretPos = WinApiProvider.GetCaretPos(out caretPoint);
                WinApiProvider.ClientToScreen(hwnd, ref caretPoint);
            }
            finally
            {
                // To dettach from current thread
                var sd = WinApiProvider.AttachThreadInput(idAttach, curThreadId, false);
            }

            return new WinApiProvider.RECT()
            {
                left = caretPoint.X,
                top = caretPoint.Y,
                bottom = caretPoint.Y + 20,
                right = caretPoint.X + 1
            };
        }

        private bool RectValid(WinApiProvider.RECT rect)
        {
            return rect.left != 0 && rect.top != 0 && rect.right != 0 && rect.bottom != 0;
        }
    }
}
