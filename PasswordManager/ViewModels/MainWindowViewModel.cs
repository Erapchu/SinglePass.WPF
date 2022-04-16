using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Collections;
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
            var vm = new MainWindowViewModel();
            return vm;
        }
        #endregion

        public event Action<CredentialViewModel> CredentialSelected;

        private AsyncRelayCommand _loadingCommand;
        private NavigationItemViewModel _selectedNavigationItem;

        public AsyncRelayCommand LoadingCommand => _loadingCommand ??= new AsyncRelayCommand(LoadingAsync);
        public PasswordsViewModel PasswordsVM { get; }
        public SettingsViewModel SettingsVM { get; }
        public CloudSyncViewModel CloudSyncVM { get; }
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

                if (_selectedNavigationItem is CloudSyncViewModel cloudSyncViewModel)
                {
                    _ = cloudSyncViewModel.FetchUserInfoIfRequired();
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
            SettingsViewModel settingsViewModel,
            CloudSyncViewModel cloudsViewModel)
        {
            PasswordsVM = passwordsViewModel;
            CloudSyncVM = cloudsViewModel;
            SettingsVM = settingsViewModel;
            SelectedNavigationItem = PasswordsVM;

            PasswordsVM.CredentialSelected += PasswordsViewModel_CredentialSelected;
            CloudSyncVM.SyncCompleted += SettingsViewModel_SyncCompleted;

            NavigationItems = new ObservableCollectionDelayed<NavigationItemViewModel>(new List<NavigationItemViewModel>()
            {
                PasswordsVM,
                CloudSyncVM,
                SettingsVM,
            });
        }

        private void SettingsViewModel_SyncCompleted()
        {
            PasswordsVM.ReloadCredentials();
        }

        private void PasswordsViewModel_CredentialSelected(CredentialViewModel credVM)
        {
            CredentialSelected?.Invoke(credVM);
        }

        private async Task LoadingAsync()
        {
            PasswordsVM.ReloadCredentials();

            // Delay only for focus
            await Task.Delay(1);
            PasswordsVM.SearchTextFocused = true;
        }
    }
}
