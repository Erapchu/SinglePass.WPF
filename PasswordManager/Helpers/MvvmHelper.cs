﻿using System.ComponentModel;
using System.Windows;

namespace PasswordManager.Helpers
{
    internal static class MvvmHelper
    {
        public static bool IsInDesignMode => DesignerProperties.GetIsInDesignMode(new DependencyObject());
    }
}
