using Erapkey.Desktop.Helpers;
using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Erapkey.Desktop.Controls
{
    public class ToggleSwitch : ToggleButton
    {
        private static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(160);
        private static readonly TimeSpan PressDuration = TimeSpan.FromMilliseconds(90);
        private TranslateTransform _thumbOffset;
        private ScaleTransform _thumbScale;
        private UIElement _onTrack;

        static ToggleSwitch()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(typeof(ToggleSwitch)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _thumbOffset = null;
            _thumbScale = null;
            if (GetTemplateChild("PART_Thumb") is FrameworkElement thumb)
            {
                _thumbOffset = new TranslateTransform();
                _thumbScale = new ScaleTransform();
                thumb.RenderTransform = new TransformGroup { Children = { _thumbScale, _thumbOffset } };
            }
            _onTrack = GetTemplateChild("PART_OnTrack") as UIElement;
            UpdatePosition(false);
            UpdateThumbScale(false);
        }

        protected override void OnChecked(RoutedEventArgs e)
        {
            base.OnChecked(e);
            UpdatePosition(ShouldAnimate);
        }

        protected override void OnUnchecked(RoutedEventArgs e)
        {
            base.OnUnchecked(e);
            UpdatePosition(ShouldAnimate);
        }

        protected override void OnIndeterminate(RoutedEventArgs e)
        {
            base.OnIndeterminate(e);
            UpdatePosition(ShouldAnimate);
        }

        private bool ShouldAnimate => IsLoaded && IsVisible && UiAnimations.ShouldAnimate(this);

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == IsPressedProperty || e.Property == IsMouseOverProperty)
                UpdateThumbScale(ShouldAnimate);
            else if (e.Property == UiAnimations.IsEnabledProperty && e.NewValue is false)
            {
                UpdatePosition(false);
                UpdateThumbScale(false);
            }
        }

        private void UpdatePosition(bool animate)
        {
            var isOn = IsChecked == true;
            SetVisualValue(_thumbOffset, TranslateTransform.XProperty, isOn ? 20 : 0, SlideDuration, animate);
            SetVisualValue(_onTrack, OpacityProperty, isOn ? 1 : 0, SlideDuration, animate);
        }

        private void UpdateThumbScale(bool animate)
        {
            var scaleX = IsPressed ? 1.35 : IsMouseOver ? 1.15 : 1;
            var scaleY = IsMouseOver || IsPressed ? 1.15 : 1;
            SetVisualValue(_thumbScale, ScaleTransform.ScaleXProperty, scaleX, PressDuration, animate);
            SetVisualValue(_thumbScale, ScaleTransform.ScaleYProperty, scaleY, PressDuration, animate);
        }

        private static void SetVisualValue(DependencyObject target, DependencyProperty property, double value,
            TimeSpan duration, bool animate)
        {
            if (target is not IAnimatable animatable)
                return;

            var current = (double)target.GetValue(property);
            target.SetValue(property, value);
            var animation = animate && current != value
                ? new DoubleAnimation(current, value, duration)
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                }
                : null;
            animatable.BeginAnimation(property, animation);
        }
    }
}
