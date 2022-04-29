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

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("oleacc.dll")]
        internal static extern int AccessibleObjectFromWindow(
         IntPtr hwnd,
         uint id,
         ref Guid iid,
         [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object ppvObject);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetCaretPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;
        public const uint WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll")]
        public static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        // This helper static method is required because the 32-bit version of user32.dll does not contain this API
        // (on any versions of Windows), so linking the method will fail at run-time. The bridge dispatches the request
        // to the correct function (GetWindowLong in 32-bit mode and GetWindowLongPtr in 64-bit mode)
        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public const uint SWP_NOZORDER = 0x0004;

        public const int CHILDID_SELF = 0;
        public const uint OBJID_WINDOW = 0x00000000;
        public const uint OBJID_SYSMENU = 0xFFFFFFFF;
        public const uint OBJID_TITLEBAR = 0xFFFFFFFE;
        public const uint OBJID_MENU = 0xFFFFFFFD;
        public const uint OBJID_CLIENT = 0xFFFFFFFC;
        public const uint OBJID_VSCROLL = 0xFFFFFFFB;
        public const uint OBJID_HSCROLL = 0xFFFFFFFA;
        public const uint OBJID_SIZEGRIP = 0xFFFFFFF9;
        public const uint OBJID_CARET = 0xFFFFFFF8;
        public const uint OBJID_CURSOR = 0xFFFFFFF7;
        public const uint OBJID_ALERT = 0xFFFFFFF6;
        public const uint OBJID_SOUND = 0xFFFFFFF5;

        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public int cbSize;
            public GuiThreadInfoFlags flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public System.Drawing.Rectangle rcCaret;
        }

        [Flags]
        public enum GuiThreadInfoFlags
        {
            GUI_CARETBLINKING = 0x00000001,
            GUI_INMENUMODE = 0x00000004,
            GUI_INMOVESIZE = 0x00000002,
            GUI_POPUPMENUMODE = 0x00000010,
            GUI_SYSTEMMENUMODE = 0x00000008
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }

            public static implicit operator System.Drawing.Point(POINT p)
            {
                return new System.Drawing.Point(p.X, p.Y);
            }

            public static implicit operator POINT(System.Drawing.Point p)
            {
                return new POINT(p.X, p.Y);
            }
        }
    }
}
