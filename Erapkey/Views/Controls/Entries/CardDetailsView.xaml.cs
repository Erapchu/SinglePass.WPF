using System.Windows;
using System.Windows.Controls;

namespace Erapkey.Views.Controls.Entries
{
    public partial class CardDetailsView : UserControl
    {
        public CardDetailsView()
        {
            InitializeComponent();
        }

        private void View_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // The card reuses this view when another entry is selected: hide its secrets again.
            RevealNumber.IsChecked = false;
            RevealCvv.IsChecked = false;
            RevealPin.IsChecked = false;
        }
    }
}
