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
            await DialogHost.Show(new NewCredentialsDialog(), MvvmHelper.MainWindowDialogName);
        }

        private AsyncRelayCommand _loadingCommand;
        public AsyncRelayCommand LoadingCommand => _loadingCommand ??= new AsyncRelayCommand(LoadingAsync);

        private AsyncRelayCommand _addCredentialCommand;
        public AsyncRelayCommand AddCredentialCommand => _addCredentialCommand ??= new AsyncRelayCommand(AddCredentialAsync);
    }
}
