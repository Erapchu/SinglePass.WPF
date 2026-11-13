using SinglePass.Language.Properties;
using System.ComponentModel;
using System.Globalization;

namespace SinglePass.WPF.Localization
{
    public sealed class LocalizationManager : INotifyPropertyChanged
    {
        private readonly CultureInfo _systemCulture = CultureInfo.CurrentUICulture;

        public static LocalizationManager Instance { get; } = new();

        public event PropertyChangedEventHandler PropertyChanged;

        public CultureInfo Culture => Resources.Culture ?? _systemCulture;

        public string this[string key] => Resources.ResourceManager.GetString(key, Resources.Culture) ?? key;

        private LocalizationManager() { }

        internal static string Normalize(string language) => language is "en" or "ru" ? language : "system";

        internal void Apply(string language)
        {
            var culture = Normalize(language) switch
            {
                "en" => CultureInfo.GetCultureInfo("en"),
                "ru" => CultureInfo.GetCultureInfo("ru"),
                _ => _systemCulture
            };

            if (Equals(Resources.Culture, culture))
                return;

            Resources.Culture = culture;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
        }
    }
}
