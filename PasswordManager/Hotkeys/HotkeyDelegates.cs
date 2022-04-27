using PasswordManager.Helpers;
using PasswordManager.Views;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using UIAutomationClient;

namespace PasswordManager.Hotkeys
{
    internal static class HotkeyDelegates
    {
        public static void PopupHotkeyHandler()
        {
            var info = new WinApiProvider.GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(info);
            if (WinApiProvider.GetGUIThreadInfo(0, ref info))
            {
                var hwndFocus = info.hwndFocus;
                var caretRect = GetAccessibleCaretRect(hwndFocus);

                var popup = (Application.Current as App).Host.Services.GetService(typeof(PopupControl)) as PopupControl;
                popup.IsOpen = true;

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
        }

        private static WinApiProvider.RECT GetAccessibleCaretRect(IntPtr hwnd)
        {
            var guid = typeof(IAccessible).GUID;
            object accessibleObject = null;
            var retVal = WinApiProvider.AccessibleObjectFromWindow(hwnd, WinApiProvider.OBJID_CARET, ref guid, ref accessibleObject);
            var accessible = accessibleObject as IAccessible;
            accessible.accLocation(out int left, out int top, out int width, out int height, WinApiProvider.CHILDID_SELF);
            return new WinApiProvider.RECT() { bottom = top + height, left = left, right = left + width, top = top };
        }

        private static WinApiProvider.RECT GetWinApiCaretRect(IntPtr hwnd)
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

        private static bool RectValid(WinApiProvider.RECT rect)
        {
            return rect.left != 0 && rect.top != 0 && rect.right != 0 && rect.bottom != 0;
        }
    }
}
