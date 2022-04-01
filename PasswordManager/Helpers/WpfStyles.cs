using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PasswordManager.Helpers
{
    internal class WpfStyles
    {
        /// <summary>
        /// Gets the small application icon (16x16) from the running executable.
        /// </summary>
        public static ImageSource SmallApplicationIcon => GetApplicationIcon(16);

        /// <summary>
        /// Gets the medium application icon (32x32) from the running executable.
        /// </summary>
        public static ImageSource MediumApplicationIcon => GetApplicationIcon(32);

        /// <summary>
        /// Gets the large application icon (48x48) from the running executable.
        /// </summary>
        public static ImageSource LargeApplicationIcon => GetApplicationIcon(48);

        public static ImageSource GetApplicationIcon(int size)
        {
            try
            {
                var hIcon = new[] { WinApiProvider.IDI_APPLICATION, 1 }
                    .Select(i => WinApiProvider.LoadImage(WinApiProvider.GetModuleHandle(null), new IntPtr(i), 1, size, size, 0))
                    .FirstOrDefault(i => i != IntPtr.Zero);

                if (hIcon == IntPtr.Zero)
                {
                    return null;
                }
                else
                {
                    var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    // Do we need to delete object?
                    //var deleted = WinApiProvider.DeleteObject(hIcon);
                    return bitmapSource;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
