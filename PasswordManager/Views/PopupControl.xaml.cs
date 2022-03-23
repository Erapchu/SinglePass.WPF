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

            INPUT[] inputs = new INPUT[4];

            inputs[0].type = WindowsKeyboard.INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = WindowsKeyboard.VK_CONTROL;

            inputs[1].type = WindowsKeyboard.INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = WindowsKeyboard.VK_V;

            inputs[2].type = WindowsKeyboard.INPUT_KEYBOARD;
            inputs[2].U.ki.wVk = WindowsKeyboard.VK_V;
            inputs[2].U.ki.dwFlags = WindowsKeyboard.KEYEVENTF_KEYUP;

            inputs[3].type = WindowsKeyboard.INPUT_KEYBOARD;
            inputs[3].U.ki.wVk = WindowsKeyboard.VK_CONTROL;
            inputs[3].U.ki.dwFlags = WindowsKeyboard.KEYEVENTF_KEYUP;

            var uSent = WindowsKeyboard.SendInput((uint)inputs.Length, inputs, INPUT.Size);

            //WindowsKeyboard.keybd_event(WindowsKeyboard.VK_CONTROL, 0, 0, UIntPtr.Zero);
            //WindowsKeyboard.keybd_event(WindowsKeyboard.VK_V, 0, 0, UIntPtr.Zero);
            //WindowsKeyboard.keybd_event(WindowsKeyboard.VK_V, 0, WindowsKeyboard.KEYEVENTF_KEYUP, UIntPtr.Zero);
            //WindowsKeyboard.keybd_event(WindowsKeyboard.VK_CONTROL, 0, WindowsKeyboard.KEYEVENTF_KEYUP, UIntPtr.Zero);

            IsOpen = false;
        }
    }
}
