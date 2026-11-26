using Erapkey.ViewModels;
using Erapkey.ViewModels.Entries;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Erapkey.Views.Controls.Entries
{
    public partial class LoginDetailsView : UserControl
    {
        private PassFieldViewModel _password;

        public LoginDetailsView()
        {
            InitializeComponent();
            Loaded += (_, _) => FollowPassword(DataContext as LoginViewModel);
            Unloaded += (_, _) => FollowPassword(null);
        }

        private void View_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // The card reuses this view when another login is selected: close the history of the previous one.
            PasswordHistory.IsExpanded = false;
            FollowPassword(e.NewValue as LoginViewModel);
        }

        private void FollowPassword(LoginViewModel login)
        {
            if (_password != null)
                _password.PropertyChanged -= Password_PropertyChanged;
            _password = login?.PasswordFieldVM;
            if (_password != null)
                _password.PropertyChanged += Password_PropertyChanged;
            UpdatePasswordMask();
        }

        private void Password_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PassFieldViewModel.Value))
                UpdatePasswordMask();
        }

        private void UpdatePasswordMask() =>
            PasswordMask.Password = new string('*', _password?.Value?.Length ?? 0);

        private void PasswordHistory_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Expander { IsVisible: false } history)
                history.IsExpanded = false;
        }
    }
}
