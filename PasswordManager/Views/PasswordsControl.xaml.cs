using PasswordManager.ViewModels;
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

        private void ListBox_CleanUpVirtualizedItem(object sender, CleanUpVirtualizedItemEventArgs e)
        {
            if (e.Value is CredentialViewModel credVM && credVM.IsExpanded)
            {
                e.Cancel = true;
            }
        }
    }
}
