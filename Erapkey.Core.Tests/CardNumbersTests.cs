using Erapkey.Core.Models;
using Erapkey.Core.Services;
using System;
using Xunit;

namespace Erapkey.Core.Tests
{
    public class CardNumbersTests
    {
        // Published test numbers of the payment networks.
        [Theory]
        [InlineData("4111 1111 1111 1111", CardBrand.Visa)]
        [InlineData("5555-5555-5555-4444", CardBrand.Mastercard)]
        [InlineData("2221000000000009", CardBrand.Mastercard)]
        [InlineData("2200000000000004", CardBrand.Mir)]
        [InlineData("378282246310005", CardBrand.AmericanExpress)]
        [InlineData("3530111333300000", CardBrand.Jcb)]
        [InlineData("6200000000000005", CardBrand.UnionPay)]
        [InlineData("6011111111111117", CardBrand.Discover)]
        [InlineData("6500000000000002", CardBrand.Discover)]
        [InlineData("6759649826438453", CardBrand.Maestro)]
        [InlineData("9999999999999995", CardBrand.Unknown)]
        public void Brand_IsDetectedByPrefix(string number, CardBrand brand)
        {
            Assert.Equal(brand, CardNumbers.GetBrand(number));
        }

        [Theory]
        [InlineData("4111 1111 1111 1111", true)]
        [InlineData("4111 1111 1111 1112", false)]
        [InlineData("378282246310005", true)]
        [InlineData("1234", false)]
        [InlineData("4111 1111 1111 111x", false)]
        [InlineData("", false)]
        public void LuhnCheck(string number, bool valid)
        {
            Assert.Equal(valid, CardNumbers.IsValid(number));
        }

        [Theory]
        [InlineData("4111111111111111", "4111 1111 1111 1111")]
        [InlineData("4111-1111-1111-1111", "4111 1111 1111 1111")]
        [InlineData("378282246310005", "3782 822463 10005")]
        [InlineData("2200000000000000004", "2200 0000 0000 0000 004")]
        [InlineData("not a number", "not a number")]
        public void Number_IsGroupedAsPrinted(string number, string formatted)
        {
            Assert.Equal(formatted, CardNumbers.Format(number));
        }

        [Fact]
        public void Mask_ShowsLastFourDigits()
        {
            Assert.Equal("•••• 1111", CardNumbers.Mask("4111 1111 1111 1111"));
            Assert.Null(CardNumbers.Mask("123"));
            Assert.Null(CardNumbers.Mask(null));
        }

        [Theory]
        [InlineData("5555 5555 5555 4444", "•••• •••• •••• 4444")]
        [InlineData("378282246310005", "•••• •••••• •0005")]
        [InlineData("2200000000000000004", "•••• •••• •••• •••0 004")]
        [InlineData("123", null)]
        public void FullMask_KeepsGroupsAndLastFourDigits(string number, string masked)
        {
            Assert.Equal(masked, CardNumbers.MaskAllButLast4(number));
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("abc", "")]
        [InlineData("4111x1111", "4111 1111")]
        [InlineData("41111111111111111111111", "4111 1111 1111 1111 111")]
        [InlineData("3782822463", "3782 822463")]
        public void NumberInput_KeepsDigitsAndGroupsThem(string typed, string shown)
        {
            Assert.Equal(shown, CardNumbers.FormatNumberInput(typed));
        }

        [Theory]
        [InlineData("1", true, "1")]
        [InlineData("12", true, "12/")]
        [InlineData("12", false, "12")]
        [InlineData("4", true, "04/")]
        [InlineData("13", true, "1")]
        [InlineData("00", true, "0")]
        [InlineData("1229", true, "12/29")]
        [InlineData("12/299", true, "12/29")]
        [InlineData("ab12cd", true, "12/")]
        public void ExpiryInput_IsMonthAndYear(string typed, bool typing, string shown)
        {
            Assert.Equal(shown, CardNumbers.FormatExpiryInput(typed, typing));
        }

        [Theory]
        [InlineData("12/29", true, 12, 29)]
        [InlineData("01/30", true, 1, 30)]
        [InlineData("13/29", false, 0, 0)]
        [InlineData("1/29", false, 0, 0)]
        [InlineData("12/2", false, 0, 0)]
        public void Expiry_IsParsed(string expiry, bool valid, int month, int year)
        {
            Assert.Equal(valid, CardNumbers.TryParseExpiry(expiry, out var parsedMonth, out var parsedYear));
            if (valid)
            {
                Assert.Equal(month, parsedMonth);
                Assert.Equal(year, parsedYear);
            }
        }

        [Fact]
        public void Card_WorksThroughItsLastMonth()
        {
            Assert.False(CardNumbers.IsExpired("11/26", new DateTime(2026, 11, 30)));
            Assert.True(CardNumbers.IsExpired("11/26", new DateTime(2026, 12, 1)));
            Assert.False(CardNumbers.IsExpired("not a date", new DateTime(2026, 12, 1)));
        }

        [Fact]
        public void ChosenBrand_IsCloned()
        {
            var card = new CardData { Brand = CardBrand.Discover };
            Assert.Equal(CardBrand.Discover, card.Clone().Brand);
            Assert.Null(new CardData().Clone().Brand);
        }
    }
}
