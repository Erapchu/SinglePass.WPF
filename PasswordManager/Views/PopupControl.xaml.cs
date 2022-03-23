using PasswordManager.Helpers;
using System;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace PasswordManager.Views
{
    /// <summary>
    /// Interaction logic for PopupControl.xaml
    /// </summary>
    public partial class PopupControl : Popup
    {
        public PopupControl()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var inputData = "pasted from popup";

            WindowsClipboard.SetText(inputData);

            Windows_keybd_event.keybd_event(Windows_keybd_event.VK_CONTROL, 0, 0, UIntPtr.Zero);
            Windows_keybd_event.keybd_event(Windows_keybd_event.VK_V, 0, 0, UIntPtr.Zero);
            Windows_keybd_event.keybd_event(Windows_keybd_event.VK_V, 0, Windows_keybd_event.KEYEVENTF_KEYUP, UIntPtr.Zero);
            Windows_keybd_event.keybd_event(Windows_keybd_event.VK_CONTROL, 0, Windows_keybd_event.KEYEVENTF_KEYUP, UIntPtr.Zero);

            IsOpen = false;
        }
    }
}
