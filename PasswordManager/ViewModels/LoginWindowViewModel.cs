using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;

namespace PasswordManager.ViewModels
{
    public class LoginWindowViewModel : ObservableRecipient
    {
        #region Design time instance
        private static readonly Lazy<LoginWindowViewModel> _lazy = new Lazy<LoginWindowViewModel>(() => new LoginWindowViewModel());
        public static LoginWindowViewModel DesignTimeInstance => _lazy.Value;
        #endregion
    }
}
