using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Collections;
using PasswordManager.Services;
using System;
using System.Collections.Generic;
using System.Linq;
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
            var vm = new MainWindowViewModel();
            return vm;
        }
        #endregion

        private readonly SettingsService _settingsService;
        private readonly ILogger<MainWindowViewModel> _logger;

        public event Action<CredentialViewModel> CredentialSelected;

        public PasswordsViewModel PasswordsViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }
        public ObservableCollectionDelayed<NavigationItemViewModel> NavigationItems { get; }

        private int _selectedNavigationItemIndex;
        public int SelectedNavigationItemIndex
        {
            get => _selectedNavigationItemIndex;
            set
            {
                SetProperty(ref _selectedNavigationItemIndex, value);
                if (value == NavigationItemViewModel.PasswordsNavigationItemIndex)
                {
                    PasswordsViewModel.SearchTextFocused = false;
                    PasswordsViewModel.SearchTextFocused = true;
                }
            }
        }

        private MainWindowViewModel() { }

        public MainWindowViewModel(
            PasswordsViewModel passwordsViewModel,
            SettingsService settingsService,
            SettingsViewModel settingsViewModel,
            ILogger<MainWindowViewModel> logger)
        {
            PasswordsViewModel = passwordsViewModel;
            SettingsViewModel = settingsViewModel;
            _settingsService = settingsService;
            _logger = logger;

            PasswordsViewModel.CredentialSelected += PasswordsViewModel_CredentialSelected;

            NavigationItems = new ObservableCollectionDelayed<NavigationItemViewModel>(new List<NavigationItemViewModel>()
            {
                PasswordsViewModel,
                SettingsViewModel
            });
        }

        private void PasswordsViewModel_CredentialSelected(CredentialViewModel credVM)
        {
            CredentialSelected?.Invoke(credVM);
        }

        private async Task LoadingAsync()
        {
            await PasswordsViewModel.LoadCredentialsAsync();
            PasswordsViewModel.SearchTextFocused = true;
        }

        private AsyncRelayCommand _loadingCommand;
        public AsyncRelayCommand LoadingCommand => _loadingCommand ??= new AsyncRelayCommand(LoadingAsync);
    }
}
