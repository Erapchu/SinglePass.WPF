using PasswordManager.Controls;
using System.Windows;

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
            p.Focusable = false;
        }
    }
}
