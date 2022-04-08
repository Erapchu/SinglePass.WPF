using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Helpers;
using PasswordManager.Services;
using PasswordManager.Views;
using PasswordManager.Views.MessageBox;
using System;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class SettingsViewModel : NavigationItemViewModel
    {
        #region Design time instance
        private static readonly Lazy<SettingsViewModel> _lazy = new(GetDesignTimeVM);
        public static SettingsViewModel DesignTimeInstance => _lazy.Value;

        private static SettingsViewModel GetDesignTimeVM()
        {
            var vm = new SettingsViewModel();
            return vm;
        }
        #endregion

        private readonly ThemeService _themeService;
        private readonly AppSettingsService _appSettingsService;
        private readonly CredentialsCryptoService _credentialsCryptoService;
        private readonly ILogger<SettingsViewModel> _logger;
        private AsyncRelayCommand _changePasswordCommand;
        private string _newPassword;
        private string _newPasswordHelperText;

        public BaseTheme ThemeMode
        {
            get => _appSettingsService.ThemeMode;
            set
            {
                _appSettingsService.ThemeMode = value;
                _appSettingsService.Save();
                OnPropertyChanged();
                _themeService.ThemeMode = value;
            }
        }

        public string NewPassword
        {
            get => _newPassword;
            set
            {
                SetProperty(ref _newPassword, value);
                ChangePasswordCommand.NotifyCanExecuteChanged();
            }
        }

        public string NewPasswordHelperText
        {
            get => _newPasswordHelperText;
            set => SetProperty(ref _newPasswordHelperText, value);
        }

        public event Action NewPasswordIsSet;

        public AsyncRelayCommand ChangePasswordCommand => _changePasswordCommand ??= new AsyncRelayCommand(ChangePasswordAsync, CanChangePassword);

        private SettingsViewModel() { }

        public SettingsViewModel(
            ThemeService themeService,
            AppSettingsService appSettingsService,
            CredentialsCryptoService credentialsCryptoService,
            ILogger<SettingsViewModel> logger)
        {
            Name = "Settings";
            IconKind = PackIconKind.Settings;

            _themeService = themeService;
            _appSettingsService = appSettingsService;
            _credentialsCryptoService = credentialsCryptoService;
            _logger = logger;
        }

        private async Task ChangePasswordAsync()
        {
            if (Loading || string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 8)
            {
                NewPasswordHelperText = "Minimum symbols count is 8";
                return;
            }

            NewPasswordHelperText = string.Empty;
            var dialogIdentifier = MvvmHelper.MainWindowDialogName;
            var success = false;

            try
            {
                Loading = true;

                _credentialsCryptoService.SetPassword(NewPassword);
                await _credentialsCryptoService.SaveCredentialsAndSync();

                NewPasswordIsSet?.Invoke();
                success = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }
            finally
            {
                Loading = false;
            }

            if (success)
            {
                await MaterialMessageBox.ShowAsync(
                    "Success",
                    "New password applied",
                    MaterialMessageBoxButtons.OK,
                    dialogIdentifier,
                    PackIconKind.Tick);
            }
        }

        private bool CanChangePassword()
        {
            return !string.IsNullOrWhiteSpace(NewPassword);
        }
    }
}
