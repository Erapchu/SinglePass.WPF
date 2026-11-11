using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace SinglePass.WPF.ViewModels.Dialogs
{
    public partial class MessageDialogViewModel : ObservableObject
    {
        #region Design time instance
        private static readonly Lazy<MessageDialogViewModel> _lazyDesignTime = new(CreateDesignTime);
        public static MessageDialogViewModel DesignTimeInstance => _lazyDesignTime.Value;

        private static MessageDialogViewModel CreateDesignTime()
        {
            var viewModel = new MessageDialogViewModel();
            viewModel.Header = "Dialog header";
            viewModel.Content = "Lorem ipsum dolor sit amet";
            viewModel.Buttons = MessageDialogButtons.YesNoCancel;
            viewModel.Glyph = "\uE7BA";
            return viewModel;
        }
        #endregion

        public event Action<DialogChoice?> Accept;

        public MessageDialogButtons Buttons { get; set; }

        [ObservableProperty]
        public partial string Header { get; set; }

        [ObservableProperty]
        public partial string Content { get; set; }

        [ObservableProperty]
        public partial string Glyph { get; set; }

        public bool IconVisible => !string.IsNullOrEmpty(Glyph);

        public string Button1Text
        {
            get
            {
                return Buttons switch
                {
                    MessageDialogButtons.AbortRetryIgnore => SinglePass.Language.Properties.Resources.Abort,
                    MessageDialogButtons.OK or MessageDialogButtons.OKCancel => SinglePass.Language.Properties.Resources.OK,
                    MessageDialogButtons.RetryCancel => SinglePass.Language.Properties.Resources.Retry,
                    MessageDialogButtons.YesNo or MessageDialogButtons.YesNoCancel => SinglePass.Language.Properties.Resources.Yes,
                    _ => string.Empty,
                };
            }
        }

        public string Button2Text
        {
            get
            {
                return Buttons switch
                {
                    MessageDialogButtons.AbortRetryIgnore => SinglePass.Language.Properties.Resources.Retry,
                    MessageDialogButtons.OKCancel or MessageDialogButtons.RetryCancel => SinglePass.Language.Properties.Resources.Cancel,
                    MessageDialogButtons.YesNo or MessageDialogButtons.YesNoCancel => SinglePass.Language.Properties.Resources.No,
                    _ => string.Empty,
                };
            }
        }

        public bool Button2Visible
        {
            get
            {
                return Buttons switch
                {
                    MessageDialogButtons.AbortRetryIgnore or
                    MessageDialogButtons.OKCancel or
                    MessageDialogButtons.RetryCancel or
                    MessageDialogButtons.YesNo or
                    MessageDialogButtons.YesNoCancel => true,
                    _ => false,
                };
            }
        }

        public bool Button2IsCancel
        {
            get
            {
                return Buttons switch
                {
                    MessageDialogButtons.AbortRetryIgnore => false,
                    MessageDialogButtons.OK => false,
                    MessageDialogButtons.OKCancel => true,
                    MessageDialogButtons.RetryCancel => true,
                    MessageDialogButtons.YesNo => true,
                    MessageDialogButtons.YesNoCancel => false,
                    _ => false,
                };
            }
        }

        public string Button3Text
        {
            get
            {
                return Buttons switch
                {
                    MessageDialogButtons.AbortRetryIgnore => SinglePass.Language.Properties.Resources.Ignore,
                    MessageDialogButtons.YesNoCancel => SinglePass.Language.Properties.Resources.Cancel,
                    _ => string.Empty,
                };
            }
        }

        public bool Button3Visible
        {
            get
            {
                return Buttons switch
                {
                    MessageDialogButtons.AbortRetryIgnore or MessageDialogButtons.YesNoCancel => true,
                    _ => false,
                };
            }
        }

        public bool Button3IsCancel
        {
            get
            {
                return Buttons switch
                {
                    MessageDialogButtons.AbortRetryIgnore => true,
                    MessageDialogButtons.OK => false,
                    MessageDialogButtons.OKCancel => false,
                    MessageDialogButtons.RetryCancel => false,
                    MessageDialogButtons.YesNo => false,
                    MessageDialogButtons.YesNoCancel => true,
                    _ => false,
                };
            }
        }

        public MessageDialogViewModel()
        {

        }

        [RelayCommand]
        private void Button1Action()
        {
            DialogChoice result = DialogChoice.None;
            switch (Buttons)
            {
                case MessageDialogButtons.AbortRetryIgnore:
                    result = DialogChoice.Abort;
                    break;
                case MessageDialogButtons.OK:
                case MessageDialogButtons.OKCancel:
                    result = DialogChoice.OK;
                    break;
                case MessageDialogButtons.RetryCancel:
                    result = DialogChoice.Retry;
                    break;
                case MessageDialogButtons.YesNo:
                case MessageDialogButtons.YesNoCancel:
                    result = DialogChoice.Yes;
                    break;
            }

            Accept?.Invoke(result);
        }

        [RelayCommand]
        private void Button2Action()
        {
            DialogChoice result = DialogChoice.None;
            switch (Buttons)
            {
                case MessageDialogButtons.AbortRetryIgnore:
                    result = DialogChoice.Retry;
                    break;
                case MessageDialogButtons.OKCancel:
                    result = DialogChoice.Cancel;
                    break;
                case MessageDialogButtons.RetryCancel:
                    result = DialogChoice.Cancel;
                    break;
                case MessageDialogButtons.YesNo:
                case MessageDialogButtons.YesNoCancel:
                    result = DialogChoice.No;
                    break;
            }

            Accept?.Invoke(result);
        }

        [RelayCommand]
        private void Button3Action()
        {
            DialogChoice result = DialogChoice.None;
            switch (Buttons)
            {
                case MessageDialogButtons.AbortRetryIgnore:
                    result = DialogChoice.Ignore;
                    break;
                case MessageDialogButtons.YesNoCancel:
                    result = DialogChoice.Cancel;
                    break;
            }

            Accept?.Invoke(result);
        }
    }

    public enum MessageDialogButtons
    {
        /// <summary>
        /// The message box contains an OK button.
        /// </summary>
        OK,

        /// <summary>
        /// The message box contains OK and Cancel buttons.
        /// </summary>
        OKCancel,

        /// <summary>
        /// The message box contains Yes and No buttons.
        /// </summary>
        YesNo,

        /// <summary>
        /// The message box contains Yes, No, and Cancel buttons.
        /// </summary>
        YesNoCancel,

        /// <summary>
        /// The message box contains Abort, Retry, and Ignore buttons.
        /// </summary>
        AbortRetryIgnore,

        /// <summary>
        /// The message box contains Retry and Cancel buttons.
        /// </summary>
        RetryCancel
    }

    public enum DialogChoice
    {
        /// <summary>
        /// Nothing is returned from the dialog box. This means that the modal dialog continues running.
        /// </summary>
        None = 0,

        /// <summary>
        /// The dialog box return value is OK (usually sent from a button labeled OK).
        /// </summary>
        OK = 1,

        /// <summary>
        /// The dialog box return value is Cancel (usually sent from a button labeled Cancel).
        /// </summary>
        Cancel = 2,

        /// <summary>
        /// The dialog box return value is Abort (usually sent from a button labeled Abort).
        /// </summary>
        Abort = 3,

        /// <summary>
        /// The dialog box return value is Retry (usually sent from a button labeled Retry).
        /// </summary>
        Retry = 4,

        /// <summary>
        /// The dialog box return value is Ignore (usually sent from a button labeled Ignore).
        /// </summary>
        Ignore = 5,

        /// <summary>
        /// The dialog box return value is Yes (usually sent from a button labeled Yes).
        /// </summary>
        Yes = 6,

        /// <summary>
        /// The dialog box return value is No (usually sent from a button labeled No).
        /// </summary>
        No = 7
    }
}
