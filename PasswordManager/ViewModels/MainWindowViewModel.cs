using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Collections;
using PasswordManager.Helpers;
using PasswordManager.Services;
using PasswordManager.Views;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class MainWindowViewModel : ObservableRecipient
    {
        #region Design time instance
        private static readonly Lazy<MainWindowViewModel> _lazy = new(GetDesignTimeVM);
        public static MainWindowViewModel DesignTimeInstance => _lazy.Value;

        private static MainWindowViewModel GetDesignTimeVM()
        {
            var vm = new MainWindowViewModel(null);
            vm.Loading = true;
            return vm;
        }
        #endregion

        private readonly SettingsService _settingsService;

        public List<PassFieldViewModel> DefaultFields { get; } = new List<PassFieldViewModel>()
        {
            new PassFieldViewModel(new Models.PassField() { Name = "Name", IconKind = PackIconKind.Information }),
            new PassFieldViewModel(new Models.PassField() { Name = "Login", IconKind = PackIconKind.Account }),
            new PassFieldViewModel(new Models.PassField() { Name = "Password", IconKind = PackIconKind.Key }),
            new PassFieldViewModel(new Models.PassField() { Name = "Other", IconKind = PackIconKind.InformationOutline }),
        };

        public ObservableCollectionDelayed<CredentialViewModel> Credentials { get; } = new ObservableCollectionDelayed<CredentialViewModel>();

        private CredentialViewModel _selectedCredential;
        public CredentialViewModel SelectedCredential
        {
            get => _selectedCredential;
            set => SetProperty(ref _selectedCredential, value);
        }

        private bool _loading;
        public bool Loading
        {
            get => _loading;
            set => SetProperty(ref _loading, value);
        }

        public MainWindowViewModel(SettingsService settingsService)
        {
            if (MvvmHelper.IsInDesignMode)
                return;

            _settingsService = settingsService;
        }

        private async Task LoadingAsync()
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
            var result = await DialogHost.Show(new NewCredentialsDialog(), MvvmHelper.MainWindowDialogName);
        }

        private AsyncRelayCommand _loadingCommand;
        public AsyncRelayCommand LoadingCommand => _loadingCommand ??= new AsyncRelayCommand(LoadingAsync);

        private AsyncRelayCommand _addCredentialCommand;
        public AsyncRelayCommand AddCredentialCommand => _addCredentialCommand ??= new AsyncRelayCommand(AddCredentialAsync);
    }
}
