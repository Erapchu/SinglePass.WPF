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
        private bool _newPasswordSetProcessing;

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

        public bool NewPasswordSetProcessing
        {
            get => _newPasswordSetProcessing;
            set => SetProperty(ref _newPasswordSetProcessing, value);
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
            if (NewPasswordSetProcessing || string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 8)
                return;

            var dialogIdentifier = MvvmHelper.MainWindowDialogName;

            try
            {
                NewPasswordSetProcessing = true;

                var processingDialog = new ProcessingControl("Setting up new password...", "Please wait", dialogIdentifier);
                _ = DialogHost.Show(processingDialog, dialogIdentifier);

                _credentialsCryptoService.SetPassword(NewPassword);
                await _credentialsCryptoService.SaveCredentialsAndSync();

                NewPasswordIsSet?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }
            finally
            {
                if (DialogHost.IsDialogOpen(dialogIdentifier))
                    DialogHost.Close(dialogIdentifier);

                await MaterialMessageBox.ShowAsync(
                    "Success",
                    "New password applied",
                    MaterialMessageBoxButtons.OK,
                    dialogIdentifier,
                    PackIconKind.Tick);

                NewPasswordSetProcessing = false;
            }
        }

        private bool CanChangePassword()
        {
            return !string.IsNullOrWhiteSpace(NewPassword);
        }
    }
}
