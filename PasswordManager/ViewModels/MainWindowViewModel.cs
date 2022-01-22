using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using NLog;
using PasswordManager.Collections;
using PasswordManager.Services;
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

        private readonly SettingsService _settingsService;
        private readonly ILogger _logger;

        public PasswordsViewModel PasswordsViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }
        public CredentialsDialogViewModel ActiveCredentialDialogViewModel { get; }

        public ObservableCollectionDelayed<NavigationItemViewModel> NavigationItems { get; }

        private int _selectedNavigationItemIndex;
        public int SelectedNavigationItemIndex
        {
            get => _selectedNavigationItemIndex;
            set
            {
                SetProperty(ref _selectedNavigationItemIndex, value);
                IsOpenFlyout = false; // Close flyout on change navigation item
            }
        }

        private bool _isOpenFlyout;
        public bool IsOpenFlyout
        {
            get => _isOpenFlyout;
            set => SetProperty(ref _isOpenFlyout, value);
        }

        private MainWindowViewModel() { }

        public MainWindowViewModel(
            PasswordsViewModel passwordsViewModel,
            SettingsService settingsService,
            SettingsViewModel settingsViewModel,
            CredentialsDialogViewModel credentialsDialogViewModel,
            ILogger logger)
        {
            PasswordsViewModel = passwordsViewModel;
            SettingsViewModel = settingsViewModel;
            ActiveCredentialDialogViewModel = credentialsDialogViewModel;
            _settingsService = settingsService;
            _logger = logger;

            PasswordsViewModel.FlyoutRequested += ViewModel_FlyoutRequested;
            ActiveCredentialDialogViewModel.FlyoutRequested += ViewModel_FlyoutRequested;

            NavigationItems = new ObservableCollectionDelayed<NavigationItemViewModel>(new List<NavigationItemViewModel>()
            {
                PasswordsViewModel,
                SettingsViewModel
            });
        }

        private void ViewModel_FlyoutRequested(CredentialViewModel credDialogViewModel, CredentialsDialogMode mode, bool isOpen)
        {
            if (credDialogViewModel is not null)
            {
                ActiveCredentialDialogViewModel.CredentialViewModel = credDialogViewModel;
                ActiveCredentialDialogViewModel.Mode = mode;
            }

            IsOpenFlyout = isOpen;
        }

        private async Task LoadingAsync()
        {
            await PasswordsViewModel.LoadCredentialsAsync();
        }

        private AsyncRelayCommand _loadingCommand;
        public AsyncRelayCommand LoadingCommand => _loadingCommand ??= new AsyncRelayCommand(LoadingAsync);
    }
}
