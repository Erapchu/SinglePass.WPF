using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PasswordManager.Views.InputBox
{
    /// <summary>
    /// Interaction logic for MaterialInputBoxContent.xaml
    /// </summary>
    public partial class MaterialInputBoxContent : UserControl
    {
        public MaterialInputBoxContent()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(200);
            InputedTextBox.Focus();
        }
    }
}
