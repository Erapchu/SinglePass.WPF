namespace SinglePass.Core.Models
{
    /// <summary>Data of a <see cref="CredentialType.Card"/> entry.</summary>
    public sealed class CardData
    {
        /// <summary>Card number; the editor stores the digits grouped as printed on the card.</summary>
        public string Number { get; set; }
        public string Holder { get; set; }

        /// <summary>Expiry as printed on the card, <c>MM/YY</c>.</summary>
        public string Expiry { get; set; }

        public string Cvv { get; set; }
        public string Pin { get; set; }

        /// <summary>Payment network chosen by the user; null to detect it from the number.</summary>
        public CardBrand? Brand { get; set; }

        public CardData Clone() => new()
        {
            Number = Number,
            Holder = Holder,
            Expiry = Expiry,
            Cvv = Cvv,
            Pin = Pin,
            Brand = Brand,
        };
    }
}
