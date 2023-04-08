using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using SinglePass.WPF.Collections;
using SinglePass.WPF.Views;
using SinglePass.WPF.Views.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SinglePass.WPF.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
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
        public event Action<CredentialViewModel> ScrollIntoViewRequired;

        public PasswordsViewModel PasswordsVM { get; }
        public SettingsViewModel SettingsVM { get; }
        public CloudSyncViewModel CloudSyncVM { get; }
        public ObservableCollectionDelayed<NavigationItemViewModel> NavigationItems { get; }

        private NavigationItemViewModel _selectedNavigationItem;
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
            PasswordsVM.ScrollIntoViewRequired += PasswordsVM_ScrollIntoViewRequired;
            CloudSyncVM.SyncCompleted += SettingsViewModel_SyncCompleted;

            NavigationItems = new ObservableCollectionDelayed<NavigationItemViewModel>(new List<NavigationItemViewModel>()
            {
                new NavigationItemViewModel(
                    SinglePass.Language.Properties.Resources.Passwords,
                    PackIconKind.Password,
                    () => new PasswordsControl() { DataContext = PasswordsVM }),
                new NavigationItemViewModel(
                    SinglePass.Language.Properties.Resources.CloudSync,
                    PackIconKind.Cloud,
                    () => new CloudSyncControl() { DataContext = CloudSyncVM }),
                new NavigationItemViewModel(
                    SinglePass.Language.Properties.Resources.Settings,
                    PackIconKind.Settings,
                    () => new SettingsControl() { DataContext = SettingsVM }),
            });
        }

        private void PasswordsVM_ScrollIntoViewRequired(CredentialViewModel credVM)
        {
            ScrollIntoViewRequired?.Invoke(credVM);
        }

        private void SettingsViewModel_SyncCompleted()
        {
            PasswordsVM.ReloadCredentials();
        }

        private void PasswordsViewModel_CredentialSelected(CredentialViewModel credVM)
        {
            CredentialSelected?.Invoke(credVM);
        }

        [RelayCommand]
        private async Task LoadingAsync()
        {
            // Delay to boost loading of the window
            await Task.Delay(1);

            SelectedNavigationItem = NavigationItems.FirstOrDefault();
            PasswordsVM.ReloadCredentials();
        }
    }
}
