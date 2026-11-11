using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SinglePass.WPF.Hotkeys;
using SinglePass.WPF.Services;
using SinglePass.WPF.Settings;
using SinglePass.WPF.ViewModels.Dialogs;
using SinglePass.WPF.Views.Helpers;
using System;
using System.Threading.Tasks;

namespace SinglePass.WPF.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
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

        public event Action NewPasswordIsSet;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
        public partial string NewPassword { get; set; }

        [ObservableProperty]
        public partial string NewPasswordHelperText { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ChangeHelperPopupHotkeyCommand))]
        public partial Hotkey ShowPopupHotkey { get; set; }

        [ObservableProperty]
        public partial AppTheme ThemeMode { get; set; }

        private SettingsViewModel() { }

        public SettingsViewModel(
            ThemeService themeService,
            AppSettingsService appSettingsService,
            CredentialsCryptoService credentialsCryptoService,
            ILogger<SettingsViewModel> logger,
            HotkeysService hotkeysService)
        {
            ThemeMode = appSettingsService.ThemeMode;
            ShowPopupHotkey = appSettingsService.ShowPopupHotkey;

            _themeService = themeService;
            _appSettingsService = appSettingsService;
            _credentialsCryptoService = credentialsCryptoService;
            _logger = logger;
            _hotkeysService = hotkeysService;
        }

        [RelayCommand(CanExecute = nameof(CanChangePassword))]
        private async Task ChangePasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 8)
            {
                NewPasswordHelperText = "Minimum symbols count is 8";
                return;
            }

            NewPasswordHelperText = string.Empty;
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
                MessageDialog.ShowDialog(
                    "Success",
                    "New password applied",
                    MessageDialogButtons.OK,
                    "\uE73E");
            }
        }

        private bool CanChangePassword()
        {
            return !string.IsNullOrWhiteSpace(NewPassword);
        }

        [RelayCommand]
        private void ChangeHelperPopupHotkey(System.Windows.Input.KeyEventArgs args)
        {
            if (_hotkeysService.GetHotkeyForKeyPress(args, out Hotkey hotkey))
            {
                _hotkeysService.UpdateKey(hotkey, nameof(_appSettingsService.ShowPopupHotkey), HotkeyDelegates.PopupHotkeyHandler);
                ShowPopupHotkey = hotkey;
            }
        }

        [RelayCommand]
        private void ClearShowPopupHotkey()
        {
            ShowPopupHotkey = Hotkey.Empty;
        }

        partial void OnThemeModeChanged(AppTheme value) => _themeService?.Apply(value);
    }
}
