using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using System;

namespace SinglePass.WPF.ViewModels.Dialogs
{
    public partial class MaterialMessageBoxViewModel : ObservableObject
    {
        #region Design time instance
        private static readonly Lazy<MaterialMessageBoxViewModel> _lazyDesignTime = new(CreateDesignTime);
        public static MaterialMessageBoxViewModel DesignTimeInstance => _lazyDesignTime.Value;

        private static MaterialMessageBoxViewModel CreateDesignTime()
        {
            var materialMessageBoxVM = new MaterialMessageBoxViewModel();
            materialMessageBoxVM.Header = "Dialog header";
            materialMessageBoxVM.Content = "Lorem ipsum dolor sit amet";
            materialMessageBoxVM.MaterialMessageBoxButtons = MaterialMessageBoxButtons.YesNoCancel;
            materialMessageBoxVM.IconKind = PackIconKind.Warning;
            return materialMessageBoxVM;
        }
        #endregion

        public event Action<MaterialDialogResult?> Accept;

        public MaterialMessageBoxButtons MaterialMessageBoxButtons { get; set; }

        [ObservableProperty]
        private string _header;

        [ObservableProperty]
        private string _content;

        [ObservableProperty]
        private PackIconKind? _iconKind;

        public bool IconVisible => IconKind != null;

        public string Button1Text
        {
            get
            {
                return MaterialMessageBoxButtons switch
                {
                    MaterialMessageBoxButtons.AbortRetryIgnore => SinglePass.Language.Properties.Resources.Abort,
                    MaterialMessageBoxButtons.OK or MaterialMessageBoxButtons.OKCancel => SinglePass.Language.Properties.Resources.OK,
                    MaterialMessageBoxButtons.RetryCancel => SinglePass.Language.Properties.Resources.Retry,
                    MaterialMessageBoxButtons.YesNo or MaterialMessageBoxButtons.YesNoCancel => SinglePass.Language.Properties.Resources.Yes,
                    _ => string.Empty,
                };
            }
        }

        public string Button2Text
        {
            get
            {
                return MaterialMessageBoxButtons switch
                {
                    MaterialMessageBoxButtons.AbortRetryIgnore => SinglePass.Language.Properties.Resources.Retry,
                    MaterialMessageBoxButtons.OKCancel or MaterialMessageBoxButtons.RetryCancel => SinglePass.Language.Properties.Resources.Cancel,
                    MaterialMessageBoxButtons.YesNo or MaterialMessageBoxButtons.YesNoCancel => SinglePass.Language.Properties.Resources.No,
                    _ => string.Empty,
                };
            }
        }

        public bool Button2Visible
        {
            get
            {
                return MaterialMessageBoxButtons switch
                {
                    MaterialMessageBoxButtons.AbortRetryIgnore or
                    MaterialMessageBoxButtons.OKCancel or
                    MaterialMessageBoxButtons.RetryCancel or
                    MaterialMessageBoxButtons.YesNo or
                    MaterialMessageBoxButtons.YesNoCancel => true,
                    _ => false,
                };
            }
        }

        public bool Button2IsCancel
        {
            get
            {
                return MaterialMessageBoxButtons switch
                {
                    MaterialMessageBoxButtons.AbortRetryIgnore => false,
                    MaterialMessageBoxButtons.OK => false,
                    MaterialMessageBoxButtons.OKCancel => true,
                    MaterialMessageBoxButtons.RetryCancel => true,
                    MaterialMessageBoxButtons.YesNo => true,
                    MaterialMessageBoxButtons.YesNoCancel => false,
                    _ => false,
                };
            }
        }

        public string Button3Text
        {
            get
            {
                return MaterialMessageBoxButtons switch
                {
                    MaterialMessageBoxButtons.AbortRetryIgnore => SinglePass.Language.Properties.Resources.Ignore,
                    MaterialMessageBoxButtons.YesNoCancel => SinglePass.Language.Properties.Resources.Cancel,
                    _ => string.Empty,
                };
            }
        }

        public bool Button3Visible
        {
            get
            {
                return MaterialMessageBoxButtons switch
                {
                    MaterialMessageBoxButtons.AbortRetryIgnore or MaterialMessageBoxButtons.YesNoCancel => true,
                    _ => false,
                };
            }
        }

        public bool Button3IsCancel
        {
            get
            {
                return MaterialMessageBoxButtons switch
                {
                    MaterialMessageBoxButtons.AbortRetryIgnore => true,
                    MaterialMessageBoxButtons.OK => false,
                    MaterialMessageBoxButtons.OKCancel => false,
                    MaterialMessageBoxButtons.RetryCancel => false,
                    MaterialMessageBoxButtons.YesNo => false,
                    MaterialMessageBoxButtons.YesNoCancel => true,
                    _ => false,
                };
            }
        }

        public MaterialMessageBoxViewModel()
        {

        }

        [RelayCommand]
        private void Button1Action()
        {
            MaterialDialogResult result = MaterialDialogResult.None;
            switch (MaterialMessageBoxButtons)
            {
                case MaterialMessageBoxButtons.AbortRetryIgnore:
                    result = MaterialDialogResult.Abort;
                    break;
                case MaterialMessageBoxButtons.OK:
                case MaterialMessageBoxButtons.OKCancel:
                    result = MaterialDialogResult.OK;
                    break;
                case MaterialMessageBoxButtons.RetryCancel:
                    result = MaterialDialogResult.Retry;
                    break;
                case MaterialMessageBoxButtons.YesNo:
                case MaterialMessageBoxButtons.YesNoCancel:
                    result = MaterialDialogResult.Yes;
                    break;
            }

            Accept?.Invoke(result);
        }

        [RelayCommand]
        private void Button2Action()
        {
            MaterialDialogResult result = MaterialDialogResult.None;
            switch (MaterialMessageBoxButtons)
            {
                case MaterialMessageBoxButtons.AbortRetryIgnore:
                    result = MaterialDialogResult.Retry;
                    break;
                case MaterialMessageBoxButtons.OKCancel:
                    result = MaterialDialogResult.Cancel;
                    break;
                case MaterialMessageBoxButtons.RetryCancel:
                    result = MaterialDialogResult.Cancel;
                    break;
                case MaterialMessageBoxButtons.YesNo:
                case MaterialMessageBoxButtons.YesNoCancel:
                    result = MaterialDialogResult.No;
                    break;
            }

            Accept?.Invoke(result);
        }

        [RelayCommand]
        private void Button3Action()
        {
            MaterialDialogResult result = MaterialDialogResult.None;
            switch (MaterialMessageBoxButtons)
            {
                case MaterialMessageBoxButtons.AbortRetryIgnore:
                    result = MaterialDialogResult.Ignore;
                    break;
                case MaterialMessageBoxButtons.YesNoCancel:
                    result = MaterialDialogResult.Cancel;
                    break;
            }

            Accept?.Invoke(result);
        }
    }

    public enum MaterialMessageBoxButtons
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

    public enum MaterialDialogResult
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
