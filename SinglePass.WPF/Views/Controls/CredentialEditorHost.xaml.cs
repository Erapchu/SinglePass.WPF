using SinglePass.WPF.Controls;
using SinglePass.WPF.ViewModels;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SinglePass.WPF.Views.Controls
{
    public partial class CredentialEditorHost : UserControl
    {
        private const double WindowInset = 8;
        private const double BackdropOpacity = 0.32;
        private const double ShadowOpacity = 0.36;
        private static readonly TimeSpan LiftDuration = TimeSpan.FromMilliseconds(65);
        private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(275);
        private static readonly TimeSpan CloseDuration = TimeSpan.FromMilliseconds(235);

        private sealed record EditorSession(CredentialsDetailsControl Card, CredentialsDetailsControl Details,
            Button Source, Decorator Anchor, PasswordsViewModel ViewModel)
        {
            public bool IsAdding => Anchor == null;
            public FrameworkElement Origin => IsAdding ? Source : Anchor;
        }

        private EditorSession _session;
        private DispatcherTimer _lift;
        private Rect _layout;
        private bool _closing;
        private bool _waitingForDraft;

        public CredentialEditorHost()
        {
            InitializeComponent();
        }

        internal void OpenForEdit(CredentialsDetailsControl card, Decorator anchor, Button source, PasswordsViewModel viewModel)
        {
            if (_session != null)
                return;

            var session = new EditorSession(card, card, source, anchor, viewModel);
            Open(session);
            if (SystemParameters.ClientAreaAnimation)
            {
                _lift = new DispatcherTimer(LiftDuration, DispatcherPriority.Input, (_, _) => StartEdit(), Dispatcher);
            }
            else
            {
                StartEdit();
            }
        }

        internal async Task OpenForAddAsync(CredentialsDetailsControl details, Button source, PasswordsViewModel viewModel)
        {
            if (_session != null)
                return;

            var card = new CredentialsDetailsControl();
            card.UseDefaultIcon();
            var session = new EditorSession(card, details, source, null, viewModel);
            Open(session);
            try
            {
                await viewModel.AddCommand.ExecuteAsync(null);
            }
            finally
            {
                if (ReferenceEquals(_session, session) && _waitingForDraft)
                    Close();
            }
        }

        private void Open(EditorSession session)
        {
            var origin = GetBounds(session.Origin);
            _session = session;
            _waitingForDraft = session.IsAdding;
            session.ViewModel.PropertyChanged += DraftChanged;

            // The host controls when the form switches modes and releases its draft.
            session.Card.DisplayedDraft = null;
            session.Card.DataContext = session.ViewModel;
            if (session.IsAdding)
                session.Details.DisplayedDraft = null;
            else
                session.Anchor.Child = null;

            session.Card.UseModalSurface(true);
            CardContent.Child = session.Card;
            CardContent.Opacity = session.IsAdding ? 0 : 1;
            Visibility = Visibility.Visible;
            Focus();
            SetLayout(GetLayout(session, origin));
            if (SystemParameters.ClientAreaAnimation)
            {
                var lift = session.IsAdding ? TimeSpan.Zero : LiftDuration;
                AnimateBounds(origin, _layout, OpenDuration, lift, session.IsAdding ? 0 : -6);
                AnimateOpacity(Backdrop, OpacityProperty, 0, BackdropOpacity, OpenDuration);
                AnimateOpacity(CardShadow, OpacityProperty, 0, ShadowOpacity, OpenDuration);
                session.Card.AnimateEditActions(OpacityAnimation(0, 1, OpenDuration, TimeSpan.FromMilliseconds(105)));
            }
            else
            {
                Backdrop.Opacity = BackdropOpacity;
                CardShadow.Opacity = ShadowOpacity;
            }
        }

        private void StartEdit()
        {
            StopLift();
            var viewModel = _session.ViewModel;
            if (viewModel.EditCommand.CanExecute(null))
                viewModel.EditCommand.Execute(null);
            if (!viewModel.IsEditing)
                Close();
        }

        private FrameworkElement Surface => (FrameworkElement)Parent;

        private Rect GetBounds(FrameworkElement element) => new(element.TranslatePoint(new Point(), Surface), element.RenderSize);

        private Rect GetLayout(EditorSession session, Rect origin)
        {
            var width = Surface.ActualWidth - 2 * WindowInset;
            var height = Surface.ActualHeight - 2 * WindowInset;
            if (session.IsAdding)
            {
                width = Math.Min(560, width);
                height = Math.Min(610, height);
                return new Rect((Surface.ActualWidth - width) / 2, (Surface.ActualHeight - height) / 2, width, height);
            }

            width = Math.Min(origin.Width + 132, width);
            var right = Math.Max(WindowInset + width, Math.Min(origin.Right - 10, Surface.ActualWidth - WindowInset));
            var top = Math.Max(WindowInset, origin.Top - 48);
            var bottom = Math.Min(Surface.ActualHeight - WindowInset, origin.Bottom);
            return new Rect(right - width, top, width, Math.Max(1, bottom - top));
        }

        private void SetLayout(Rect bounds)
        {
            _layout = bounds;
            Canvas.SetLeft(CardPanel, bounds.X);
            Canvas.SetTop(CardPanel, bounds.Y);
            CardPanel.Width = bounds.Width;
            CardPanel.Height = bounds.Height;
            ContentClip.Rect = new Rect(bounds.Size);
            CardShadow.ShadowBounds = ContentClip.Rect;
        }

        private void AnimateBounds(Rect from, Rect to, TimeSpan duration, TimeSpan lift = default, double liftOffset = 0)
        {
            // Scale the card surface. The form moves and clips at its final layout size.
            AnimateDouble(CardOffset, TranslateTransform.XProperty, from.X - _layout.X, to.X - _layout.X, duration, lift);
            AnimateDouble(CardOffset, TranslateTransform.YProperty, from.Y - _layout.Y, to.Y - _layout.Y, duration, lift, liftOffset);
            AnimateDouble(SurfaceScale, ScaleTransform.ScaleXProperty, from.Width / _layout.Width, to.Width / _layout.Width, duration, lift);
            AnimateDouble(SurfaceScale, ScaleTransform.ScaleYProperty, from.Height / _layout.Height, to.Height / _layout.Height, duration, lift);

            var startClip = new Rect(from.Size);
            var clip = new RectAnimationUsingKeyFrames { Duration = duration };
            clip.KeyFrames.Add(new DiscreteRectKeyFrame(startClip, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            if (lift > TimeSpan.Zero)
                clip.KeyFrames.Add(new DiscreteRectKeyFrame(startClip, KeyTime.FromTimeSpan(lift)));
            clip.KeyFrames.Add(new EasingRectKeyFrame(new Rect(to.Size), KeyTime.FromTimeSpan(duration), MoveEase()));
            ContentClip.BeginAnimation(RectangleGeometry.RectProperty, clip);
            CardShadow.BeginAnimation(PrerenderedShadow.ShadowBoundsProperty, clip);
        }

        private static CubicEase MoveEase() => new() { EasingMode = EasingMode.EaseInOut };

        private static void AnimateDouble(Animatable target, DependencyProperty property, double from, double to,
            TimeSpan duration, TimeSpan lift, double liftOffset = 0)
        {
            var animation = new DoubleAnimationUsingKeyFrames { Duration = duration };
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            if (lift > TimeSpan.Zero)
                animation.KeyFrames.Add(new EasingDoubleKeyFrame(from + liftOffset, KeyTime.FromTimeSpan(lift),
                    new CubicEase { EasingMode = EasingMode.EaseOut }));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(duration), MoveEase()));
            target.BeginAnimation(property, animation);
        }

        private static DoubleAnimationUsingKeyFrames OpacityAnimation(double from, double to, TimeSpan duration, TimeSpan delay = default)
        {
            var animation = new DoubleAnimationUsingKeyFrames
            {
                Duration = duration,
                KeyFrames =
                {
                    new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)),
                    new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(delay)),
                    new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(duration), MoveEase())
                }
            };
            return animation;
        }

        private static void AnimateOpacity(IAnimatable target, DependencyProperty property, double from, double to, TimeSpan duration) =>
            target.BeginAnimation(property, OpacityAnimation(from, to, duration));

        private void DraftChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_closing)
                return;

            if (e.PropertyName == nameof(PasswordsViewModel.DraftCredential))
            {
                if (_session.ViewModel.DraftCredential is { } draft)
                {
                    _session.Card.DisplayedDraft = draft;
                    if (!_waitingForDraft)
                        return;

                    _waitingForDraft = false;
                    CardContent.Opacity = 1;
                    if (SystemParameters.ClientAreaAnimation)
                        AnimateOpacity(CardContent, OpacityProperty, 0, 1, OpenDuration);
                }
                else if (!_waitingForDraft)
                    Close();
            }
            else if (e.PropertyName == nameof(PasswordsViewModel.SelectedCredentialVM) && _lift != null)
            {
                Close();
            }
        }

        private void Close()
        {
            if (_session is not { } session || _closing)
                return;

            _closing = true;
            StopLift();
            if (_waitingForDraft)
                session.ViewModel.AddCommand.Cancel();

            if (!SystemParameters.ClientAreaAnimation || !session.Origin.IsVisible)
            {
                Release(restoreFocus: true);
                return;
            }

            CardContent.IsEnabled = false;
            Focus();
            var from = new Rect(new Point(_layout.X + CardOffset.X, _layout.Y + CardOffset.Y), ContentClip.Rect.Size);
            AnimateBounds(from, GetBounds(session.Origin), CloseDuration);
            if (session.IsAdding)
                AnimateOpacity(CardContent, OpacityProperty, CardContent.Opacity, 0, CloseDuration);
            AnimateOpacity(CardShadow, OpacityProperty, CardShadow.Opacity, 0, CloseDuration);
            var fade = OpacityAnimation(Backdrop.Opacity, 0, CloseDuration);
            fade.Completed += (_, _) =>
            {
                if (ReferenceEquals(_session, session))
                    Release(restoreFocus: true);
            };
            Backdrop.BeginAnimation(OpacityProperty, fade);
        }

        internal void Dismiss()
        {
            if (_session is not { } session)
                return;

            Release(restoreFocus: false);
            if (session.ViewModel.CancelEditCommand.CanExecute(null))
                session.ViewModel.CancelEditCommand.Execute(null);
        }

        private void Release(bool restoreFocus)
        {
            var session = _session;
            _session = null;
            StopLift();
            session.ViewModel.PropertyChanged -= DraftChanged;
            if (_waitingForDraft)
                session.ViewModel.AddCommand.Cancel();

            _waitingForDraft = false;
            _closing = false;
            Visibility = Visibility.Collapsed;
            Backdrop.BeginAnimation(OpacityProperty, null);
            CardShadow.BeginAnimation(OpacityProperty, null);
            CardContent.BeginAnimation(OpacityProperty, null);
            Backdrop.Opacity = 0;
            CardShadow.Opacity = 0;
            CardContent.IsEnabled = true;
            ResetBoundsAnimations();

            CardContent.Child = null;
            session.Card.AnimateEditActions(null);
            session.Card.DisplayedDraft = null;
            if (session.Anchor != null)
            {
                session.Card.UseModalSurface(false);
                session.Anchor.Child = session.Card;
            }
            session.Card.ClearValue(DataContextProperty);
            session.Details.FollowDraft();

            if (restoreFocus)
                Dispatcher.InvokeAsync(() => session.Source.Focus(), DispatcherPriority.Input);
        }

        private void StopLift()
        {
            _lift?.Stop();
            _lift = null;
        }

        private void ResetBoundsAnimations()
        {
            CardOffset.BeginAnimation(TranslateTransform.XProperty, null);
            CardOffset.BeginAnimation(TranslateTransform.YProperty, null);
            SurfaceScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            SurfaceScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            ContentClip.BeginAnimation(RectangleGeometry.RectProperty, null);
            CardShadow.BeginAnimation(PrerenderedShadow.ShadowBoundsProperty, null);
        }

        private void Host_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_session is not { } session || _closing || e.NewSize.Width <= 16 || e.NewSize.Height <= 16)
                return;

            var layout = GetLayout(session, GetBounds(session.Origin));
            if (layout != _layout)
            {
                ResetBoundsAnimations();
                SetLayout(layout);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key != Key.Escape || _session is not { } session)
                return;

            if (_waitingForDraft || _lift != null)
            {
                e.Handled = true;
                Close();
            }
            else if (session.ViewModel.CancelEditCommand.CanExecute(null))
            {
                e.Handled = true;
                session.ViewModel.CancelEditCommand.Execute(null);
            }
        }
    }
}
