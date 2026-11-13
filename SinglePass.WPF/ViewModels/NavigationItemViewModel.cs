using CommunityToolkit.Mvvm.ComponentModel;
using SinglePass.WPF.Localization;
using SinglePass.WPF.Models;
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
        public string Name => Group?.Name ?? LocalizationManager.Instance[_nameKey];

        /// <summary>
        /// Navigation icon.
        /// </summary>
        public string Glyph => Group?.Icon ?? _glyph;

        private readonly string _glyph;
        public PasswordGroup Group { get; private set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            internal set => SetProperty(ref _isSelected, value);
        }

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
            _glyph = glyph;
            _lazyContent = new Lazy<Control>(createContent);
            PropertyChangedEventManager.AddHandler(LocalizationManager.Instance, OnLanguageChanged, "Item[]");
        }

        public NavigationItemViewModel(PasswordGroup group, NavigationItemViewModel passwords)
        {
            Group = group;
            _lazyContent = passwords._lazyContent;
        }

        internal void UpdateGroup(PasswordGroup group)
        {
            Group = group;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Glyph));
        }

        private void OnLanguageChanged(object sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(Name));
    }
}
