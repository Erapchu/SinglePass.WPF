using SinglePass.Core.Services;
using Xunit;

namespace SinglePass.Core.Tests
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
    }
}
