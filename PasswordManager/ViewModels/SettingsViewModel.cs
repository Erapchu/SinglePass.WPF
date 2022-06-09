using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Helpers;
using PasswordManager.Hotkeys;
using PasswordManager.Services;
using PasswordManager.Settings;
using PasswordManager.Views.MessageBox;
using System;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class SettingsViewModel : ObservableRecipient
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
        private readonly HotkeysService _hotkeysService;
        private string _newPassword;
        private string _newPasswordHelperText;
        private AsyncRelayCommand _changePasswordCommand;
        private RelayCommand<System.Windows.Input.KeyEventArgs> _changeHelperPopupHotkeyCommand;
        private RelayCommand _clearShowPopupHotkeyCommand;

        private BaseTheme _themeMode;
        public BaseTheme ThemeMode
        {
            get => _themeMode;
            set
            {
                SetProperty(ref _themeMode, value);
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

        private Hotkey _showPopupHotkey;
        public Hotkey ShowPopupHotkey
        {
            get => _showPopupHotkey;
            set
            {
                SetProperty(ref _showPopupHotkey, value);
                ChangeHelperPopupHotkeyCommand.NotifyCanExecuteChanged();
            }
        }

        public event Action NewPasswordIsSet;

        public AsyncRelayCommand ChangePasswordCommand => _changePasswordCommand ??= new AsyncRelayCommand(ChangePasswordAsync, CanChangePassword);
        public RelayCommand<System.Windows.Input.KeyEventArgs> ChangeHelperPopupHotkeyCommand => _changeHelperPopupHotkeyCommand ??= new RelayCommand<System.Windows.Input.KeyEventArgs>(ChangeHelperPopupHotkey);
        public RelayCommand ClearShowPopupHotkeyCommand => _clearShowPopupHotkeyCommand ??= new RelayCommand(ClearShowPopupHotkey);

        private SettingsViewModel() { }

        public SettingsViewModel(
            ThemeService themeService,
            AppSettingsService appSettingsService,
            CredentialsCryptoService credentialsCryptoService,
            ILogger<SettingsViewModel> logger,
            HotkeysService hotkeysService)
        {
            _themeService = themeService;
            _appSettingsService = appSettingsService;
            _credentialsCryptoService = credentialsCryptoService;
            _logger = logger;
            _hotkeysService = hotkeysService;

            _themeMode = _appSettingsService.ThemeMode;
            _showPopupHotkey = _appSettingsService.ShowPopupHotkey;
        }

        private async Task ChangePasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 8)
            {
                NewPasswordHelperText = "Minimum symbols count is 8";
                return;
            }

            NewPasswordHelperText = string.Empty;
            var dialogIdentifier = MvvmHelper.MainWindowDialogName;
            var success = false;

            try
            {
                _credentialsCryptoService.SetPassword(NewPassword);
                await _credentialsCryptoService.SaveCredentials();

                NewPasswordIsSet?.Invoke();
                success = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
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

        private void ChangeHelperPopupHotkey(System.Windows.Input.KeyEventArgs args)
        {
            if (_hotkeysService.GetHotkeyForKeyPress(args, out Hotkey hotkey))
            {
                _hotkeysService.UpdateKey(hotkey, nameof(_appSettingsService.ShowPopupHotkey), HotkeyDelegates.PopupHotkeyHandler);
                ShowPopupHotkey = hotkey;
            }
        }

        private void ClearShowPopupHotkey()
        {
            ShowPopupHotkey = Hotkey.Empty;
        }

        private bool CanChangePassword()
        {
            return !string.IsNullOrWhiteSpace(NewPassword);
        }
    }
}
