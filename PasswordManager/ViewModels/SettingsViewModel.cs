using MaterialDesignThemes.Wpf;
using PasswordManager.Services;
using System;

namespace PasswordManager.ViewModels
{
    public class SettingsViewModel : NavigationItemViewModel
    {
        #region Design time instance
        private static readonly Lazy<SettingsViewModel> _lazy = new(GetDesignTimeVM);
        public static SettingsViewModel DesignTimeInstance => _lazy.Value;

        private static SettingsViewModel GetDesignTimeVM()
        {
            var vm = new SettingsViewModel(null);
            return vm;
        }
        #endregion

        private readonly ThemeService _themeService;

        public bool IsDarkMode
        {
            get => _themeService.IsDarkMode;
            set
            {
                if (_themeService.IsDarkMode == value)
                    return;

                _themeService.IsDarkMode = value;
                OnPropertyChanged();
            }
        }

        public SettingsViewModel(ThemeService themeService)
        {
            _themeService = themeService;

            Name = "Settings";
            ItemIndex = SettingsNavigationItemIndex;
            IconKind = PackIconKind.Settings;
        }
    }
}
