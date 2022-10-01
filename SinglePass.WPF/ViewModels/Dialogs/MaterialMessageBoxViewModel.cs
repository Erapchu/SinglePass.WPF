using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using System;
using System.Windows;

namespace SinglePass.WPF.ViewModels.Dialogs
{
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

    internal class MaterialMessageBoxViewModel : ObservableRecipient
    {
        #region Design time instance
        private static Lazy<MaterialMessageBoxViewModel> _lazyDesignTime = new Lazy<MaterialMessageBoxViewModel>(CreateDesignTime);
        public static MaterialMessageBoxViewModel DesignTimeInstance => _lazyDesignTime.Value;

        private static MaterialMessageBoxViewModel CreateDesignTime()
        {
            var columnsViewModel = new MaterialMessageBoxViewModel("Dialog header", "Lorem ipsum dolor sit amet", MaterialMessageBoxButtons.YesNoCancel, null);
            return columnsViewModel;
        }
        #endregion

        private string _header;
        private string _content;
        private PackIconKind? _iconKind;
        private RelayCommand _button1Command;
        private RelayCommand _button2Command;
        private RelayCommand _button3Command;

        public RelayCommand Button1Command => _button1Command ??= new RelayCommand(Button1Action);
        public RelayCommand Button2Command => _button2Command ??= new RelayCommand(Button2Action);
        public RelayCommand Button3Command => _button3Command ??= new RelayCommand(Button3Action);

        public string Header
        {
            get => _header;
            set
            {
                if (_header == value)
                    return;

                _header = value;
                OnPropertyChanged();
            }
        }

        public HorizontalAlignment HeaderHorizontalAlignment => IconVisible ? HorizontalAlignment.Center : HorizontalAlignment.Left;

        public string Content
        {
            get => _content;
            set
            {
                if (_content == value)
                    return;

                _content = value;
                OnPropertyChanged();
            }
        }

        public PackIconKind? IconKind
        {
            get => _iconKind;
            set
            {
                if (_iconKind == value)
                    return;

                _iconKind = value;
                OnPropertyChanged();
            }
        }

        public bool IconVisible => IconKind != null;

        public string Button1Text
        {
            get
            {
                switch (_materialMessageBoxButtons)
                {
                    case MaterialMessageBoxButtons.AbortRetryIgnore:
                        return SinglePass.Language.Properties.Resources.Abort;
                    case MaterialMessageBoxButtons.OK:
                    case MaterialMessageBoxButtons.OKCancel:
                        return SinglePass.Language.Properties.Resources.OK;
                    case MaterialMessageBoxButtons.RetryCancel:
                        return SinglePass.Language.Properties.Resources.Retry;
                    case MaterialMessageBoxButtons.YesNo:
                    case MaterialMessageBoxButtons.YesNoCancel:
                        return SinglePass.Language.Properties.Resources.Yes;
                    default:
                        return string.Empty;
                }
            }
        }

        public bool Button1IsDefault => true;

        public string Button2Text
        {
            get
            {
                switch (_materialMessageBoxButtons)
                {
                    case MaterialMessageBoxButtons.AbortRetryIgnore:
                        return SinglePass.Language.Properties.Resources.Retry;
                    case MaterialMessageBoxButtons.OKCancel:
                    case MaterialMessageBoxButtons.RetryCancel:
                        return SinglePass.Language.Properties.Resources.Cancel;
                    case MaterialMessageBoxButtons.YesNo:
                    case MaterialMessageBoxButtons.YesNoCancel:
                        return SinglePass.Language.Properties.Resources.No;
                    case MaterialMessageBoxButtons.OK:
                    default:
                        return string.Empty;
                }
            }
        }

        public bool Button2Visible
        {
            get
            {
                switch (_materialMessageBoxButtons)
                {
                    case MaterialMessageBoxButtons.AbortRetryIgnore:
                    case MaterialMessageBoxButtons.OKCancel:
                    case MaterialMessageBoxButtons.RetryCancel:
                    case MaterialMessageBoxButtons.YesNo:
                    case MaterialMessageBoxButtons.YesNoCancel:
                        return true;
                    case MaterialMessageBoxButtons.OK:
                    default:
                        return false;
                }
            }
        }

        public bool Button2IsCancel
        {
            get
            {
                return _materialMessageBoxButtons switch
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
                switch (_materialMessageBoxButtons)
                {
                    case MaterialMessageBoxButtons.AbortRetryIgnore:
                        return SinglePass.Language.Properties.Resources.Ignore;
                    case MaterialMessageBoxButtons.YesNoCancel:
                        return SinglePass.Language.Properties.Resources.Cancel;
                    case MaterialMessageBoxButtons.OK:
                    case MaterialMessageBoxButtons.OKCancel:
                    case MaterialMessageBoxButtons.RetryCancel:
                    case MaterialMessageBoxButtons.YesNo:
                    default:
                        return string.Empty;
                }
            }
        }

        public bool Button3Visible
        {
            get
            {
                switch (_materialMessageBoxButtons)
                {
                    case MaterialMessageBoxButtons.AbortRetryIgnore:
                    case MaterialMessageBoxButtons.YesNoCancel:
                        return true;
                    case MaterialMessageBoxButtons.OKCancel:
                    case MaterialMessageBoxButtons.RetryCancel:
                    case MaterialMessageBoxButtons.YesNo:
                    case MaterialMessageBoxButtons.OK:
                    default:
                        return false;
                }
            }
        }

        public bool Button3IsCancel
        {
            get
            {
                return _materialMessageBoxButtons switch
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

        private readonly string _dialogIdentifier;
        private readonly MaterialMessageBoxButtons _materialMessageBoxButtons;

        public MaterialMessageBoxViewModel(
            string header,
            string content,
            MaterialMessageBoxButtons buttons,
            string dialogIdentifier,
            PackIconKind? packIconKind = null)
        {
            _header = header;
            _content = content;
            _materialMessageBoxButtons = buttons;
            _dialogIdentifier = dialogIdentifier;
            _iconKind = packIconKind;
        }

        private void Button1Action()
        {
            MaterialDialogResult result = MaterialDialogResult.None;
            switch (_materialMessageBoxButtons)
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

            if (DialogHost.IsDialogOpen(_dialogIdentifier))
                DialogHost.Close(_dialogIdentifier, result);
        }

        private void Button2Action()
        {
            MaterialDialogResult result = MaterialDialogResult.None;
            switch (_materialMessageBoxButtons)
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

            if (DialogHost.IsDialogOpen(_dialogIdentifier))
                DialogHost.Close(_dialogIdentifier, result);
        }

        private void Button3Action()
        {
            MaterialDialogResult result = MaterialDialogResult.None;
            switch (_materialMessageBoxButtons)
            {
                case MaterialMessageBoxButtons.AbortRetryIgnore:
                    result = MaterialDialogResult.Ignore;
                    break;
                case MaterialMessageBoxButtons.YesNoCancel:
                    result = MaterialDialogResult.Cancel;
                    break;
            }

            if (DialogHost.IsDialogOpen(_dialogIdentifier))
                DialogHost.Close(_dialogIdentifier, result);
        }
    }
}
