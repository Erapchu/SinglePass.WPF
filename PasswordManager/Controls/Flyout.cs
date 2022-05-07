using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PasswordManager.Controls
{
    public class Flyout : ContentControl
    {
        public static readonly DependencyProperty AnimateOnPositionChangeProperty = DependencyProperty.Register(
            nameof(AnimateOnPositionChange),
            typeof(bool),
            typeof(Flyout),
            new PropertyMetadata(true));

        public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
            nameof(Position),
            typeof(Position),
            typeof(Flyout),
            new PropertyMetadata(Position.Left, OnPositionPropertyChanged));

        public static readonly DependencyProperty AnimateOpacityProperty = DependencyProperty.Register(
            nameof(AnimateOpacity),
            typeof(bool),
            typeof(Flyout),
            new FrameworkPropertyMetadata(false, UpdateOpacityChange));

        public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
            nameof(IsOpen),
            typeof(bool),
            typeof(Flyout),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenPropertyChanged));

        public static readonly RoutedEvent IsOpenChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(IsOpenChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(Flyout));

        public static readonly DependencyProperty AreAnimationsEnabledProperty = DependencyProperty.Register(
            nameof(AreAnimationsEnabled),
            typeof(bool),
            typeof(Flyout),
            new PropertyMetadata(true));

        public static readonly DependencyProperty ShadowVisibilityProperty = DependencyProperty.Register(
            nameof(ShadowVisibility),
            typeof(Visibility),
            typeof(Flyout),
            new FrameworkPropertyMetadata(Visibility.Visible, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        private Storyboard _showStoryboard;
        private Storyboard _hideStoryboard;
        private SplineDoubleKeyFrame _hideFrame;
        private SplineDoubleKeyFrame _hideFrameY;
        private SplineDoubleKeyFrame _showFrame;
        private SplineDoubleKeyFrame _showFrameY;
        private SplineDoubleKeyFrame _fadeOutFrame;
        private FrameworkElement _flyoutRoot;
        private FrameworkElement _flyoutContent;

        /// <summary>
        /// Gets or sets the position of this <see cref="Flyout"/> inside the <see cref="FlyoutsControl"/>.
        /// </summary>
        public Position Position
        {
            get => (Position)GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }

        /// <summary>
        /// Gets or sets whether this <see cref="Flyout"/> animates the opacity when opening/closing the <see cref="Flyout"/>.
        /// </summary>
        public bool AnimateOpacity
        {
            get => (bool)GetValue(AnimateOpacityProperty);
            set => SetValue(AnimateOpacityProperty, value);
        }

        /// <summary>
        /// Gets or sets whether this <see cref="Flyout"/> should be visible or not.
        /// </summary>
        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        /// <summary>
        /// Gets or sets whether this <see cref="Flyout"/> uses the open/close animation when changing the <see cref="Position"/> property (default is true).
        /// </summary>
        public bool AnimateOnPositionChange
        {
            get => (bool)GetValue(AnimateOnPositionChangeProperty);
            set => SetValue(AnimateOnPositionChangeProperty, value);
        }

        public Visibility ShadowVisibility
        {
            get => (Visibility)GetValue(ShadowVisibilityProperty);
            set => SetValue(ShadowVisibilityProperty, value);
        }

        /// <summary>
        /// Gets or sets a value that indicates whether the <see cref="Flyout"/> uses animations for open/close.
        /// </summary>
        public bool AreAnimationsEnabled
        {
            get => (bool)GetValue(AreAnimationsEnabledProperty);
            set => SetValue(AreAnimationsEnabledProperty, value);
        }

        public event RoutedEventHandler IsOpenChanged
        {
            add { AddHandler(IsOpenChangedEvent, value); }
            remove { RemoveHandler(IsOpenChangedEvent, value); }
        }

        static Flyout()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Flyout), new FrameworkPropertyMetadata(typeof(Flyout)));
        }

        private static void UpdateOpacityChange(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (!(dependencyObject is Flyout flyout))
                return;

            if (flyout._flyoutRoot is null
                || flyout._fadeOutFrame is null
                || System.ComponentModel.DesignerProperties.GetIsInDesignMode(flyout))
                return;

            if (!flyout.AnimateOpacity)
            {
                flyout._fadeOutFrame.Value = 1;
                flyout._flyoutRoot.Opacity = 1;
            }
            else
            {
                flyout._fadeOutFrame.Value = 0;
                if (!flyout.IsOpen)
                {
                    flyout._flyoutRoot.Opacity = 0;
                }
            }
        }

        private static void OnIsOpenPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var flyout = (Flyout)dependencyObject;

            Action openedChangedAction = () =>
            {
                if (e.NewValue != e.OldValue)
                {
                    if (flyout.AreAnimationsEnabled)
                    {
                        if ((bool)e.NewValue)
                        {
                            if (flyout._hideStoryboard != null)
                            {
                                // don't let the storyboard end it's completed event
                                // otherwise it could be hidden on start
                                flyout._hideStoryboard.Completed -= flyout.HideStoryboardCompleted;
                            }

                            flyout.Visibility = Visibility.Visible;
                            flyout.ApplyAnimation(flyout.Position, flyout.AnimateOpacity);
                            flyout.TryFocusElement();
                            if (flyout._showStoryboard != null)
                            {
                                flyout._showStoryboard.Completed += flyout.ShowStoryboardCompleted;
                            }
                        }
                        else
                        {
                            if (flyout._showStoryboard != null)
                            {
                                flyout._showStoryboard.Completed -= flyout.ShowStoryboardCompleted;
                            }

                            if (flyout._hideStoryboard != null)
                            {
                                flyout._hideStoryboard.Completed += flyout.HideStoryboardCompleted;
                            }
                            else
                            {
                                flyout.Hide();
                            }
                        }

                        VisualStateManager.GoToState(flyout, (bool)e.NewValue == false ? "Hide" : "Show", true);
                    }
                    else
                    {
                        if ((bool)e.NewValue)
                        {
                            flyout.Visibility = Visibility.Visible;
                            flyout.TryFocusElement();
                        }
                        else
                        {
                            flyout.Hide();
                        }

                        VisualStateManager.GoToState(flyout, (bool)e.NewValue == false ? "HideDirect" : "ShowDirect", true);
                    }
                }

                flyout.RaiseEvent(new RoutedEventArgs(IsOpenChangedEvent));
            };

            flyout.Dispatcher.BeginInvoke(DispatcherPriority.Background, openedChangedAction);
        }

        private static void OnPositionPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var flyout = (Flyout)dependencyObject;
            var wasOpen = flyout.IsOpen;
            if (wasOpen && flyout.AnimateOnPositionChange)
            {
                flyout.ApplyAnimation((Position)e.NewValue, flyout.AnimateOpacity);
                VisualStateManager.GoToState(flyout, "Hide", true);
            }
            else
            {
                flyout.ApplyAnimation((Position)e.NewValue, flyout.AnimateOpacity, false);
            }

            if (wasOpen && flyout.AnimateOnPositionChange)
            {
                flyout.ApplyAnimation((Position)e.NewValue, flyout.AnimateOpacity);
                VisualStateManager.GoToState(flyout, "Show", true);
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _flyoutRoot = GetTemplateChild("PART_Root") as FrameworkElement;

            if (_flyoutRoot is null)
                return;

            _flyoutContent = GetTemplateChild("PART_Content") as FrameworkElement;
            _showStoryboard = GetTemplateChild("ShowStoryboard") as Storyboard;
            _hideStoryboard = GetTemplateChild("HideStoryboard") as Storyboard;
            _hideFrame = GetTemplateChild("hideFrame") as SplineDoubleKeyFrame;
            _hideFrameY = GetTemplateChild("hideFrameY") as SplineDoubleKeyFrame;
            _showFrame = GetTemplateChild("showFrame") as SplineDoubleKeyFrame;
            _showFrameY = GetTemplateChild("showFrameY") as SplineDoubleKeyFrame;
            _fadeOutFrame = GetTemplateChild("fadeOutFrame") as SplineDoubleKeyFrame;

            if (_hideFrame is null || _showFrame is null || _hideFrameY is null || _showFrameY is null)
                return;

            ApplyAnimation(Position, AnimateOpacity);
        }

        private void ApplyAnimation(Position position, bool animateOpacity, bool resetShowFrame = true)
        {
            if (_flyoutRoot is null
                || _hideFrame is null
                || _showFrame is null
                || _hideFrameY is null
                || _showFrameY is null)
                return;

            _showFrame.Value = 0;

            if (!animateOpacity)
            {
                _fadeOutFrame.Value = 1;
                _flyoutRoot.Opacity = 1;
            }
            else
            {
                _fadeOutFrame.Value = 0;
                if (!IsOpen)
                {
                    _flyoutRoot.Opacity = 0;
                }
            }

            switch (position)
            {
                default:
                    HorizontalAlignment = Margin.Right <= 0 ? HorizontalContentAlignment != HorizontalAlignment.Stretch ? HorizontalAlignment.Left : HorizontalContentAlignment : HorizontalAlignment.Stretch;
                    VerticalAlignment = VerticalAlignment.Stretch;
                    _hideFrame.Value = -_flyoutRoot.ActualWidth - Margin.Left;
                    if (resetShowFrame)
                    {
                        _flyoutRoot.RenderTransform = new TranslateTransform(-_flyoutRoot.ActualWidth, 0);
                    }

                    break;
                case Position.Right:
                    HorizontalAlignment = Margin.Left <= 0 ? HorizontalContentAlignment != HorizontalAlignment.Stretch ? HorizontalAlignment.Right : HorizontalContentAlignment : HorizontalAlignment.Stretch;
                    VerticalAlignment = VerticalAlignment.Stretch;
                    _hideFrame.Value = _flyoutRoot.ActualWidth + Margin.Right;
                    if (resetShowFrame)
                    {
                        _flyoutRoot.RenderTransform = new TranslateTransform(_flyoutRoot.ActualWidth, 0);
                    }

                    break;
                case Position.Top:
                    HorizontalAlignment = HorizontalAlignment.Stretch;
                    VerticalAlignment = Margin.Bottom <= 0 ? VerticalContentAlignment != VerticalAlignment.Stretch ? VerticalAlignment.Top : VerticalContentAlignment : VerticalAlignment.Stretch;
                    _hideFrameY.Value = -_flyoutRoot.ActualHeight - 1 - Margin.Top;
                    if (resetShowFrame)
                    {
                        _flyoutRoot.RenderTransform = new TranslateTransform(0, -_flyoutRoot.ActualHeight - 1);
                    }

                    break;
                case Position.Bottom:
                    HorizontalAlignment = HorizontalAlignment.Stretch;
                    VerticalAlignment = Margin.Top <= 0 ? VerticalContentAlignment != VerticalAlignment.Stretch ? VerticalAlignment.Bottom : VerticalContentAlignment : VerticalAlignment.Stretch;
                    _hideFrameY.Value = _flyoutRoot.ActualHeight + Margin.Bottom;
                    if (resetShowFrame)
                    {
                        _flyoutRoot.RenderTransform = new TranslateTransform(0, _flyoutRoot.ActualHeight);
                    }

                    break;
            }
        }

        private void HideStoryboardCompleted(object sender, EventArgs e)
        {
            _hideStoryboard.Completed -= HideStoryboardCompleted;
            Hide();
        }

        private void ShowStoryboardCompleted(object sender, EventArgs e)
        {
            _showStoryboard.Completed -= ShowStoryboardCompleted;
        }

        private void Hide()
        {
            // hide the flyout, we should get better performance and prevent showing the flyout on any resizing events
            Visibility = Visibility.Hidden;
        }

        private void TryFocusElement()
        {
            // first focus itself
            Focus();

            if (_flyoutContent != null)
                _flyoutContent.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }
    }

    /// <summary>
    /// An Enum representing different positions, such as Left or Right.
    /// </summary>
    public enum Position
    {
        Left,
        Right,
        Top,
        Bottom
    }
}
