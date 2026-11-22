using SinglePass.Core.Models;
using System.Windows;
using System.Windows.Controls;

namespace SinglePass.WPF.Views.Controls.Entries
{
    /// <summary>Badge of a payment network; a generic card for <see cref="CardBrand.Unknown"/> and <see cref="CardBrand.Other"/>.</summary>
    public partial class CardBrandIcon : UserControl
    {
        public static readonly DependencyProperty BrandProperty = DependencyProperty.Register(
            nameof(Brand), typeof(CardBrand), typeof(CardBrandIcon),
            new PropertyMetadata(CardBrand.Unknown, (sender, _) => ((CardBrandIcon)sender).ShowBrand()));

        public CardBrand Brand
        {
            get => (CardBrand)GetValue(BrandProperty);
            set => SetValue(BrandProperty, value);
        }

        public CardBrandIcon()
        {
            InitializeComponent();
            ShowBrand();
        }

        private void ShowBrand()
        {
            if (Generic == null)
                return;

            UIElement shown = Brand switch
            {
                CardBrand.Visa => Visa,
                CardBrand.Mastercard => Mastercard,
                CardBrand.Maestro => Maestro,
                CardBrand.Mir => Mir,
                CardBrand.AmericanExpress => AmericanExpress,
                CardBrand.UnionPay => UnionPay,
                CardBrand.Jcb => Jcb,
                CardBrand.Discover => Discover,
                _ => Generic,
            };
            foreach (UIElement child in ((Grid)Generic.Parent).Children)
                child.Visibility = ReferenceEquals(child, shown) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
