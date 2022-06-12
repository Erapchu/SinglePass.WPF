using PasswordManager.Helpers;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace PasswordManager.Controls
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
                    _handle = new WindowInteropHelper(this).EnsureHandle();

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

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            source.AddHook(WndProc);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            source.RemoveHook(WndProc);

            base.OnClosing(e);
        }

        public override void OnApplyTemplate()
        {
            if (_minimizeButton != null)
                _minimizeButton.Click -= MinimizeButtonClickHandler;

            _minimizeButton = GetTemplateChild(MinimizeButtonName) as Button;

            if (_minimizeButton != null)
                _minimizeButton.Click += MinimizeButtonClickHandler;

            if (_maximizeRestoreButton != null)
            {
                _maximizeRestoreButton.Click -= MaximizeRestoreButtonClickHandler;
            }

            _maximizeRestoreButton = GetTemplateChild(MaximizeRestoreButtonName) as Button;

            if (_maximizeRestoreButton != null)
            {
                _maximizeRestoreButton.Click += MaximizeRestoreButtonClickHandler;
            }

            if (_closeButton != null)
                _closeButton.Click -= CloseButtonClickHandler;

            _closeButton = GetTemplateChild(CloseButtonName) as Button;

            if (_closeButton != null)
                _closeButton.Click += CloseButtonClickHandler;

            base.OnApplyTemplate();
        }

        private void CloseButtonClickHandler(object sender, RoutedEventArgs args) => Close();

        private void MaximizeRestoreButtonClickHandler(object sender, RoutedEventArgs args) => WindowState = (WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;

        private void MinimizeButtonClickHandler(object sender, RoutedEventArgs args) => WindowState = WindowState.Minimized;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Handle messages...

            switch (msg)
            {
                case WinApiProvider.WM_NCHITTEST:
                    {
                        try
                        {
                            int x = lParam.ToInt32() & 0xffff;
                            int y = lParam.ToInt32() >> 16;
                            var dpiScale = DpiUtilities.GetDpiForWindow(Handle);
                            var rect = GetButtonRectangle(_maximizeRestoreButton);
                            if (rect.Contains(new Point(x, y)))
                            {
                                handled = true;
                                _maximizeRestoreButton.Background = Brushes.AliceBlue;
                            }
                            else
                            {
                                _maximizeRestoreButton.Background = Brushes.AntiqueWhite;
                            }
                            return new IntPtr(WinApiProvider.HTMAXBUTTON);
                        }
                        catch (OverflowException)
                        {
                            handled = true;
                        }

                        break;
                    }
                case WinApiProvider.WM_NCLBUTTONUP:
                    {
                        int x = lParam.ToInt32() & 0xffff;
                        int y = lParam.ToInt32() >> 16;
                        var rect = GetButtonRectangle(_maximizeRestoreButton);
                        if (rect.Contains(new Point(x, y)))
                        {
                            handled = true;
                            IInvokeProvider invokeProv = new ButtonAutomationPeer(_maximizeRestoreButton).GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                            invokeProv?.Invoke();
                        }
                        break;
                    }
            }

            return IntPtr.Zero;
        }

        private Rect GetButtonRectangle(Button button)
        {
            var dpi = DpiUtilities.GetDpiForWindow(Handle);
            var dpiScale = dpi / DpiUtilities.DefaultDpiX;
            var rect = new Rect(button.PointToScreen(
                new Point()),
                new Size(button.Width * dpiScale, button.Height * dpiScale));
            return rect;
        }
    }
}
