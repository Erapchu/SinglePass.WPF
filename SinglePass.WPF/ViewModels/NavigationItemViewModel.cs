using CommunityToolkit.Mvvm.ComponentModel;
using SinglePass.WPF.Localization;
using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace SinglePass.WPF.ViewModels
{
    public class NavigationItemViewModel : ObservableRecipient
    {
        private readonly Lazy<Control> _lazyContent;
        private readonly string _nameKey;

        /// <summary>
        /// Name of settings item.
        /// </summary>
        public string Name => LocalizationManager.Instance[_nameKey];

        /// <summary>
        /// Navigation icon.
        /// </summary>
        public string Glyph { get; }

        /// <summary>
        /// Visible content.
        /// </summary>
        public Control Content
        {
            get
            {
                try
                {
                    return _lazyContent.Value;
                }
                catch
                {
                    return null;
                }
            }
        }

        public NavigationItemViewModel(string nameKey, string glyph, Func<Control> createContent)
        {
            _nameKey = nameKey;
            Glyph = glyph;
            _lazyContent = new Lazy<Control>(createContent);
            PropertyChangedEventManager.AddHandler(LocalizationManager.Instance, OnLanguageChanged, "Item[]");
        }

        private void OnLanguageChanged(object sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(Name));
    }
}
