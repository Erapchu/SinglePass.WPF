using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SinglePass.Core.Models;
using SinglePass.WPF.Services;
using SinglePass.WPF.Views.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        public TrashViewModel TrashVM { get; }
        public UpdateService Updates { get; }
        public NavigationItemViewModel AllPasswordsNavigation { get; }
        public NavigationItemViewModel SettingsNavigation { get; }
        public NavigationItemViewModel TrashNavigation { get; }
        public ObservableCollection<NavigationItemViewModel> GroupNavigation { get; } = new();

        /// <summary>One item per entry type. Shown even when there are no entries of that type.</summary>
        public IReadOnlyList<NavigationItemViewModel> CategoryNavigation { get; }

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
                if (value == TrashNavigation)
                    _ = TrashVM.RefreshAsync();
                else if (value != SettingsNavigation)
                    PasswordsVM.SelectFilter(value.Group?.Id, value.Category);
                SetProperty(ref _selectedNavigationItem, value);
            }
        }

        private MainWindowViewModel() { }

        public MainWindowViewModel(
            PasswordsViewModel passwordsViewModel,
            SettingsViewModel settingsViewModel,
            CloudSyncViewModel cloudsViewModel,
            TrashViewModel trashViewModel,
            UpdateService updateService)
        {
            PasswordsVM = passwordsViewModel;
            CloudSyncVM = cloudsViewModel;
            SettingsVM = settingsViewModel;
            TrashVM = trashViewModel;
            // Entries restored from the trash go back to the list.
            TrashVM.VaultChanged += TrashViewModel_VaultChanged;

            PasswordsVM.CredentialSelected += PasswordsViewModel_CredentialSelected;
            PasswordsVM.ScrollIntoViewRequired += PasswordsVM_ScrollIntoViewRequired;
            CloudSyncVM.SyncCompleted += SettingsViewModel_SyncCompleted;
            // A restored backup changes credentials and groups the same way a sync does.
            SettingsVM.VaultRestored += SettingsViewModel_SyncCompleted;

            AllPasswordsNavigation = new NavigationItemViewModel(
                "AllPasswords",
                "\uE72E",
                () => new PasswordsControl { DataContext = PasswordsVM });
            CategoryNavigation =
            [
                new NavigationItemViewModel("CategoryLogins", "", CredentialType.Login, AllPasswordsNavigation),
                new NavigationItemViewModel("CategorySshKeys", "", CredentialType.SshKey, AllPasswordsNavigation),
                new NavigationItemViewModel("CategoryCards", "", CredentialType.Card, AllPasswordsNavigation),
                new NavigationItemViewModel("CategoryHosts", "", CredentialType.Host, AllPasswordsNavigation),
            ];
            TrashNavigation = new NavigationItemViewModel(
                "Trash",
                "\uE74D",
                () => new TrashControl { DataContext = TrashVM });
            SettingsNavigation = new NavigationItemViewModel(
                nameof(SinglePass.Language.Properties.Resources.Settings),
                "\uE713",
                () => new SettingsControl { DataContext = SettingsVM });
            foreach (var group in PasswordsVM.PasswordGroups)
                GroupNavigation.Add(new NavigationItemViewModel(group, AllPasswordsNavigation));

            // UpdateService outlives this view model (it is recreated on every unlock), so subscribe weakly.
            Updates = updateService;
            SettingsNavigation.HasBadge = updateService.IsUpdateReady;
            PropertyChangedEventManager.AddHandler(updateService, UpdateService_IsUpdateReadyChanged, nameof(UpdateService.IsUpdateReady));
        }

        private void UpdateService_IsUpdateReadyChanged(object sender, PropertyChangedEventArgs e) =>
            SettingsNavigation.HasBadge = Updates.IsUpdateReady;

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
        /// <summary>Selects the entry if the current list shows it (the browser extension's "open in SinglePass").</summary>
        internal void SelectCredential(Guid id)
        {
            var item = PasswordsVM.DisplayedCredentials.FirstOrDefault(credential => credential.Model.Id == id);
            if (item == null)
                return;

            PasswordsVM.SelectedCredentialVM = item;
            ScrollIntoViewRequired?.Invoke(item);
        }

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
            var groups = PasswordsVM.PasswordGroups.ToDictionary(group => group.Id);
            foreach (var navigation in GroupNavigation.ToList())
            {
                if (groups.Remove(navigation.Group.Id, out var group))
                {
                    if (navigation.Group != group)
                        navigation.UpdateGroup(group);
                    continue;
                }

                if (SelectedNavigationItem == navigation)
                    Navigate(AllPasswordsNavigation);
                GroupNavigation.Remove(navigation);
            }

            foreach (var group in groups.Values)
                GroupNavigation.Add(new NavigationItemViewModel(group, AllPasswordsNavigation));

            await PasswordsVM.ReloadCredentialsAsync();
            if (SelectedNavigationItem == TrashNavigation)
                await TrashVM.RefreshAsync();
        }

        private async void TrashViewModel_VaultChanged() => await PasswordsVM.ReloadCredentialsAsync();

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
