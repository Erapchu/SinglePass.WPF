using SinglePass.WPF.Controls;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace SinglePass.WPF.Helpers
{
    //https://stackoverflow.com/questions/11862315/changing-the-color-of-the-title-bar-in-winform
    internal class DarkTitleBarHelper
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        internal static bool UseImmersiveDarkModeWholeApp(bool enabled)
        {
            bool result = true;

            foreach (var window in Application.Current.Windows.OfType<MaterialWindow>())
            {
                result &= UseImmersiveDarkMode(window.Handle, enabled);
            }

            return result;
        }

        internal static bool UseImmersiveDarkMode(IntPtr handle, bool enabled)
        {
            if (IsWindows10OrGreater(17763))
            {
                var attribute = DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
                if (IsWindows10OrGreater(18985))
                {
                    attribute = DWMWA_USE_IMMERSIVE_DARK_MODE;
                }

                int useImmersiveDarkMode = enabled ? 1 : 0;
                return DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) == 0;
            }

            return false;
        }

        private static bool IsWindows10OrGreater(int build = -1)
        {
            return Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= build;
        }
    }
}
