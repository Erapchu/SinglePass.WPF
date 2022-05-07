using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
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
        private readonly HotkeysService _hotkeysService;
        private string _newPassword;
        private string _newPasswordHelperText;
        private AsyncRelayCommand _changePasswordCommand;
        private RelayCommand<System.Windows.Input.KeyEventArgs> _changeHelperPopupHotkeyCommand;
        private RelayCommand _clearShowPopupHotkeyCommand;

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

        public Hotkey ShowPopupHotkey
        {
            get => _appSettingsService.ShowPopupHotkey;
            set
            {
                _appSettingsService.ShowPopupHotkey = value;
                _appSettingsService.Save();
                OnPropertyChanged();
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
            Name = PasswordManager.Language.Properties.Resources.Settings;
            IconKind = PackIconKind.Settings;

            _themeService = themeService;
            _appSettingsService = appSettingsService;
            _credentialsCryptoService = credentialsCryptoService;
            _logger = logger;
            _hotkeysService = hotkeysService;
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
                await _credentialsCryptoService.SaveCredentials();

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
