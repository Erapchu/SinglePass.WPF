using CommunityToolkit.Mvvm.Input;
using Erapkey.Core.Localization;
using Erapkey.Core.Models;
using Erapkey.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Erapkey.ViewModels.Entries
{
    public sealed partial class CardViewModel : CredentialDataViewModel
    {
        public CardData Model { get; }

        public CardViewModel(CardData model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }

        #region Fields
        public string Number
        {
            get => Model.Number;
            set
            {
                if (Set(Model.Number, value, v => Model.Number = v))
                    OnPropertiesChanged(nameof(FormattedNumber), nameof(MaskedNumber), nameof(NumberDigits),
                        nameof(DetectedBrand), nameof(HasNumber), nameof(IsNumberSuspicious));
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
                    OnPropertiesChanged(nameof(HasExpiry), nameof(IsExpiryInvalid), nameof(IsExpired));
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
        #endregion

        #region Number
        /// <summary>The number grouped as printed on the card.</summary>
        public string FormattedNumber => CardNumbers.Format(Number);

        /// <summary>The grouped number with every digit but the last four hidden.</summary>
        public string MaskedNumber => CardNumbers.MaskAllButLast4(Number) ?? new string(CardNumbers.MaskChar, 4);

        /// <summary>What "copy number" puts on the clipboard: digits only, which payment forms accept.</summary>
        public string NumberDigits => CardNumbers.Digits(Number) ?? Number;

        /// <summary>A number was entered but fails the Luhn check: likely a typo. Saving is still allowed.</summary>
        public bool IsNumberSuspicious => HasNumber && !CardNumbers.IsValid(Number);
        #endregion

        #region Expiry
        /// <summary>Something other than a complete <c>MM/YY</c> was entered.</summary>
        public bool IsExpiryInvalid => HasExpiry && !CardNumbers.TryParseExpiry(Expiry, out _, out _);

        public bool IsExpired => CardNumbers.IsExpired(Expiry, DateTime.Today);
        #endregion

        #region Brand
        public sealed record BrandOption(CardBrand Brand, string Name);

        /// <summary>The networks the user can choose from.</summary>
        public IReadOnlyList<BrandOption> BrandOptions { get; } = new[]
        {
            CardBrand.Visa, CardBrand.Mastercard, CardBrand.Mir, CardBrand.AmericanExpress, CardBrand.UnionPay,
            CardBrand.Jcb, CardBrand.Maestro, CardBrand.Discover, CardBrand.Other,
        }.Select(brand => new BrandOption(brand, GetBrandName(brand))).ToArray();

        public CardBrand DetectedBrand => CardNumbers.GetBrand(Number);

        /// <summary>The network shown: the user's choice, otherwise the one detected from the number.</summary>
        public CardBrand Brand => Model.Brand ?? DetectedBrand;

        public bool IsBrandChosen => Model.Brand != null;

        /// <summary>The network selected in the editor. Choosing one stores it; it no longer follows the number.</summary>
        public CardBrand SelectedBrand
        {
            get => Brand == CardBrand.Unknown ? CardBrand.Other : Brand;
            set
            {
                if (value == SelectedBrand)
                    return;

                Model.Brand = value;
                OnBrandChanged();
            }
        }

        [RelayCommand]
        private void DetectBrand()
        {
            if (Model.Brand == null)
                return;

            Model.Brand = null;
            OnBrandChanged();
        }

        public string BrandName => GetBrandName(Brand);

        public static string GetBrandName(CardBrand brand) => brand switch
        {
            CardBrand.Visa => "Visa",
            CardBrand.Mastercard => "Mastercard",
            CardBrand.Mir => "Mir",
            CardBrand.AmericanExpress => "American Express",
            CardBrand.UnionPay => "UnionPay",
            CardBrand.Jcb => "JCB",
            CardBrand.Maestro => "Maestro",
            CardBrand.Discover => "Discover",
            CardBrand.Other => LocalizationManager.Instance["CardBrandOther"],
            _ => null
        };

        private void OnBrandChanged() =>
            OnPropertiesChanged(nameof(Brand), nameof(BrandName), nameof(SelectedBrand), nameof(IsBrandChosen),
                nameof(Subtitle), nameof(SubtitleToolTip));
        #endregion

        // The list shows the network as a badge, so the subtitle is just the end of the number.
        public override string Subtitle => CardNumbers.Mask(Number);

        public override string SubtitleToolTip => HasHolder ? Holder : BrandName;
        public override string Glyph => "";

        // Only the last digits are searchable, as they are what the list shows.
        public override IEnumerable<string> SearchableTexts => [Holder, CardNumbers.Mask(Number), BrandName];

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

            // The shown network follows the number until the user chooses one.
            if (names.Contains(nameof(DetectedBrand)) && Model.Brand == null)
                OnBrandChanged();
        }
    }
}
