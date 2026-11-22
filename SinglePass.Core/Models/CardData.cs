namespace SinglePass.Core.Models
{
    /// <summary>Data of a <see cref="CredentialType.Card"/> entry. Values are kept as the user typed them.</summary>
    public sealed class CardData
    {
        public string Number { get; set; }
        public string Holder { get; set; }

        /// <summary>Expiry as printed on the card, usually <c>MM/YY</c>.</summary>
        public string Expiry { get; set; }

        public string Cvv { get; set; }
        public string Pin { get; set; }

        public CardData Clone() => new()
        {
            Number = Number,
            Holder = Holder,
            Expiry = Expiry,
            Cvv = Cvv,
            Pin = Pin,
        };
    }
}
