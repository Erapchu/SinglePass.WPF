using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace SinglePass.WPF.Controls
{
    public class MaterialWindow : Window
    {
        private const string MinimizeButtonName = "MinimizeButton";
        private const string MaximizeRestoreButtonName = "MaximizeRestoreButton";
        private const string CloseButtonName = "CloseButton";

        private Button _minimizeButton;
        private Button _maximizeRestoreButton;
        private Button _closeButton;

        public static readonly DependencyProperty CaptionVisibilityProperty = DependencyProperty.Register(
            "CaptionVisibility",
            typeof(Visibility),
            typeof(MaterialWindow),
            new FrameworkPropertyMetadata(Visibility.Visible));

        public Visibility CaptionVisibility
        {
            get => (Visibility)GetValue(CaptionVisibilityProperty);
            set => SetValue(CaptionVisibilityProperty, value);
        }

        private IntPtr _handle;
        public IntPtr Handle
        {
            get
            {
                if (_handle == IntPtr.Zero)
                {
                    _handle = new WindowInteropHelper(this).EnsureHandle();
                }

                return _handle;
            }
        }

        static MaterialWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MaterialWindow), new FrameworkPropertyMetadata(typeof(MaterialWindow)));
        }

        public MaterialWindow() : base()
        {

        }

        public override void OnApplyTemplate()
        {
            if (_minimizeButton != null)
                _minimizeButton.Click -= MinimizeButtonClickHandler;

            _minimizeButton = GetTemplateChild(MinimizeButtonName) as Button;

            if (_minimizeButton != null)
                _minimizeButton.Click += MinimizeButtonClickHandler;

            if (_maximizeRestoreButton != null)
                _maximizeRestoreButton.Click -= MaximizeRestoreButtonClickHandler;

            _maximizeRestoreButton = GetTemplateChild(MaximizeRestoreButtonName) as Button;

            if (_maximizeRestoreButton != null)
                _maximizeRestoreButton.Click += MaximizeRestoreButtonClickHandler;

            if (_closeButton != null)
                _closeButton.Click -= CloseButtonClickHandler;

            _closeButton = GetTemplateChild(CloseButtonName) as Button;

            if (_closeButton != null)
                _closeButton.Click += CloseButtonClickHandler;

            base.OnApplyTemplate();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            var themeService = (System.Windows.Application.Current as App).Services.GetService<ThemeService>();
            var darkModeEnabled = themeService.ThemeMode == BaseTheme.Dark;
            DarkTitleBarHelper.UseImmersiveDarkMode(Handle, darkModeEnabled);
        }

        private void CloseButtonClickHandler(object sender, RoutedEventArgs args)
        {
            Close();
        }

        private void MaximizeRestoreButtonClickHandler(object sender, RoutedEventArgs args)
        {
            WindowState = (WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
        }

        private void MinimizeButtonClickHandler(object sender, RoutedEventArgs args)
        {
            WindowState = WindowState.Minimized;
        }
    }
}
