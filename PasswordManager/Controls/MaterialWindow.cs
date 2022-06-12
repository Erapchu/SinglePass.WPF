using PasswordManager.Helpers;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Shell;

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
                _maximizeRestoreButton.MouseEnter -= MaximizeRestoreButton_MouseEnter;
                _maximizeRestoreButton.MouseLeave -= MaximizeRestoreButton_MouseLeave;
            }

            _maximizeRestoreButton = GetTemplateChild(MaximizeRestoreButtonName) as Button;

            if (_maximizeRestoreButton != null)
            {
                _maximizeRestoreButton.Click += MaximizeRestoreButtonClickHandler;
                _maximizeRestoreButton.MouseEnter += MaximizeRestoreButton_MouseEnter;
                _maximizeRestoreButton.MouseLeave += MaximizeRestoreButton_MouseLeave;
            }

            if (_closeButton != null)
                _closeButton.Click -= CloseButtonClickHandler;

            _closeButton = GetTemplateChild(CloseButtonName) as Button;

            if (_closeButton != null)
                _closeButton.Click += CloseButtonClickHandler;

            base.OnApplyTemplate();
        }

        private void MaximizeRestoreButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _showSnap = false;
        }

        private void MaximizeRestoreButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _showSnap = true;
        }

        private bool _showSnap = false;

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
                        // Get the point in screen coordinates.
                        // GET_X_LPARAM and GET_Y_LPARAM are defined in windowsx.h
                        //WinApiProvider.GetCursorPos(out WinApiProvider.POINT point);
                        //// Map the point to client coordinates.
                        //var rect = new WinApiProvider.RECT() { left = point.X, top = point.Y };
                        //WinApiProvider.MapWindowPoints(IntPtr.Zero, Handle, ref rect, 1);
                        // If the point is in your maximize button then return HTMAXBUTTON
                        //if (_maximizeRestoreButton.IsMouseOver)
                        //{
                        //    handled = true;
                        //    return new IntPtr(WinApiProvider.HTMAXBUTTON);
                        //}

                        //int x = lParam.ToInt32() & 0xffff;
                        //int y = lParam.ToInt32() >> 16;
                        //var rect = GetButtonRectangle(_maximizeRestoreButton);
                        //if (rect.Contains(new Point(x, y)))
                        //{
                        //    _maximizeRestoreButton.CaptureMouse();
                        //    handled = true;
                        //    return new IntPtr(WinApiProvider.HTMAXBUTTON);
                        //}
                        //else
                        //{
                        //    _maximizeRestoreButton.ReleaseMouseCapture();
                        //}

                        if (_showSnap)
                        {
                            handled = true;
                            return new IntPtr(WinApiProvider.HTMAXBUTTON);
                        }

                        break;
                    }
                //case WinApiProvider.WM_NCLBUTTONUP:
                //    {
                //        int x = lParam.ToInt32() & 0xffff;
                //        int y = lParam.ToInt32() >> 16;
                //        var rect = GetButtonRectangle(_maximizeRestoreButton);
                //        if (rect.Contains(new Point(x, y)))
                //        {
                //            handled = true;
                //            IInvokeProvider invokeProv = new ButtonAutomationPeer(_maximizeRestoreButton).GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                //            invokeProv?.Invoke();
                //        }
                //        break;
                //    }
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
