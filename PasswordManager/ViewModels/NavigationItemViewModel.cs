using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;

namespace PasswordManager.ViewModels
{
    public class NavigationItemViewModel : ObservableRecipient
    {
        /// <summary>
        /// Name of settings item.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Item index.
        /// </summary>
        public int ItemIndex { get; }

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
            int itemIndex)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            IconKind = packIconKind;
            ItemIndex = itemIndex;
        }
    }
}
