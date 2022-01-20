using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace PasswordManager.ViewModels
{
    public class NavigationItemViewModel : ObservableRecipient
    {
        public static int PasswordsNavigationItemIndex { get; }
        public static int SettingsNavigationItemIndex { get; } = 1;

        /// <summary>
        /// Name of settings item.
        /// </summary>
        public string Name { get; protected set; }

        /// <summary>
        /// Item index.
        /// </summary>
        public int ItemIndex { get; protected set; }

        /// <summary>
        /// Icon kind.
        /// </summary>
        public PackIconKind IconKind { get; protected set; }

        private bool _loading;
        /// <summary>
        /// Indicates loading of some content.
        /// </summary>
        public bool Loading
        {
            get => _loading;
            set => SetProperty(ref _loading, value);
        }
    }
}
