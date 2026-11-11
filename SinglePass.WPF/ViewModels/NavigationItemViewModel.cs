using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows.Controls;

namespace SinglePass.WPF.ViewModels
{
    public class NavigationItemViewModel : ObservableRecipient
    {
        private readonly Lazy<Control> _lazyContent;

        /// <summary>
        /// Name of settings item.
        /// </summary>
        public string Name { get; }

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

        public NavigationItemViewModel(string name, string glyph, Func<Control> createContent)
        {
            Name = name;
            Glyph = glyph;
            _lazyContent = new Lazy<Control>(createContent);
        }
    }
}
