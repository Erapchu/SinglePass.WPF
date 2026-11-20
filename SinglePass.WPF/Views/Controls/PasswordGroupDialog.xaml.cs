using SinglePass.WPF.Localization;
using SinglePass.WPF.Models;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SinglePass.WPF.Views.Controls
{
    /// <summary>
    /// Adds or edits a password group. Shown by <see cref="ModalHost"/>.
    /// </summary>
    public partial class PasswordGroupDialog : UserControl, IModalDialog
    {
        private sealed record GroupIcon(string Glyph, string Name);
        private readonly PasswordGroup _existingGroup;

        /// <summary>The saved group; <c>null</c> if the dialog was cancelled.</summary>
        public PasswordGroup Group { get; private set; }

        public event Action CloseRequested;

        FrameworkElement IModalDialog.Footer => Footer;

        public IInputElement InitialFocus => GroupNameTextBox;

        public PasswordGroupDialog() : this(null) { }

        public PasswordGroupDialog(PasswordGroup group)
        {
            InitializeComponent();

            _existingGroup = group;
            var loc = LocalizationManager.Instance;
            GroupIcon[] icons =
            {
                new("", loc["GroupIconFolder"]),
                new("", loc["GroupIconWork"]),
                new("", loc["GroupIconHome"]),
                new("", loc["GroupIconFavorites"]),
                new("", loc["GroupIconServers"]),
                new("", loc["GroupIconFinance"]),
                new("", loc["GroupIconShopping"]),
                new("", loc["GroupIconTravel"]),
                new("", loc["GroupIconInternet"]),
                new("", loc["GroupIconBooks"]),
                new("", loc["GroupIconGames"]),
                new("", loc["GroupIconMusic"]),
                new("", loc["GroupIconMessages"]),
                new("", loc["GroupIconContacts"]),
                new("", loc["GroupIconIdeas"]),
                new("", loc["GroupIconEducation"]),
                new("", loc["GroupIconHealth"]),
                new("", loc["GroupIconPhotos"])
            };
            IconList.ItemsSource = icons;
            var iconIndex = Array.FindIndex(icons, icon => icon.Glyph == group?.Icon);
            IconList.SelectedIndex = Math.Max(0, iconIndex);

            HeaderText.Text = loc[group == null ? "AddPasswordGroup" : "EditPasswordGroup"];
            if (group != null)
            {
                GroupNameTextBox.Text = group.Name;
                GroupNameTextBox.SelectAll();
            }
        }

        public void Cancel() => CloseRequested?.Invoke();

        private void GroupName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SaveGroupButton != null)
                SaveGroupButton.IsEnabled = !string.IsNullOrWhiteSpace(GroupNameTextBox.Text);
        }

        private void SaveGroup_Click(object sender, RoutedEventArgs e)
        {
            var name = GroupNameTextBox.Text.Trim();
            if (name.Length == 0 || IconList.SelectedItem is not GroupIcon icon)
                return;

            Group = new PasswordGroup(_existingGroup?.Id ?? Guid.NewGuid(), name, icon.Glyph);
            CloseRequested?.Invoke();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Cancel();
    }
}
