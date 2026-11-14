using SinglePass.WPF.Enums;
using System;
using System.Windows;
using System.Windows.Interop;

namespace SinglePass.WPF.Views.Windows
{
    /// <summary>
    /// Interaction logic for HiddenInterprocessWindow.xaml
    /// </summary>
    public partial class HiddenInterprocessWindow : Window
    {
        private readonly Action _showVault;

        public HiddenInterprocessWindow(Action showVault)
        {
            _showVault = showVault;
            InitializeComponent();
        }

        public HwndSource InitWithoutShowing()
        {
            var handle = new WindowInteropHelper(this).EnsureHandle();
            HwndSource source = HwndSource.FromHwnd(handle);
            source.AddHook(WndProc);
            return source;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Handle messages...
            switch (msg)
            {
                case (int)CustomWindowsMessages.WM_SHOW_MAIN_WINDOW:
                    {
                        Dispatcher.InvokeAsync(_showVault);
                        break;
                    }
            }

            return IntPtr.Zero;
        }
    }
}
