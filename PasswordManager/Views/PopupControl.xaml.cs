using PasswordManager.Helpers;
using PasswordManager.ViewModels;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PasswordManager.Views
{
    /// <summary>
    /// Interaction logic for PopupControl.xaml
    /// </summary>
    public partial class PopupControl : Popup
    {
        private PopupViewModel ViewModel => DataContext as PopupViewModel;

        public PopupControl(PopupViewModel popupViewModel)
        {
            InitializeComponent();

            DataContext = popupViewModel;
        }

        private void Popup_Opened(object sender, System.EventArgs e)
        {
            CredListBox.SelectedIndex = 0;
        }

        private void ListBoxItem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var mousePosition = Mouse.GetPosition(Child);
                var inputElement = CredListBox.InputHitTest(mousePosition);
                if (inputElement is FrameworkElement element)
                {
                    if (element.DataContext is CredentialViewModel credVM)
                    {
                        var inputData = credVM.NameFieldVM.Value;

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

                        // Send input simulate Ctrl + V
                        var uSent = WindowsKeyboard.SendInput((uint)inputs.Length, inputs, INPUT.Size);

                        IsOpen = false;
                    }
                }
            }
        }
    }
}
