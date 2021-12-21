using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Helpers;
using PasswordManager.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class MainWindowViewModel : ObservableRecipient
    {
        #region Design time instance
        private static readonly Lazy<MainWindowViewModel> _lazy = new(() => new MainWindowViewModel(null));
        public static MainWindowViewModel DesignTimeInstance => _lazy.Value;
        #endregion

        private readonly SettingsService _settingsService;

        public ObservableCollection<CredentialViewModel> Credentials { get; private set; }

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
                var credentials = await _settingsService.LoadCredentialsFromFileAsync();
                Credentials = new ObservableCollection<CredentialViewModel>(credentials.Select(c => new CredentialViewModel(c)));
                OnPropertyChanged(nameof(Credentials));
            }
            finally
            {
                Loading = false;
            }
        }

        private RelayCommand _loadingCommand;
        public RelayCommand LoadingCommand => _loadingCommand ??= new RelayCommand(async () => await LoadingAsync());
    }
}
