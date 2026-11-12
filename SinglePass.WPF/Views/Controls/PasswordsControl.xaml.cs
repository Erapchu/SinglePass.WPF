using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SinglePass.WPF.Views.Controls
{
    /// <summary>
    /// Interaction logic for PasswordsControl.xaml
    /// </summary>
    public partial class PasswordsControl : UserControl
    {
        public PasswordsControl()
        {
            InitializeComponent();
        }

        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { ContextMenu: { } menu } button)
            {
                menu.PlacementTarget = button;
                menu.IsOpen = true;
            }
        }

        private async void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool v && v)
            {
                await Task.Delay(50);
                SearchTextBox.Focus();
            }
        }
    }
}
