using Erapkey.Desktop.ViewModels.Dialogs;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Erapkey.Desktop.Views.Controls
{
    /// <summary>
    /// In-window card for a <see cref="MessageDialogViewModel"/>, shown by <see cref="ModalHost"/>.
    /// </summary>
    public partial class MessageDialogCard : UserControl, IModalDialog
    {
        public MessageDialogCard()
        {
            InitializeComponent();
            DataContextChanged += (_, e) =>
            {
                if (e.OldValue is MessageDialogViewModel oldViewModel)
                    oldViewModel.Accept -= ViewModel_Accept;
                if (e.NewValue is MessageDialogViewModel newViewModel)
                    newViewModel.Accept += ViewModel_Accept;
            };
        }

        /// <summary>The chosen button; <c>null</c> while the dialog is open or if it was dismissed.</summary>
        public DialogChoice? Result { get; private set; }

        public event Action CloseRequested;

        FrameworkElement IModalDialog.Footer => Footer;

        /// <summary>
        /// The cancel button is focused first, so Enter never confirms by accident.
        /// </summary>
        public IInputElement InitialFocus => DataContext switch
        {
            MessageDialogViewModel { Button3IsCancel: true } => Button3,
            MessageDialogViewModel { Button2IsCancel: true } => Button2,
            _ => Button1,
        };

        public void Cancel()
        {
            if (DataContext is MessageDialogViewModel viewModel)
                ViewModel_Accept(viewModel.CancelChoice);
        }

        private void ViewModel_Accept(DialogChoice? choice)
        {
            Result ??= choice;
            CloseRequested?.Invoke();
        }
    }
}
