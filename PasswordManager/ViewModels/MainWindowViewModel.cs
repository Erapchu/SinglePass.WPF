using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Collections;
using PasswordManager.Views;
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
        public PasswordsViewModel PasswordsVM { get; }
        public SettingsViewModel SettingsVM { get; }
        public CloudSyncViewModel CloudSyncVM { get; }
        public ObservableCollectionDelayed<NavigationItemViewModel> NavigationItems { get; }

        public NavigationItemViewModel SelectedNavigationItem
        {
            get => _selectedNavigationItem;
            set
            {
                // Prevent unselect item
                if (value is null)
                    return;

                SetProperty(ref _selectedNavigationItem, value);
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

            PasswordsVM.CredentialSelected += PasswordsViewModel_CredentialSelected;
            CloudSyncVM.SyncCompleted += SettingsViewModel_SyncCompleted;

            NavigationItems = new ObservableCollectionDelayed<NavigationItemViewModel>(new List<NavigationItemViewModel>()
            {
                new NavigationItemViewModel(
                    PasswordManager.Language.Properties.Resources.Passwords,
                    PackIconKind.Password,
                    () => new PasswordsControl() { DataContext = PasswordsVM }),
                new NavigationItemViewModel(
                    PasswordManager.Language.Properties.Resources.CloudSync,
                    PackIconKind.Cloud,
                    () => new CloudSyncControl() { DataContext = CloudSyncVM }),
                new NavigationItemViewModel(
                    PasswordManager.Language.Properties.Resources.Settings,
                    PackIconKind.Settings,
                    () => new SettingsControl() { DataContext = SettingsVM }),
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
            // Delay to boost loading of the window
            await Task.Delay(1);

            SelectedNavigationItem = NavigationItems.FirstOrDefault();
            PasswordsVM.ReloadCredentials();
        }
    }
}
