using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Windows.Controls;

namespace PasswordManager.ViewModels
{
    public class NavigationItemViewModel : ObservableRecipient
    {
        private bool _loading;
        private readonly Lazy<Control> _lazyContent;

        /// <summary>
        /// Name of settings item.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Icon kind.
        /// </summary>
        public PackIconKind IconKind { get; }

        /// <summary>
        /// Indicates loading of some content.
        /// </summary>
        public bool Loading
        {
            get => _loading;
            set => SetProperty(ref _loading, value);
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

        public NavigationItemViewModel(string name, PackIconKind icon, Func<Control> createContent)
        {
            Name = name;
            IconKind = icon;
            _lazyContent = new Lazy<Control>(createContent);
        }
    }
}
