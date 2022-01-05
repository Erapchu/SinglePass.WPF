using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Services;
using System;
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

        public PasswordsViewModel PasswordsViewModel { get; }

        private MainWindowViewModel() { }

        public MainWindowViewModel(PasswordsViewModel passwordsViewModel, SettingsService settingsService)
        {
            PasswordsViewModel = passwordsViewModel;
            _settingsService = settingsService;
        }

        private async Task LoadingAsync()
        {
            await PasswordsViewModel.LoadCredentialsAsync();
        }

        private AsyncRelayCommand _loadingCommand;
        public AsyncRelayCommand LoadingCommand => _loadingCommand ??= new AsyncRelayCommand(LoadingAsync);
    }
}
