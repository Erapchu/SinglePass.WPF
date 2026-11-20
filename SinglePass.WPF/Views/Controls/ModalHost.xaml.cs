using SinglePass.WPF.Controls;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.ViewModels;
using SinglePass.WPF.ViewModels.Dialogs;
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
    public partial class ModalHost : UserControl
    {
        private const double WindowInset = 8;
        private const double BackdropOpacity = 0.32;
        private const double ShadowOpacity = 0.36;
        private const double CenteredCardRestScale = 1.05;
        private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(180);
        private static readonly TimeSpan ResizeDuration = TimeSpan.FromMilliseconds(210);
        private static readonly TimeSpan CloseDuration = TimeSpan.FromMilliseconds(180);

        private const double DialogMaxWidth = 400;

        /// <summary>
        /// One modal shown by the host: either the credential editor that morphs out of the page,
        /// or a centered dialog card (<see cref="Dialog"/> is set).
        /// </summary>
        /// <param name="Source">Element that receives focus back when the modal closes.</param>
        private sealed record ModalSession(FrameworkElement Source)
        {
            // Credential editor
            public CredentialsDetailsControl Card { get; init; }
            public CredentialsDetailsControl Details { get; init; }
            public Decorator Anchor { get; init; }
            public PasswordsViewModel ViewModel { get; init; }

            // Centered dialog
            public FrameworkElement Dialog { get; init; }
            public FrameworkElement DialogFooter { get; init; }
            /// <summary>Invoked on Escape. Closes the dialog when not set.</summary>
            public Action Cancel { get; init; }
            /// <summary>Invoked once the dialog has been removed from the host.</summary>
            public Action Released { get; init; }

            public bool IsDialog => Dialog != null;
            public bool IsAdding => !IsDialog && Anchor == null;
            public bool IsCentered => IsDialog || IsAdding;
            public FrameworkElement Origin => IsDialog ? null : IsAdding ? Source : Anchor;
            public FrameworkElement Footer => IsDialog ? DialogFooter : Card.EditorFooter;
        }

        private ModalSession _session;
        private Rect _layout;
        private bool _closing;
        private bool _waitingForDraft;
        private TaskCompletionSource _released = NewReleasedSignal();

        public ModalHost()
        {
            InitializeComponent();
        }

        private static TaskCompletionSource NewReleasedSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Whether a dialog can be shown now: nothing is open, or the open modal is already closing
        /// (the dialog then waits for it to finish).
        /// </summary>
        internal bool CanOpen => _session == null || _closing;

        /// <summary>
        /// Shows a dialog card centered in the window.
        /// </summary>
        /// <param name="dialog">The card. It must be a <see cref="FrameworkElement"/>.</param>
        /// <param name="focusReturn">Element focused after the dialog closes; defaults to the currently focused element.</param>
        /// <returns>A task that completes when the dialog raises <see cref="IModalDialog.CloseRequested"/>
        /// or is dismissed by the host; immediately if another modal is open.</returns>
        internal async Task ShowDialogAsync(IModalDialog dialog, FrameworkElement focusReturn = null)
        {
            var element = (FrameworkElement)dialog;
            if (focusReturn == null && Keyboard.FocusedElement is FrameworkElement focused &&
                !IsAncestorOf(focused) && Window.GetWindow(focused) == Window.GetWindow(this))
                focusReturn = focused;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            void Finish()
            {
                completion.TrySetResult();
                if (ReferenceEquals(_session?.Dialog, element))
                    Close();
            }

            // Subscribe first: the dialog may finish while it waits for a closing modal.
            dialog.CloseRequested += Finish;
            try
            {
                while (_session != null)
                {
                    if (!_closing)
                        return;
                    await _released.Task;
                }

                if (completion.Task.IsCompleted)
                    return;

                var session = new ModalSession(focusReturn)
                {
                    Dialog = element,
                    DialogFooter = dialog.Footer,
                    Cancel = dialog.Cancel,
                    Released = () => completion.TrySetResult()
                };
                OpenDialog(session, dialog.InitialFocus);
                await completion.Task;
            }
            finally
            {
                dialog.CloseRequested -= Finish;
            }
        }

        /// <summary>
        /// Shows a message dialog centered in the window.
        /// </summary>
        /// <param name="viewModel">Dialog content and buttons.</param>
        /// <param name="focusReturn">Element focused after the dialog closes.</param>
        /// <returns>The chosen button, or <see cref="DialogChoice.None"/> if the dialog was dismissed
        /// or another modal is already open.</returns>
        internal async Task<DialogChoice> ShowMessageAsync(MessageDialogViewModel viewModel, FrameworkElement focusReturn = null)
        {
            var card = new MessageDialogCard { DataContext = viewModel };
            await ShowDialogAsync(card, focusReturn);
            return card.Result ?? DialogChoice.None;
        }

        internal void OpenForEdit(CredentialsDetailsControl card, Decorator anchor, Button source, PasswordsViewModel viewModel)
        {
            if (_session != null)
                return;

            var session = new ModalSession(source) { Card = card, Details = card, Anchor = anchor, ViewModel = viewModel };
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
            var session = new ModalSession(source) { Card = card, Details = details, ViewModel = viewModel };
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
            // The anchor only limits the dialog width to the details pane.
            var session = new ModalSession(source) { Anchor = anchor, Dialog = card, DialogFooter = card.Footer };
            card.CancelButton.Click += (_, _) => Close();
            card.ConfirmButton.Click += async (_, _) => await ConfirmDeletionAsync(session, card, viewModel, credential);
            OpenDialog(session, card.CancelButton);
        }

        private async Task ConfirmDeletionAsync(ModalSession session, CredentialDeleteConfirmation card,
            PasswordsViewModel viewModel, CredentialViewModel credential)
        {
            if (!ReferenceEquals(_session, session) || _closing || !CardContent.IsEnabled)
                return;

            CardContent.IsEnabled = false;
            Focus();
            var deleted = await viewModel.DeleteCredentialAsync(credential);
            if (!ReferenceEquals(_session, session))
                return;

            if (deleted)
                Close();
            else
            {
                CardContent.IsEnabled = true;
                card.CancelButton.Focus();
            }
        }

        private void OpenDialog(ModalSession session, IInputElement initialFocus)
        {
            _session = session;
            ShowCard(session.Dialog, session, Rect.Empty, initiallyHidden: true);
            RevealCenteredCard(UiAnimations.ShouldAnimate(this));
            if (initialFocus == null)
                return;

            initialFocus.Focus();
            // A freshly added card may not accept focus until it has been laid out.
            Dispatcher.InvokeAsync(() =>
            {
                if (ReferenceEquals(_session, session) && !_closing && !CardContent.IsKeyboardFocusWithin)
                    initialFocus.Focus();
            }, DispatcherPriority.Loaded);
        }

        private void Open(ModalSession session)
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

        private void ShowCard(UIElement card, ModalSession session, Rect origin, bool initiallyHidden = false)
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

        private Rect GetLayout(ModalSession session, Rect origin)
        {
            var width = Surface.ActualWidth - 2 * WindowInset;
            var height = Surface.ActualHeight - 2 * WindowInset;
            if (session.IsAdding)
            {
                width = Math.Min(560, width);
                height = FitEditorHeight(session.Card, width, height);
                return CenterCard(width, height);
            }

            if (session.Dialog is { } dialog)
            {
                width = Math.Min(DialogMaxWidth, width);
                if (session.Anchor != null)
                    width = Math.Min(GetBounds(session.Anchor).Width, width);
                dialog.Measure(new Size(width, double.PositiveInfinity));
                height = Math.Min(height, dialog.DesiredSize.Height);
                return CenterCard(width, height);
            }

            width = Math.Min(origin.Width, width);
            height = Math.Min(origin.Height, height);
            var right = Math.Max(WindowInset + width, Math.Min(origin.Right - 10, Surface.ActualWidth - WindowInset));
            var preferredTop = origin.Top + 12;
            var top = Math.Max(WindowInset, Math.Min(preferredTop, Surface.ActualHeight - WindowInset - height));
            return new Rect(right - width, top, width, height);
        }

        private Rect CenterCard(double width, double height) =>
            new((Surface.ActualWidth - width) / 2, (Surface.ActualHeight - height) / 2, width, height);

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

        private void RevealCenteredCard(bool animate)
        {
            if (!animate)
            {
                CardPanel.Opacity = 1;
                return;
            }

            CenteredCardScale.ScaleX = CenteredCardRestScale;
            CenteredCardScale.ScaleY = CenteredCardRestScale;
            AnimateCenteredCard(1, 1, OpenDuration);
        }

        private void AnimateCenteredCard(double opacity, double scale, TimeSpan duration)
        {
            AnimateOpacity(CardPanel, OpacityProperty, CardPanel.Opacity, opacity, duration);
            AnimateDouble(CenteredCardScale, ScaleTransform.ScaleXProperty, CenteredCardScale.ScaleX, scale, duration);
            AnimateDouble(CenteredCardScale, ScaleTransform.ScaleYProperty, CenteredCardScale.ScaleY, scale, duration);
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
                        RevealCenteredCard(animate);
                    }
                }
                else if (!_waitingForDraft)
                    Close();
            }
        }

        private void Close()
        {
            if (_session is not { } session || _closing)
                return;

            _closing = true;
            if (_waitingForDraft)
                session.ViewModel.AddCommand.Cancel();

            if (!UiAnimations.ShouldAnimate(this) || (!session.IsCentered && !session.Origin.IsVisible))
            {
                Release(restoreFocus: true);
                return;
            }

            CardContent.IsEnabled = false;
            Focus();
            if (session.IsCentered)
            {
                AnimateCenteredCard(0, CenteredCardRestScale, CloseDuration);
            }
            else
            {
                AnimateBounds(CurrentCardBounds, GetBounds(session.Origin), CloseDuration);
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
            if (!session.IsDialog && session.ViewModel.CancelEditCommand.CanExecute(null))
                session.ViewModel.CancelEditCommand.Execute(null);
        }

        private void Release(bool restoreFocus)
        {
            var session = _session;
            _session = null;
            if (session.ViewModel != null)
                session.ViewModel.PropertyChanged -= DraftChanged;
            if (_waitingForDraft)
                session.ViewModel.AddCommand.Cancel();

            _waitingForDraft = false;
            _closing = false;
            Visibility = Visibility.Collapsed;
            Backdrop.BeginAnimation(OpacityProperty, null);
            CardShadow.BeginAnimation(OpacityProperty, null);
            CardPanel.BeginAnimation(OpacityProperty, null);
            CenteredCardScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            CenteredCardScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            CardPanel.Opacity = 1;
            CenteredCardScale.ScaleX = 1;
            CenteredCardScale.ScaleY = 1;
            Backdrop.Opacity = 0;
            CardShadow.Opacity = 0;
            CardContent.IsEnabled = true;
            ResetBoundsAnimations();

            CardContent.Child = null;
            session.Footer.BeginAnimation(OpacityProperty, null);
            session.Footer.Opacity = 1;
            if (!session.IsDialog)
                ReleaseDraft(session);

            if (restoreFocus && session.Source is { } source)
                Dispatcher.InvokeAsync(() => source.Focus(), DispatcherPriority.Input);
            session.Released?.Invoke();
            var released = _released;
            _released = NewReleasedSignal();
            released.TrySetResult();
        }

        private static void ReleaseDraft(ModalSession session)
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
            if (e.ExtentHeightChange == 0 || _session is not { } session)
                return;

            if (session.IsAdding && session.Card.EditScrollViewer == e.OriginalSource)
                UpdateEditorLayout(animate: true);
            // Dialog content is measured on open, before its bindings have been applied;
            // fit the card again once the real text is laid out.
            else if (session.IsDialog)
                UpdateEditorLayout(animate: false);
        }

        private void UpdateEditorLayout(bool animate, bool opening = false)
        {
            if (_session is not { } session || _closing || (!opening && _waitingForDraft))
                return;

            var origin = session.Origin is { } originElement ? GetBounds(originElement) : Rect.Empty;
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

            if (session.IsDialog)
            {
                e.Handled = true;
                if (session.Cancel != null)
                    session.Cancel();
                else
                    Close();
            }
            else if (_waitingForDraft)
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
