using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace PasswordManager.ViewModels
{
    public class NavigationItemViewModel : ObservableRecipient
    {
        private bool _loading;
        private bool _isVisible;

        /// <summary>
        /// Name of settings item.
        /// </summary>
        public string Name { get; protected set; }

        /// <summary>
        /// Icon kind.
        /// </summary>
        public PackIconKind IconKind { get; protected set; }

        /// <summary>
        /// Indicates loading of some content.
        /// </summary>
        public bool Loading
        {
            get => _loading;
            set => SetProperty(ref _loading, value);
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }
    }
}
