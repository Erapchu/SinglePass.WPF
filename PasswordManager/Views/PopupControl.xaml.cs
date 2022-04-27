using PasswordManager.ViewModels;
using System.Windows.Controls.Primitives;

namespace PasswordManager.Views
{
    /// <summary>
    /// Interaction logic for PopupControl.xaml
    /// </summary>
    public partial class PopupControl : Popup
    {
        private PopupViewModel ViewModel => DataContext as PopupViewModel;

        public PopupControl(PopupViewModel popupViewModel)
        {
            InitializeComponent();

            popupViewModel.Accept += PopupViewModel_Accept;
            DataContext = popupViewModel;
        }

        private void PopupViewModel_Accept()
        {
            IsOpen = false;
        }

        private void Popup_Opened(object sender, System.EventArgs e)
        {
            CredListBox.SelectedIndex = 0;
        }
    }
}
