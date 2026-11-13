using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SinglePass.Language.Properties;
using SinglePass.WPF.Hotkeys;
using SinglePass.WPF.Localization;
using SinglePass.WPF.Services;
using SinglePass.WPF.Settings;
using SinglePass.WPF.ViewModels.Dialogs;
using SinglePass.WPF.Views.Helpers;
using SinglePass.WPF.Views.Windows;
using System;
using System.Windows;

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
        private readonly HotkeysService _hotkeysService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ChangeHelperPopupHotkeyCommand))]
        public partial Hotkey ShowPopupHotkey { get; set; }

        [ObservableProperty]
        public partial AppTheme ThemeMode { get; set; }

        [ObservableProperty]
        public partial string Language { get; set; } = "system";

        private SettingsViewModel() { }

        public SettingsViewModel(
            ThemeService themeService,
            AppSettingsService appSettingsService,
            CredentialsCryptoService credentialsCryptoService,
            HotkeysService hotkeysService)
        {
            ThemeMode = appSettingsService.ThemeMode;
            Language = LocalizationManager.Normalize(appSettingsService.Language);
            ShowPopupHotkey = appSettingsService.ShowPopupHotkey;

            _themeService = themeService;
            _appSettingsService = appSettingsService;
            _credentialsCryptoService = credentialsCryptoService;
            _hotkeysService = hotkeysService;
        }

        [RelayCommand]
        private void ChangePassword()
        {
            var dialog = new ChangePasswordDialogWindow(_credentialsCryptoService)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                MessageDialog.ShowDialog(
                    Resources.Success,
                    LocalizationManager.Instance["NewPasswordApplied"],
                    MessageDialogButtons.OK,
                    "\uE73E");
            }
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

        partial void OnLanguageChanged(string value) => LocalizationManager.Instance.Apply(value);

        partial void OnThemeModeChanged(AppTheme value) => _themeService?.Apply(value);
    }
}
