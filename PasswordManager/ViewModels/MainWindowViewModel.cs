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

        public event Action<CredentialViewModel> CredentialSelected;

        private AsyncRelayCommand _loadingCommand;
        private NavigationItemViewModel _selectedNavigationItem;

        public AsyncRelayCommand LoadingCommand => _loadingCommand ??= new AsyncRelayCommand(LoadingAsync);
        public PasswordsViewModel PasswordsViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }
        public ObservableCollectionDelayed<NavigationItemViewModel> NavigationItems { get; }

        public NavigationItemViewModel SelectedNavigationItem
        {
            get => _selectedNavigationItem;
            set
            {
                if (_selectedNavigationItem != null)
                    _selectedNavigationItem.IsVisible = false;

                SetProperty(ref _selectedNavigationItem, value);
                _selectedNavigationItem.IsVisible = true;

                if (_selectedNavigationItem is SettingsViewModel settingsViewModel)
                {
                    _ = settingsViewModel.FetchUserInfoIfRequired();
                }

                if (_selectedNavigationItem is PasswordsViewModel passwordsViewModel)
                {
                    passwordsViewModel.SearchTextFocused = false;
                    passwordsViewModel.SearchTextFocused = true;
                }
            }
        }

        private MainWindowViewModel() { }

        public MainWindowViewModel(
            PasswordsViewModel passwordsViewModel,
            SettingsViewModel settingsViewModel)
        {
            PasswordsViewModel = passwordsViewModel;
            SettingsViewModel = settingsViewModel;
            SelectedNavigationItem = PasswordsViewModel;

            PasswordsViewModel.CredentialSelected += PasswordsViewModel_CredentialSelected;
            SettingsViewModel.SyncCompleted += SettingsViewModel_SyncCompleted;

            NavigationItems = new ObservableCollectionDelayed<NavigationItemViewModel>(new List<NavigationItemViewModel>()
            {
                PasswordsViewModel,
                SettingsViewModel
            });
        }

        private void SettingsViewModel_SyncCompleted()
        {
            PasswordsViewModel.ReloadCredentials();
        }

        private void PasswordsViewModel_CredentialSelected(CredentialViewModel credVM)
        {
            CredentialSelected?.Invoke(credVM);
        }

        private async Task LoadingAsync()
        {
            PasswordsViewModel.ReloadCredentials();
            
            // Delay only for focus
            await Task.Delay(1);
            PasswordsViewModel.SearchTextFocused = true;
        }
    }
}
