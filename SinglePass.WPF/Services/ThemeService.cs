using SinglePass.WPF.Settings;
using System.Windows;

namespace SinglePass.WPF.Services
{
    public class ThemeService
    {
        public void Apply(AppTheme theme) => ApplyTheme(theme);

#pragma warning disable WPF0001 // WPF exposes Fluent theme switching through this API.
        internal static void ApplyTheme(AppTheme theme)
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
