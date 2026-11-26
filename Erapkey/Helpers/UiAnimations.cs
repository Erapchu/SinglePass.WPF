using System.Windows;

namespace Erapkey.Helpers
{
    public static class UiAnimations
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(UiAnimations),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits));

        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

        internal static bool ShouldAnimate(DependencyObject element) => GetIsEnabled(element) && SystemParameters.ClientAreaAnimation;
    }
}
