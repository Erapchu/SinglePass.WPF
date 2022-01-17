using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using NLog;
using PasswordManager.Collections;
using PasswordManager.Services;
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

        private readonly SettingsService _settingsService;
        private readonly ILogger _logger;

        public PasswordsViewModel PasswordsViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }
        public ObservableCollectionDelayed<NavigationItemViewModel> NavigationItems { get; }

        private NavigationItemViewModel _selectedNavigationItem;
        public NavigationItemViewModel SelectedNavigationItem
        {
            get => _selectedNavigationItem;
            set
            {
                if (_selectedNavigationItem == value)
                    return;

                // Prevent unselected items
                if (value is null)
                    return;

                _selectedNavigationItem = value;
                OnPropertyChanged();
            }
        }

        private MainWindowViewModel() { }

        public MainWindowViewModel(
            PasswordsViewModel passwordsViewModel,
            SettingsService settingsService,
            SettingsViewModel settingsViewModel,
            ILogger logger)
        {
            PasswordsViewModel = passwordsViewModel;
            SettingsViewModel = settingsViewModel;
            _settingsService = settingsService;
            _logger = logger;

            NavigationItems = new ObservableCollectionDelayed<NavigationItemViewModel>(GenerateSettingsItem());
            SelectedNavigationItem = NavigationItems.First();
        }

        private List<NavigationItemViewModel> GenerateSettingsItem()
        {
            return new List<NavigationItemViewModel>()
            {
                new NavigationItemViewModel(
                    "Credentials",
                    MaterialDesignThemes.Wpf.PackIconKind.Password,
                    () => new PasswordsControl() { DataContext = PasswordsViewModel },
                    _logger),
                new NavigationItemViewModel(
                    "Settings",
                    MaterialDesignThemes.Wpf.PackIconKind.Settings,
                    () => new SettingsControl() { DataContext = SettingsViewModel },
                    _logger),
            };
        }

        private async Task LoadingAsync()
        {
            await PasswordsViewModel.LoadCredentialsAsync();
        }

        private AsyncRelayCommand _loadingCommand;
        public AsyncRelayCommand LoadingCommand => _loadingCommand ??= new AsyncRelayCommand(LoadingAsync);
    }
}
