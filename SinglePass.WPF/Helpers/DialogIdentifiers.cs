using System.ComponentModel;
using System.Windows;

namespace SinglePass.WPF.Helpers
{
    internal static class DialogIdentifiers
    {
        public static string MainWindowName { get; } = "MainWindowDialogHost";

        public static bool IsInDesignMode => DesignerProperties.GetIsInDesignMode(new DependencyObject());
    }
}
