using System;
using System.Runtime.InteropServices;

namespace PasswordManager.Helpers
{
    internal static class WinApiProvider
    {
        public const int IDI_APPLICATION = 0x7F00;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadImage(IntPtr hinst, IntPtr lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);
    }
}
