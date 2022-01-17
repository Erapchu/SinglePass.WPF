using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using NLog;
using System;
using System.Windows.Controls;

namespace PasswordManager.ViewModels
{
    public class NavigationItemViewModel : ObservableRecipient
    {
        private readonly ILogger _logger;
        private readonly Lazy<Control> _lazyContent;

        /// <summary>
        /// Name of settings item.
        /// </summary>
        public string Name { get; }

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
                catch (Exception ex)
                {
                    _logger.Error(ex);
                    return null;
                }
            }
        }

        /// <summary>
        /// Icon kind.
        /// </summary>
        public PackIconKind IconKind { get; }

        /// <summary>
        /// Constructs settings item view model with specified name, content and icon.
        /// </summary>
        /// <param name="name">Name of settings item.</param>
        /// <param name="createContent">Function to create content.</param>
        /// <param name="packIconKind">Icon kind on the left side.</param>
        public NavigationItemViewModel(
            string name,
            PackIconKind packIconKind,
            Func<Control> createContent,
            ILogger logger)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _lazyContent = new Lazy<Control>(createContent);
            IconKind = packIconKind;
            _logger = logger;
        }
    }
}
