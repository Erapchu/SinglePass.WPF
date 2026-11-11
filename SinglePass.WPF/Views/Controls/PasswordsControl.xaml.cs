using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

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

        private void SortPopup_Opened(object sender, System.EventArgs e)
        {
            SortByName.Focus();
        }

        private void SortPopup_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SortPopup.SetCurrentValue(Popup.IsOpenProperty, false);
                SortButton.Focus();
                e.Handled = true;
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
