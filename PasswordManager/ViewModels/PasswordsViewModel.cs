using Autofac;
using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Collections;
using PasswordManager.Helpers;
using PasswordManager.Services;
using PasswordManager.Views;
using System;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class PasswordsViewModel : ObservableRecipient
    {
        #region Design time instance
        private static readonly Lazy<PasswordsViewModel> _lazy = new(GetDesignTimeVM);
        public static PasswordsViewModel DesignTimeInstance => _lazy.Value;

        private static PasswordsViewModel GetDesignTimeVM()
        {
            var vm = new PasswordsViewModel();
            return vm;
        }
        #endregion

        private readonly SettingsService _settingsService;
        private readonly ILifetimeScope _lifetimeScope;

        private bool _loading;
        public bool Loading
        {
            get => _loading;
            set => SetProperty(ref _loading, value);
        }

        public ObservableCollectionDelayed<CredentialViewModel> Credentials { get; } = new ObservableCollectionDelayed<CredentialViewModel>();

        private CredentialViewModel _selectedCredential;
        public CredentialViewModel SelectedCredential
        {
            get => _selectedCredential;
            set => SetProperty(ref _selectedCredential, value);
        }

        private PasswordsViewModel() { }

        public PasswordsViewModel(ILifetimeScope lifetimeScope, SettingsService settingsService)
        {
            _settingsService = settingsService;
            _lifetimeScope = lifetimeScope;
        }

        public async Task LoadCredentialsAsync()
        {
            try
            {
                Loading = true;
                var credentials = await _settingsService.LoadCredentialsAsync();
                using var delayed = Credentials.DelayNotifications();
                foreach (var cred in credentials)
                {
                    delayed.Add(new CredentialViewModel(cred));
                }
            }
            finally
            {
                Loading = false;
            }
        }

        private async Task AddCredentialAsync()
        {
            var newCredDialog = new NewCredentialsDialog
            {
                DataContext = _lifetimeScope.Resolve<NewCredentialsViewModel>()
            };
            var result = await DialogHost.Show(newCredDialog, MvvmHelper.MainWindowDialogName);
        }

        private AsyncRelayCommand _addCredentialCommand;
        public AsyncRelayCommand AddCredentialCommand => _addCredentialCommand ??= new AsyncRelayCommand(AddCredentialAsync);
    }
}
