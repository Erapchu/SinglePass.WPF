using System;
using System.Runtime.InteropServices;
using System.Windows;
using static SinglePass.WPF.Helpers.WinApiProvider;

namespace SinglePass.WPF.Helpers
{
    public static class WindowPositionHelper
    {
        public static bool CheckIsOnAnyScreen(Rect rect)
        {
            var intersects = false;
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
            {
                intersects = TryGetWorkArea(monitor, out var area)
                    && rect.Left < area.right && rect.Right > area.left
                    && rect.Top < area.bottom && rect.Bottom > area.top;
                return !intersects;
            }, IntPtr.Zero);

            return intersects;
        }

        public static void ShiftWindowToScreen(Window window)
        {
            var monitor = DpiUtilities.GetNearestMonitorFromPoint((int)window.Left, (int)window.Top);
            if (!TryGetWorkArea(monitor, out var area))
                return;

            if (window.Left + window.Width > area.right)
                window.Left = area.right - window.Width;

            if (window.Top + window.Height > area.bottom)
                window.Top = area.bottom - window.Height;
        }

        private static bool TryGetWorkArea(IntPtr monitor, out RECT area)
        {
            var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
            var success = GetMonitorInfo(monitor, ref info);
            area = info.WorkArea;
            return success;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public uint Size;
            public RECT MonitorArea;
            public RECT WorkArea;
            public uint Flags;
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        private delegate bool MonitorEnumerator(IntPtr monitor, IntPtr deviceContext, IntPtr bounds, IntPtr data);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayMonitors(IntPtr deviceContext, IntPtr clip, MonitorEnumerator callback, IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    }
}
