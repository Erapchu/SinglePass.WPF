using PasswordManager.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace PasswordManager.Views
{
    /// <summary>
    /// Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        private SettingsViewModel ViewModel => DataContext as SettingsViewModel;

        public SettingsControl()
        {
            InitializeComponent();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.NewPassword = NewPasswordBox.Password;
            }
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue != null && e.OldValue is SettingsViewModel oldSettingsViewModel)
            {
                oldSettingsViewModel.NewPasswordIsSet -= SettingsViewModel_NewPasswordIsSet;
            }

            if (e.NewValue != null && e.NewValue is SettingsViewModel newSettingsViewModel)
            {
                newSettingsViewModel.NewPasswordIsSet += SettingsViewModel_NewPasswordIsSet;
            }
        }

        private void SettingsViewModel_NewPasswordIsSet()
        {
            NewPasswordBox.Password = string.Empty;
        }
    }
}
