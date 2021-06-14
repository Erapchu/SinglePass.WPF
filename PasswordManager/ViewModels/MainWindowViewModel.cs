using PasswordManager.Helpers;
using PasswordManager.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        #region Design time instance
        private static readonly Lazy<MainWindowViewModel> _lazy = new Lazy<MainWindowViewModel>(() => new MainWindowViewModel());
        public static MainWindowViewModel DesignTimeInstance => _lazy.Value;
        #endregion

        public ObservableCollection<CredentialViewModel> Credentials { get; private set; } = new ObservableCollection<CredentialViewModel>();

        #region SelectedCredential
        private CredentialViewModel _selectedCredential;
        public CredentialViewModel SelectedCredential
        {
            get => _selectedCredential;
            set
            {
                if (_selectedCredential == value)
                    return;

                _selectedCredential = value;
                RaisePropertyChanged();
            }
        }
        #endregion

        public MainWindowViewModel()
        {
            foreach (var cred in LoadCredentials())
                Credentials.Add(cred);
        }

        private IEnumerable<CredentialViewModel> LoadCredentials()
        {
            // TODO: load credentials from encrypted file
            return new List<CredentialViewModel> { new CredentialViewModel(new Credential() { Name = "Credential", Login="hello", Password="secret", Other="info" }) };
        }
    }
}
