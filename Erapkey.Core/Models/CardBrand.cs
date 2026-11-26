namespace Erapkey.Core.Models
{
    /// <summary>Payment network of a card. Stored as a number: append new values, never renumber.</summary>
    public enum CardBrand
    {
        /// <summary>Not recognized from the number.</summary>
        Unknown = 0,
        Visa = 1,
        Mastercard = 2,
        Mir = 3,
        AmericanExpress = 4,
        UnionPay = 5,
        Jcb = 6,
        Maestro = 7,
        Discover = 8,
        /// <summary>Chosen by the user for a network this list does not have.</summary>
        Other = 9,
    }
}
