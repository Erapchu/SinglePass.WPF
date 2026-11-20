using SinglePass.WPF.ViewModels.Dialogs;
using System.Windows.Controls;

namespace SinglePass.WPF.Views.Controls
{
    /// <summary>
    /// In-window card for a <see cref="MessageDialogViewModel"/>, shown by <see cref="ModalHost"/>.
    /// </summary>
    public partial class MessageDialogCard : UserControl
    {
        public MessageDialogCard()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The button that cancels the dialog (focused initially so Enter never confirms by accident).
        /// </summary>
        public Button CancelButton => DataContext switch
        {
            MessageDialogViewModel { Button3IsCancel: true } => Button3,
            MessageDialogViewModel { Button2IsCancel: true } => Button2,
            _ => Button1,
        };
    }
}
