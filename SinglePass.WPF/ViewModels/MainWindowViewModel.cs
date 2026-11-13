using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SinglePass.WPF.Models;
using SinglePass.WPF.Views.Controls;
using System;
using System.Collections.ObjectModel;
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
        public NavigationItemViewModel AllPasswordsNavigation { get; }
        public NavigationItemViewModel SettingsNavigation { get; }
        public ObservableCollection<NavigationItemViewModel> GroupNavigation { get; } = new();

        private NavigationItemViewModel _selectedNavigationItem;
        public NavigationItemViewModel SelectedNavigationItem
        {
            get => _selectedNavigationItem;
            private set
            {
                if (value == _selectedNavigationItem)
                    return;

                if (_selectedNavigationItem != null)
                    _selectedNavigationItem.IsSelected = false;
                value.IsSelected = true;
                if (value != SettingsNavigation)
                    PasswordsVM.SelectGroup(value.Group?.Id);
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

            AllPasswordsNavigation = new NavigationItemViewModel(
                "AllPasswords",
                "\uE72E",
                () => new PasswordsControl { DataContext = PasswordsVM });
            SettingsNavigation = new NavigationItemViewModel(
                nameof(SinglePass.Language.Properties.Resources.Settings),
                "\uE713",
                () => new SettingsControl { DataContext = SettingsVM });
            foreach (var group in PasswordsVM.PasswordGroups)
                GroupNavigation.Add(new NavigationItemViewModel(group, AllPasswordsNavigation));
        }

        internal async Task AddGroupAsync(PasswordGroup group)
        {
            if (!await PasswordsVM.AddGroupAsync(group))
                return;

            var navigation = new NavigationItemViewModel(group, AllPasswordsNavigation);
            GroupNavigation.Add(navigation);
            Navigate(navigation);
        }

        internal async Task EditGroupAsync(NavigationItemViewModel navigation, PasswordGroup group)
        {
            if (await PasswordsVM.EditGroupAsync(group))
                navigation.UpdateGroup(group);
        }

        internal async Task DeleteGroupAsync(NavigationItemViewModel navigation)
        {
            if (!await PasswordsVM.DeleteGroupAsync(navigation.Group))
                return;

            if (SelectedNavigationItem == navigation)
                Navigate(AllPasswordsNavigation);
            GroupNavigation.Remove(navigation);
        }

        [RelayCommand]
        private void Navigate(NavigationItemViewModel item)
        {
            if (item != null)
                SelectedNavigationItem = item;
        }

        private void PasswordsVM_ScrollIntoViewRequired(CredentialViewModel credVM)
        {
            ScrollIntoViewRequired?.Invoke(credVM);
        }

        private async void SettingsViewModel_SyncCompleted()
        {
            await PasswordsVM.ReloadCredentialsAsync();
        }

        private void PasswordsViewModel_CredentialSelected(CredentialViewModel credVM)
        {
            CredentialSelected?.Invoke(credVM);
        }

        [RelayCommand]
        private async Task LoadingAsync()
        {
            Navigate(AllPasswordsNavigation);
            await PasswordsVM.LoadInitialCredentialsAsync();
        }
    }
}
