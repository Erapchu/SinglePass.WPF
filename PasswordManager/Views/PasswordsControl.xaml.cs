using System.Threading.Tasks;
using System.Windows.Controls;

namespace PasswordManager.Views
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

        private async void UserControl_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool v && v)
            {
                await Task.Delay(50);
                SearchTextBox.Focus();
            }
        }
    }
}
