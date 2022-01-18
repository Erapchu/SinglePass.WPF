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

        public static int PasswordsNavigationItemIndex { get; }
        public static int SettingsNavigationItemIndex { get; } = 1;

        public PasswordsViewModel PasswordsViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }

        public ObservableCollectionDelayed<NavigationItemViewModel> NavigationItems { get; }

        private int _selectedNavigationItemIndex;
        public int SelectedNavigationItemIndex
        {
            get => _selectedNavigationItemIndex;
            set => SetProperty(ref _selectedNavigationItemIndex, value);
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
        }

        private List<NavigationItemViewModel> GenerateSettingsItem()
        {
            return new List<NavigationItemViewModel>()
            {
                new NavigationItemViewModel(
                    "Credentials",
                    MaterialDesignThemes.Wpf.PackIconKind.Password,
                    PasswordsNavigationItemIndex),
                new NavigationItemViewModel(
                    "Settings",
                    MaterialDesignThemes.Wpf.PackIconKind.Settings,
                    SettingsNavigationItemIndex),
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
