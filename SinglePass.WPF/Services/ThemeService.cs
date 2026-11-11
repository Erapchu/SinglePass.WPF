using SinglePass.WPF.Settings;
using System.Windows;

namespace SinglePass.WPF.Services
{
    public class ThemeService
    {
        private readonly AppSettingsService _appSettingsService;

        public ThemeService(AppSettingsService appSettingsService)
        {
            _appSettingsService = appSettingsService;
        }

        public void Init() => Apply(_appSettingsService.ThemeMode);

#pragma warning disable WPF0001 // WPF exposes Fluent theme switching through this API.
        public void Apply(AppTheme theme)
        {
            Application.Current.ThemeMode = theme switch
            {
                AppTheme.Light => ThemeMode.Light,
                AppTheme.Dark => ThemeMode.Dark,
                _ => ThemeMode.System
            };
        }
#pragma warning restore WPF0001
    }
}
