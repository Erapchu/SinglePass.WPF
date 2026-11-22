using SinglePass.Core.Models;
using SinglePass.Core.Services;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SinglePass.WPF.ViewModels.Entries
{
    public sealed class CardViewModel : CredentialDataViewModel
    {
        public CardData Model { get; }

        public CardViewModel(CardData model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public string Number
        {
            get => Model.Number;
            set
            {
                if (Set(Model.Number, value, v => Model.Number = v))
                    OnPropertiesChanged(nameof(FormattedNumber), nameof(MaskedNumber), nameof(NumberDigits), nameof(Brand), nameof(BrandName),
                        nameof(HasNumber), nameof(IsNumberSuspicious), nameof(Subtitle), nameof(SubtitleToolTip));
            }
        }

        public string Holder
        {
            get => Model.Holder;
            set
            {
                if (Set(Model.Holder, value, v => Model.Holder = v))
                    OnPropertiesChanged(nameof(HasHolder), nameof(SubtitleToolTip));
            }
        }

        public string Expiry
        {
            get => Model.Expiry;
            set
            {
                if (Set(Model.Expiry, value, v => Model.Expiry = v))
                    OnPropertiesChanged(nameof(HasExpiry));
            }
        }

        public string Cvv
        {
            get => Model.Cvv;
            set
            {
                if (Set(Model.Cvv, value, v => Model.Cvv = v))
                    OnPropertiesChanged(nameof(HasCvv));
            }
        }

        public string Pin
        {
            get => Model.Pin;
            set
            {
                if (Set(Model.Pin, value, v => Model.Pin = v))
                    OnPropertiesChanged(nameof(HasPin));
            }
        }

        public bool HasNumber => !string.IsNullOrWhiteSpace(Number);
        public bool HasHolder => !string.IsNullOrWhiteSpace(Holder);
        public bool HasExpiry => !string.IsNullOrWhiteSpace(Expiry);
        public bool HasCvv => !string.IsNullOrEmpty(Cvv);
        public bool HasPin => !string.IsNullOrEmpty(Pin);

        /// <summary>The number grouped as printed on the card.</summary>
        public string FormattedNumber => CardNumbers.Format(Number);

        public string MaskedNumber => CardNumbers.Mask(Number);

        /// <summary>What "copy number" puts on the clipboard: digits only, which payment forms accept.</summary>
        public string NumberDigits => CardNumbers.Digits(Number) ?? Number;

        public CardBrand Brand => CardNumbers.GetBrand(Number);

        public string BrandName => Brand switch
        {
            CardBrand.Visa => "Visa",
            CardBrand.Mastercard => "Mastercard",
            CardBrand.Mir => "Mir",
            CardBrand.AmericanExpress => "American Express",
            CardBrand.UnionPay => "UnionPay",
            CardBrand.Jcb => "JCB",
            CardBrand.Maestro => "Maestro",
            _ => null
        };

        /// <summary>A number was entered but fails the Luhn check: likely a typo. Saving is still allowed.</summary>
        public bool IsNumberSuspicious => HasNumber && !CardNumbers.IsValid(Number);

        public override string Subtitle => (BrandName, MaskedNumber) switch
        {
            (null, null) => null,
            (null, var masked) => masked,
            (var brand, null) => brand,
            (var brand, var masked) => brand + " " + masked,
        };

        public override string SubtitleToolTip => HasHolder ? Holder : Subtitle;
        public override string Glyph => "";

        // Only the last digits are searchable, as they are what the list shows.
        public override IEnumerable<string> SearchableTexts => [Holder, MaskedNumber, BrandName];

        private bool Set(string current, string value, Action<string> assign, [CallerMemberName] string propertyName = null)
        {
            if (current == value)
                return false;

            assign(value);
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertiesChanged(params string[] names)
        {
            foreach (var name in names)
                OnPropertyChanged(name);
        }
    }
}
