using SinglePass.WPF.Localization;
using SinglePass.WPF.Models;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SinglePass.WPF.Views.Windows
{
    public partial class PasswordGroupDialogWindow : Window
    {
        private sealed record GroupIcon(string Glyph, string Name);
        private readonly PasswordGroup _existingGroup;

        public PasswordGroup Group { get; private set; }

        public PasswordGroupDialogWindow() : this(null) { }

        public PasswordGroupDialogWindow(PasswordGroup group)
        {
            InitializeComponent();

            _existingGroup = group;
            var loc = LocalizationManager.Instance;
            GroupIcon[] icons =
            {
                new("\uE8B7", loc["GroupIconFolder"]),
                new("\uE821", loc["GroupIconWork"]),
                new("\uE80F", loc["GroupIconHome"]),
                new("\uE734", loc["GroupIconFavorites"]),
                new("\uE968", loc["GroupIconServers"]),
                new("\uE8C7", loc["GroupIconFinance"]),
                new("\uE7BF", loc["GroupIconShopping"]),
                new("\uE806", loc["GroupIconTravel"])
            };
            IconList.ItemsSource = icons;
            var iconIndex = Array.FindIndex(icons, icon => icon.Glyph == group?.Icon);
            IconList.SelectedIndex = Math.Max(0, iconIndex);

            if (group != null)
            {
                Title = loc["EditPasswordGroup"];
                GroupNameTextBox.Text = group.Name;
            }
        }

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
            DialogResult = true;
        }
    }
}
