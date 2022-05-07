using MaterialDesignThemes.Wpf;
using PasswordManager.Settings;
using System;
using System.Linq;
using System.Windows;

namespace PasswordManager.Services
{
    public class ThemeService
    {
        private const string _defaultsSource = "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Defaults.xaml";

        private readonly AppSettingsService _appSettingsService;
        private BundledTheme _bundledThemeDictionary;

        public BaseTheme ThemeMode
        {
            get => GetTheme().GetBaseTheme();
            set
            {
                ITheme theme = GetTheme();
                // Here we are going to compare Themes, not Enums
                if (theme.GetBaseTheme().GetBaseTheme() == value.GetBaseTheme())
                    return;

                IBaseTheme newBaseTheme;
                if (value == BaseTheme.Inherit)
                {
                    newBaseTheme = Theme.GetSystemTheme()?.GetBaseTheme() ?? Theme.Light;
                }
                else
                {
                    newBaseTheme = value.GetBaseTheme();
                }
                theme.SetBaseTheme(newBaseTheme);
                _bundledThemeDictionary.SetTheme(theme);
            }
        }

        public ITheme GetTheme() => _bundledThemeDictionary.GetTheme();

        public ThemeService(AppSettingsService appSettingsService)
        {
            _appSettingsService = appSettingsService;
        }

        public void Init()
        {
            var app = Application.Current;
            if (app is null)
                return;

            var appMergedDictionaries = app.Resources.MergedDictionaries;

            var bundledThemeDictionary = (BundledTheme)appMergedDictionaries.FirstOrDefault(rd => rd is BundledTheme);
            if (bundledThemeDictionary is null)
            {
                bundledThemeDictionary = new BundledTheme();
                appMergedDictionaries.Add(bundledThemeDictionary);
            }

            var materialDefaultsUri = new Uri(_defaultsSource);
            var defaultsDictionary = appMergedDictionaries.FirstOrDefault(rd => rd.Source == materialDefaultsUri);
            if (defaultsDictionary is null)
            {
                defaultsDictionary = new ResourceDictionary()
                {
                    Source = materialDefaultsUri,
                };
                appMergedDictionaries.Add(defaultsDictionary);
            }

            bundledThemeDictionary.ColorAdjustment = new ColorAdjustment();
            bundledThemeDictionary.BaseTheme = _appSettingsService.Settings.ThemeMode;
            bundledThemeDictionary.PrimaryColor = MaterialDesignColors.PrimaryColor.DeepPurple;
            bundledThemeDictionary.SecondaryColor = MaterialDesignColors.SecondaryColor.Lime;

            _bundledThemeDictionary = bundledThemeDictionary;
        }
    }
}
