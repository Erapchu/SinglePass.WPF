using MaterialDesignThemes.Wpf;
using PasswordManager.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
            //var popupWindow = new PopupMaterialWindow();

            //var hwnd = new WindowInteropHelper(popupWindow).EnsureHandle();
            //var style = GetWindowLong(hwnd, GWL_EXSTYLE);
            //style |= WS_EX_NOACTIVATE;
            //SetWindowLong(hwnd, GWL_EXSTYLE, style);

            //popupWindow.Show();

            var p = new PopupControl();
            p.IsOpen = true;
            p.Focusable = false;

            //var popup = new Popup();
            //var t = new TextBlock();
            //t.Foreground = Brushes.White;
            //t.Text = "test";
            //popup.Child = t;
            //popup.IsOpen = true;
        }

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        static int GWL_STYLE = -16;
        static int GWL_EXSTYLE = -20;
        static uint WS_EX_NOACTIVATE = 0x08000000;
    }
}
