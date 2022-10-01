using SinglePass.WPF.Helpers;
using SinglePass.WPF.Views.Windows;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using UIAutomationClient;

namespace SinglePass.WPF.Hotkeys
{
    internal static class HotkeyDelegates
    {
        public static void PopupHotkeyHandler()
        {
            // Prevent duplicates
            if (IsAnyPopupWindowOpened())
            {
                return;
            }

            var info = new WinApiProvider.GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(info);
            if (WinApiProvider.GetGUIThreadInfo(0, ref info))
            {
                var hwndFocus = info.hwndFocus;
                var caretRect = GetAccessibleCaretRect(hwndFocus);
                var popup = (System.Windows.Application.Current as App).Services.GetService(typeof(PopupWindow)) as PopupWindow;

                if (!RectValid(caretRect))
                {
                    // Can't accquire caret placement
                    caretRect = GetWinApiCaretRect(hwndFocus);
                    if (!RectValid(caretRect))
                    {
                        caretRect = new WinApiProvider.RECT()
                        {
                            left = (int)(SystemParameters.PrimaryScreenWidth - popup.Width),
                            top = (int)(SystemParameters.PrimaryScreenHeight - popup.Height),
                            right = (int)SystemParameters.PrimaryScreenWidth,
                            bottom = (int)SystemParameters.PrimaryScreenHeight
                        };
                    }
                }

                // https://stackoverflow.com/questions/1918877/how-can-i-get-the-dpi-in-wpf
                // VisualTreeHelper.GetDpi(Visual visual)
                var dpiAtPoint = DpiUtilities.GetDpiForNearestMonitor(caretRect.right, caretRect.bottom);
                popup.Left = caretRect.right * DpiUtilities.DefaultDpiX / dpiAtPoint;
                popup.Top = caretRect.bottom * DpiUtilities.DefaultDpiY / dpiAtPoint;
                WindowPositionHelper.ShiftWindowToScreen(popup);
                popup.ForegroundHWND = hwndFocus;
                popup.Show();
                var popuHandle = new WindowInteropHelper(popup).EnsureHandle();
                WinApiProvider.SetForegroundWindow(popuHandle);
                //popup.HorizontalOffset = caretRect.right * DpiUtilities.DefaultDpiX / dpiAtPoint;
                //popup.VerticalOffset = caretRect.bottom * DpiUtilities.DefaultDpiY / dpiAtPoint;
                //popup.ForegroundHWND = hwndFocus;
                //popup.IsOpen = true;
            }
        }

        private static bool IsAnyPopupWindowOpened()
        {
            return Application.Current.Windows.OfType<PopupWindow>().Any();
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
