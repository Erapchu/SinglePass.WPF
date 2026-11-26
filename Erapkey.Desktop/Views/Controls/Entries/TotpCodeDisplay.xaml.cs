using Erapkey.Core.Models;
using Erapkey.Core.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Erapkey.Desktop.Views.Controls.Entries
{
    /// <summary>Current TOTP code with a countdown to the next one. Ticks only while it is visible.</summary>
    public partial class TotpCodeDisplay : UserControl
    {
        public static readonly DependencyProperty ConfigurationProperty = DependencyProperty.Register(
            nameof(Configuration), typeof(TotpConfiguration), typeof(TotpCodeDisplay),
            new PropertyMetadata(null, (sender, _) => ((TotpCodeDisplay)sender).Refresh()));

        private static readonly DependencyPropertyKey HasCodePropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(HasCode), typeof(bool), typeof(TotpCodeDisplay), new PropertyMetadata(false));

        public static readonly DependencyProperty HasCodeProperty = HasCodePropertyKey.DependencyProperty;

        public TotpConfiguration Configuration
        {
            get => (TotpConfiguration)GetValue(ConfigurationProperty);
            set => SetValue(ConfigurationProperty, value);
        }

        /// <summary>True while a valid code is shown.</summary>
        public bool HasCode => (bool)GetValue(HasCodeProperty);

        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

        public TotpCodeDisplay()
        {
            _timer.Tick += (_, _) => Refresh();
            InitializeComponent();
            Loaded += (_, _) => Refresh();
            Unloaded += (_, _) =>
            {
                _timer.Stop();
                CodeText.Text = string.Empty;
                ProgressArc.Data = null;
                SetValue(HasCodePropertyKey, false);
            };
            IsVisibleChanged += (_, _) => Refresh();
        }

        private void Refresh()
        {
            if (CodeText == null)
                return;

            var configuration = IsLoaded && IsVisible ? Configuration : null;
            var now = DateTime.UtcNow;
            var valid = TotpService.TryGetCode(configuration, now, out var code, out var seconds);
            CodeText.Text = valid ? code.Insert(code.Length / 2, " ") : string.Empty;
            Countdown.Visibility = valid ? Visibility.Visible : Visibility.Collapsed;
            ProgressArc.SetResourceReference(Shape.StrokeProperty,
                valid && seconds <= 5 ? "SystemFillColorCriticalBrush" : "AccentFillColorDefaultBrush");
            ProgressArc.Data = valid
                ? CreateProgressArc((seconds - now.Millisecond / 1000.0) / configuration.PeriodSeconds)
                : null;
            SetValue(HasCodePropertyKey, valid);
            _timer.IsEnabled = valid;
        }

        private static Geometry CreateProgressArc(double fraction)
        {
            const double center = 8;
            const double radius = 7;
            if (fraction >= 1)
                return new EllipseGeometry(new Point(center, center), radius, radius);

            var angle = (1 - fraction) * 2 * Math.PI;
            var start = new Point(center + radius * Math.Sin(angle), center - radius * Math.Cos(angle));
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(start, isFilled: false, isClosed: false);
                context.ArcTo(new Point(center, center - radius), new Size(radius, radius), 0, fraction > 0.5,
                    SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
            }
            geometry.Freeze();
            return geometry;
        }
    }
}
