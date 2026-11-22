using SinglePass.WPF.ViewModels;
using System.Windows;
using System.Windows.Media;

namespace SinglePass.WPF.Views.Controls.Entries
{
    /// <summary>Access from an entry view or editor to the details card it is shown in.</summary>
    internal static class EntryHost
    {
        public static CredentialsDetailsControl FindCard(DependencyObject element)
        {
            for (var current = element; current != null;
                 current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current))
            {
                if (current is CredentialsDetailsControl card)
                    return card;
            }
            return null;
        }

        public static PasswordsViewModel FindViewModel(DependencyObject element) =>
            FindCard(element)?.DataContext as PasswordsViewModel;
    }
}
