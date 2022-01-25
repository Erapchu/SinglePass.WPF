using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using System;
using System.Windows;

namespace PasswordManager.Views.MessageBox
{
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

        #region public string Header
        private string _header;
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
        #endregion

        #region public string Content
        private string _content;
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
        #endregion

        private PackIconKind? _iconKind;
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

        #region public string Button1
        public string Button1Text
        {
            get
            {
                switch (_materialMessageBoxButtons)
                {
                    case MaterialMessageBoxButtons.AbortRetryIgnore:
                        return "Abort";
                    case MaterialMessageBoxButtons.OK:
                    case MaterialMessageBoxButtons.OKCancel:
                        return "OK";
                    case MaterialMessageBoxButtons.RetryCancel:
                        return "Retry";
                    case MaterialMessageBoxButtons.YesNo:
                    case MaterialMessageBoxButtons.YesNoCancel:
                        return "Yes";
                    default:
                        return string.Empty;
                }
            }
        }

        public bool Button1IsDefault => true;
        #endregion

        #region public string Button2
        public string Button2Text
        {
            get
            {
                switch (_materialMessageBoxButtons)
                {
                    case MaterialMessageBoxButtons.AbortRetryIgnore:
                        return "Retry";
                    case MaterialMessageBoxButtons.OKCancel:
                    case MaterialMessageBoxButtons.RetryCancel:
                        return "Cancel";
                    case MaterialMessageBoxButtons.YesNo:
                    case MaterialMessageBoxButtons.YesNoCancel:
                        return "No";
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
        #endregion

        #region public string Button3
        public string Button3Text
        {
            get
            {
                switch (_materialMessageBoxButtons)
                {
                    case MaterialMessageBoxButtons.AbortRetryIgnore:
                        return "Ignore";
                    case MaterialMessageBoxButtons.YesNoCancel:
                        return "Cancel";
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
        #endregion

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

        #region Commands

        private RelayCommand _button1Command;
        public RelayCommand Button1Command => _button1Command
            ?? (_button1Command = new RelayCommand(Button1Action));

        private RelayCommand _button2Command;
        public RelayCommand Button2Command => _button2Command
            ?? (_button2Command = new RelayCommand(Button2Action));

        private RelayCommand _button3Command;
        public RelayCommand Button3Command => _button3Command
            ?? (_button3Command = new RelayCommand(Button3Action));
        #endregion
    }
}
