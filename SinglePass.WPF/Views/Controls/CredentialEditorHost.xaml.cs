using SinglePass.WPF.Controls;
using SinglePass.WPF.Helpers;
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
        private const double AddCardRestScale = 1.05;
        private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(180);
        private static readonly TimeSpan ResizeDuration = TimeSpan.FromMilliseconds(210);
        private static readonly TimeSpan CloseDuration = TimeSpan.FromMilliseconds(180);

        private sealed record EditorSession(CredentialsDetailsControl Card, CredentialsDetailsControl Details,
            Button Source, Decorator Anchor, PasswordsViewModel ViewModel, CredentialDeleteConfirmation DeleteCard = null)
        {
            public bool IsAdding => Anchor == null;
            public FrameworkElement Origin => IsAdding || DeleteCard != null ? Source : Anchor;
            public FrameworkElement Footer => DeleteCard != null ? DeleteCard.Footer : Card.EditorFooter;
        }

        private EditorSession _session;
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
            if (viewModel.EditCommand.CanExecute(null))
                viewModel.EditCommand.Execute(null);
            if (!viewModel.IsEditing)
                Close();
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

        internal void OpenForDelete(CredentialsDetailsControl details, Decorator anchor, Button source, PasswordsViewModel viewModel)
        {
            if (_session != null || viewModel.SelectedCredentialVM is not { } credential)
                return;

            var card = new CredentialDeleteConfirmation { DataContext = credential };
            var session = new EditorSession(null, details, source, anchor, viewModel, card);
            _session = session;
            card.CancelButton.Click += (_, _) => Close();
            card.ConfirmButton.Click += async (_, _) => await ConfirmDeletionAsync(session, credential);
            var animate = UiAnimations.ShouldAnimate(this);
            card.Footer.Opacity = animate ? 0 : 1;
            var origin = GetBounds(source);
            ShowCard(card, session, origin);
            if (animate)
            {
                AnimateBounds(origin, _layout, OpenDuration);
                RevealFooter(card.Footer);
            }
            card.CancelButton.Focus();
        }

        private async Task ConfirmDeletionAsync(EditorSession session, CredentialViewModel credential)
        {
            if (!ReferenceEquals(_session, session) || _closing || !CardContent.IsEnabled)
                return;

            var origin = GetBounds(session.Source);
            CardContent.IsEnabled = false;
            Focus();
            var deleted = await session.ViewModel.DeleteCredentialAsync(credential);
            if (!ReferenceEquals(_session, session))
                return;

            if (deleted)
                Close(hiddenOrigin: origin);
            else
            {
                CardContent.IsEnabled = true;
                session.DeleteCard.CancelButton.Focus();
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
            ShowCard(session.Card, session, origin, initiallyHidden: session.IsAdding);
        }

        private void ShowCard(UIElement card, EditorSession session, Rect origin, bool initiallyHidden = false)
        {
            CardContent.Child = card;
            CardPanel.Opacity = initiallyHidden ? 0 : 1;
            Visibility = Visibility.Visible;
            Focus();
            SetLayout(GetLayout(session, origin));
            if (UiAnimations.ShouldAnimate(this))
            {
                AnimateOpacity(Backdrop, OpacityProperty, 0, BackdropOpacity, OpenDuration);
                if (initiallyHidden)
                    CardShadow.Opacity = ShadowOpacity;
                else
                    AnimateOpacity(CardShadow, OpacityProperty, 0, ShadowOpacity, OpenDuration);
            }
            else
            {
                Backdrop.Opacity = BackdropOpacity;
                CardShadow.Opacity = ShadowOpacity;
            }
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
                height = FitEditorHeight(session.Card, width, height);
                return new Rect((Surface.ActualWidth - width) / 2, (Surface.ActualHeight - height) / 2, width, height);
            }

            var pane = session.DeleteCard == null ? origin : GetBounds(session.Anchor);
            width = Math.Min(pane.Width, width);
            if (session.DeleteCard is { } deleteCard)
            {
                width = Math.Min(400, width);
                deleteCard.Measure(new Size(width, double.PositiveInfinity));
                height = Math.Min(height, deleteCard.DesiredSize.Height);
            }
            else
            {
                height = Math.Min(origin.Height, height);
            }
            var rightEdge = session.DeleteCard == null ? pane.Right - 10 : origin.Right;
            var right = Math.Max(WindowInset + width, Math.Min(rightEdge, Surface.ActualWidth - WindowInset));
            var preferredTop = session.DeleteCard == null ? origin.Top + 12 : origin.Top;
            var top = Math.Max(WindowInset, Math.Min(preferredTop, Surface.ActualHeight - WindowInset - height));
            return new Rect(right - width, top, width, height);
        }

        private static double FitEditorHeight(CredentialsDetailsControl card, double width, double availableHeight) =>
            card.IsEditing ? Math.Clamp(card.MeasureEditorHeight(width), 1, availableHeight) : availableHeight;

        private void SetLayout(Rect bounds)
        {
            _layout = bounds;
            Canvas.SetLeft(CardPanel, bounds.X);
            Canvas.SetTop(CardPanel, bounds.Y);
            CardPanel.Width = bounds.Width;
            CardPanel.Height = bounds.Height;
            ContentClip.Rect = new Rect(bounds.Size);
            CardShadow.ShadowBounds = new Rect(bounds.Size);
        }

        private Rect CurrentCardBounds =>
            new(new Point(_layout.X + CardOffset.X, _layout.Y + CardOffset.Y), ContentClip.Rect.Size);

        private void AnimateBounds(Rect from, Rect to, TimeSpan duration)
        {
            // Scale the card surface. The form moves and clips at its final layout size.
            AnimateDouble(CardOffset, TranslateTransform.XProperty, from.X - _layout.X, to.X - _layout.X, duration);
            AnimateDouble(CardOffset, TranslateTransform.YProperty, from.Y - _layout.Y, to.Y - _layout.Y, duration);
            AnimateDouble(SurfaceScale, ScaleTransform.ScaleXProperty, from.Width / _layout.Width, to.Width / _layout.Width, duration);
            AnimateDouble(SurfaceScale, ScaleTransform.ScaleYProperty, from.Height / _layout.Height, to.Height / _layout.Height, duration);

            var clip = new RectAnimation(new Rect(from.Size), new Rect(to.Size), duration) { EasingFunction = MoveEase() };
            ContentClip.BeginAnimation(RectangleGeometry.RectProperty, clip);
            CardShadow.BeginAnimation(PrerenderedShadow.ShadowBoundsProperty, clip);
        }

        private static CubicEase MoveEase() => new() { EasingMode = EasingMode.EaseInOut };

        private static void AnimateDouble(Animatable target, DependencyProperty property, double from, double to, TimeSpan duration) =>
            target.BeginAnimation(property, new DoubleAnimation(from, to, duration) { EasingFunction = MoveEase() });

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

        private static void RevealFooter(FrameworkElement footer) =>
            footer.BeginAnimation(OpacityProperty, OpacityAnimation(0, 1, OpenDuration, TimeSpan.FromMilliseconds(60)));

        private void AnimateAddCard(double opacity, double scale, TimeSpan duration)
        {
            AnimateOpacity(CardPanel, OpacityProperty, CardPanel.Opacity, opacity, duration);
            AnimateDouble(AddCardScale, ScaleTransform.ScaleXProperty, AddCardScale.ScaleX, scale, duration);
            AnimateDouble(AddCardScale, ScaleTransform.ScaleYProperty, AddCardScale.ScaleY, scale, duration);
        }

        private void DraftChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_closing)
                return;

            if (e.PropertyName == nameof(PasswordsViewModel.DraftCredential))
            {
                if (_session.ViewModel.DraftCredential is { } draft)
                {
                    var isAdding = _session.IsAdding;
                    var footer = _session.Card.EditorFooter;
                    var animate = UiAnimations.ShouldAnimate(this);
                    footer.Opacity = animate && !isAdding ? 0 : 1;
                    _session.Card.DisplayedDraft = draft;
                    UpdateEditorLayout(animate: !isAdding, opening: true);
                    if (animate && !isAdding)
                        RevealFooter(footer);

                    if (_waitingForDraft)
                    {
                        _waitingForDraft = false;
                        if (animate)
                        {
                            AddCardScale.ScaleX = AddCardRestScale;
                            AddCardScale.ScaleY = AddCardRestScale;
                            AnimateAddCard(1, 1, OpenDuration);
                        }
                        else
                        {
                            CardPanel.Opacity = 1;
                        }
                    }
                }
                else if (!_waitingForDraft)
                    Close();
            }
        }

        private void Close(Rect? hiddenOrigin = null)
        {
            if (_session is not { } session || _closing)
                return;

            _closing = true;
            if (_waitingForDraft)
                session.ViewModel.AddCommand.Cancel();

            var destinationBounds = !session.IsAdding && session.Origin.IsVisible ? GetBounds(session.Origin) : hiddenOrigin;
            if (!UiAnimations.ShouldAnimate(this) || (!session.IsAdding && destinationBounds == null))
            {
                Release(restoreFocus: true);
                return;
            }

            CardContent.IsEnabled = false;
            Focus();
            if (session.IsAdding)
            {
                AnimateAddCard(0, AddCardRestScale, CloseDuration);
            }
            else
            {
                AnimateBounds(CurrentCardBounds, destinationBounds.Value, CloseDuration);
                var footer = session.Footer;
                AnimateOpacity(footer, OpacityProperty, footer.Opacity, 0, CloseDuration);
                AnimateOpacity(CardShadow, OpacityProperty, CardShadow.Opacity, 0, CloseDuration);
            }
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
            if (session.DeleteCard == null && session.ViewModel.CancelEditCommand.CanExecute(null))
                session.ViewModel.CancelEditCommand.Execute(null);
        }

        private void Release(bool restoreFocus)
        {
            var session = _session;
            _session = null;
            session.ViewModel.PropertyChanged -= DraftChanged;
            if (_waitingForDraft)
                session.ViewModel.AddCommand.Cancel();

            _waitingForDraft = false;
            _closing = false;
            Visibility = Visibility.Collapsed;
            Backdrop.BeginAnimation(OpacityProperty, null);
            CardShadow.BeginAnimation(OpacityProperty, null);
            CardPanel.BeginAnimation(OpacityProperty, null);
            AddCardScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            AddCardScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            CardPanel.Opacity = 1;
            AddCardScale.ScaleX = 1;
            AddCardScale.ScaleY = 1;
            Backdrop.Opacity = 0;
            CardShadow.Opacity = 0;
            CardContent.IsEnabled = true;
            ResetBoundsAnimations();

            CardContent.Child = null;
            session.Footer.BeginAnimation(OpacityProperty, null);
            session.Footer.Opacity = 1;
            if (session.DeleteCard == null)
                ReleaseDraft(session);

            if (restoreFocus)
                Dispatcher.InvokeAsync(() => session.Source.Focus(), DispatcherPriority.Input);
        }

        private static void ReleaseDraft(EditorSession session)
        {
            session.Card.DisplayedDraft = null;
            if (session.Anchor != null)
            {
                session.Card.UseModalSurface(false);
                session.Anchor.Child = session.Card;
            }
            session.Card.ClearValue(DataContextProperty);
            session.Details.FollowDraft();
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
            if (e.NewSize.Width > 2 * WindowInset && e.NewSize.Height > 2 * WindowInset)
                UpdateEditorLayout(animate: false);
        }

        private void Host_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_session is { IsAdding: true } session && session.Card.EditScrollViewer == e.OriginalSource && e.ExtentHeightChange != 0)
                UpdateEditorLayout(animate: true);
        }

        private void UpdateEditorLayout(bool animate, bool opening = false)
        {
            if (_session is not { } session || _closing || (!opening && _waitingForDraft))
                return;

            var origin = GetBounds(session.Origin);
            var layout = GetLayout(session, origin);
            if (layout == _layout && !opening)
                return;

            var from = opening ? origin : CurrentCardBounds;
            SetLayout(layout);
            if (animate && UiAnimations.ShouldAnimate(this))
                AnimateBounds(from, layout, opening ? OpenDuration : ResizeDuration);
            else
                ResetBoundsAnimations();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key != Key.Escape || _session is not { } session || !CardContent.IsEnabled)
                return;

            if (_waitingForDraft || session.DeleteCard != null)
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
