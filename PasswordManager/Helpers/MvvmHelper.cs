﻿using System.ComponentModel;
using System.Windows;

namespace SinglePass.WPF.Helpers
{
    internal static class MvvmHelper
    {
        public static string MainWindowDialogName { get; } = "MainWindowDialogHost";

        public static bool IsInDesignMode => DesignerProperties.GetIsInDesignMode(new DependencyObject());
    }
}
