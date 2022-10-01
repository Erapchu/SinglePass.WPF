using MaterialDesignThemes.Wpf;
using SinglePass.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SinglePass.WPF.Views.Controls
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

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            var darkColor = BaseTheme.Dark.GetBaseTheme().MaterialDesignPaper;
            var lightColor = BaseTheme.Light.GetBaseTheme().MaterialDesignPaper;

            DarkButton.Background = new SolidColorBrush(darkColor);
            LightButton.Background = new SolidColorBrush(lightColor);
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
