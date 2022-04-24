using PasswordManager.Controls;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PasswordManager.Views
{
    /// <summary>
    /// Interaction logic for InputMaterialWindow.xaml
    /// </summary>
    public partial class InputMaterialWindow : MaterialWindow
    {
        public InputMaterialWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var p = new PopupControl();
            p.IsOpen = true;

            // TODO: Split code parts - determine position before showing, but first implement hotkeys
            // Obtain popup handle for placement
            IntPtr handle = ((HwndSource)PresentationSource.FromVisual(p.Child)).Handle;

            // TODO: See ConsoleTest project
            // Get position
            System.Threading.Thread.Sleep(1000);
            var hFore = GetForegroundWindow();
            var idAttach = GetWindowThreadProcessId(hFore, out uint id);
            var curThreadId = GetCurrentThreadId();
            // To attach to current thread
            var sa = AttachThreadInput(idAttach, curThreadId, true);
            var caretPos = GetCaretPos(out POINT caretPoint);
            ClientToScreen(hFore, ref caretPoint);
            // To dettach from current thread
            var sd = AttachThreadInput(idAttach, curThreadId, false);
            var data = string.Format("X={0}, Y={1}", caretPoint.X, caretPoint.Y);

            var rect = new RECT();
            GetWindowRect(handle, ref rect);
            SetWindowPos(handle, IntPtr.Zero, caretPoint.X, caretPoint.Y, rect.right - rect.left, rect.bottom - rect.top, SWP_NOZORDER);

            p.Focusable = false;
        }

        public const uint SWP_NOZORDER = 0x0004;

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetCaretPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

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
