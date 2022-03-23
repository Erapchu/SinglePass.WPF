using System;
using System.Runtime.InteropServices;

namespace PasswordManager.Helpers
{
    internal static class Windows_keybd_event
    {
        public const byte VK_V = 0x56;
        public const byte VK_CONTROL = 0x11;
        public const int KEYEVENTF_KEYUP = 0x0002;

        public const uint WM_PASTE = 0x0302;
        public const uint WM_KEYDOWN = 0x0100;
        public const uint WM_KEYUP = 0x0101;

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    }
}
