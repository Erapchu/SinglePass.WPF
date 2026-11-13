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

        public PasswordGroup Group { get; private set; }

        public PasswordGroupDialogWindow()
        {
            InitializeComponent();

            var loc = LocalizationManager.Instance;
            IconList.ItemsSource = new GroupIcon[]
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
            IconList.SelectedIndex = 0;
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

            Group = new PasswordGroup(Guid.NewGuid(), name, icon.Glyph);
            DialogResult = true;
        }
    }
}
