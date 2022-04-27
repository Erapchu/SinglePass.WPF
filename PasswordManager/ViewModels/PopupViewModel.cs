using Microsoft.Toolkit.Mvvm.ComponentModel;
using PasswordManager.Collections;
using PasswordManager.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class PopupViewModel : ObservableObject
    {
        #region Design time instance
        private static readonly Lazy<PopupViewModel> _lazy = new(GetDesignTimeVM);
        public static PopupViewModel DesignTimeInstance => _lazy.Value;

        private static PopupViewModel GetDesignTimeVM()
        {
            var vm = new PopupViewModel();
            return vm;
        }
        #endregion

        private readonly CredentialsCryptoService _credentialsCryptoService;

        public ObservableCollection<CredentialViewModel> DisplayedCredentials { get; private set; }

        private CredentialViewModel _selectedCredentialVM;
        public CredentialViewModel SelectedCredentialVM
        {
            get => _selectedCredentialVM;
            set => SetProperty(ref _selectedCredentialVM, value);
        }

        private PopupViewModel() { }

        public PopupViewModel(CredentialsCryptoService credentialsCryptoService)
        {
            _credentialsCryptoService = credentialsCryptoService;

            var creds = _credentialsCryptoService.Credentials.Select(cr => new CredentialViewModel(cr)).ToList();
            DisplayedCredentials = new ObservableCollection<CredentialViewModel>(creds);
        }
    }
}
