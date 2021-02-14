using PasswordManager.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class LoginWindowViewModel : ViewModelBase
    {
        #region Design time instance
        private static readonly Lazy<LoginWindowViewModel> _lazy = new Lazy<LoginWindowViewModel>(() => new LoginWindowViewModel());
        public static LoginWindowViewModel DesignTimeInstance => _lazy.Value;
        #endregion
    }
}
