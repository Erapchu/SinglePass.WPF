using MaterialDesignThemes.Wpf;
using System.Linq;

namespace PasswordManager.Services
{
    public class ThemeService
    {
        private readonly CredentialsCryptoService _credentialsCryptoService;
        private readonly BundledTheme _bundledThemeDictionary;

        public bool IsDarkMode
        {
            get => GetTheme().GetBaseTheme() == BaseTheme.Dark;
            set
            {
                ITheme theme = GetTheme();

                var currentBaseTheme = theme.GetBaseTheme();
                if (currentBaseTheme == BaseTheme.Dark && value
                    || currentBaseTheme == BaseTheme.Light && !value)
                    return;

                IBaseTheme newBaseTheme = value ? Theme.Dark : Theme.Light;
                theme.SetBaseTheme(newBaseTheme);
                _bundledThemeDictionary.SetTheme(theme);
            }
        }

        public ITheme GetTheme() => _bundledThemeDictionary.GetTheme();

        public ThemeService(CredentialsCryptoService credentialsCryptoService)
        {
            _credentialsCryptoService = credentialsCryptoService;

            var app = System.Windows.Application.Current;
            if (app is null)
                return;

            var appMergedDictionaries = app.Resources.MergedDictionaries;

            var bundledThemeDictionary = (BundledTheme)appMergedDictionaries.FirstOrDefault(rd => rd is BundledTheme);
            if (bundledThemeDictionary is null)
            {
                bundledThemeDictionary = new BundledTheme();
                appMergedDictionaries.Add(bundledThemeDictionary);
            }

            bundledThemeDictionary.ColorAdjustment = new ColorAdjustment();
            bundledThemeDictionary.BaseTheme = BaseTheme.Light;
            bundledThemeDictionary.PrimaryColor = MaterialDesignColors.PrimaryColor.DeepPurple;
            bundledThemeDictionary.SecondaryColor = MaterialDesignColors.SecondaryColor.Lime;

            _bundledThemeDictionary = bundledThemeDictionary;
        }
    }
}
