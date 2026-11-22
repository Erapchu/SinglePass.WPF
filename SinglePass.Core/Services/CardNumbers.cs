using System;
using System.Linq;
using System.Text;

namespace SinglePass.Core.Services
{
    public enum CardBrand { Unknown, Visa, Mastercard, Mir, AmericanExpress, UnionPay, Jcb, Maestro }

    /// <summary>Reading payment card numbers: digits, brand, Luhn check and display forms.</summary>
    public static class CardNumbers
    {
        /// <summary>The digits of a number typed with spaces or dashes; null when it has other characters.</summary>
        public static string Digits(string number)
        {
            if (string.IsNullOrWhiteSpace(number))
                return null;

            var builder = new StringBuilder(number.Length);
            foreach (var character in number)
            {
                if (char.IsAsciiDigit(character))
                    builder.Append(character);
                else if (character != ' ' && character != '-')
                    return null;
            }
            return builder.Length > 0 ? builder.ToString() : null;
        }

        /// <summary>Brand by the number prefix (IIN ranges). Unknown for prefixes this list does not cover.</summary>
        public static CardBrand GetBrand(string number)
        {
            var digits = Digits(number);
            if (digits == null)
                return CardBrand.Unknown;

            int Prefix(int length) => digits.Length >= length ? int.Parse(digits.AsSpan(0, length)) : -1;

            if (Prefix(4) is >= 2200 and <= 2204)
                return CardBrand.Mir;
            if (Prefix(2) is >= 51 and <= 55 || Prefix(4) is >= 2221 and <= 2720)
                return CardBrand.Mastercard;
            if (digits[0] == '4')
                return CardBrand.Visa;
            if (Prefix(2) is 34 or 37)
                return CardBrand.AmericanExpress;
            if (Prefix(4) is >= 3528 and <= 3589)
                return CardBrand.Jcb;
            if (Prefix(2) == 62)
                return CardBrand.UnionPay;
            if (Prefix(2) is 50 or (>= 56 and <= 69))
                return CardBrand.Maestro;
            return CardBrand.Unknown;
        }

        /// <summary>True when the number has 12–19 digits and passes the Luhn check.</summary>
        public static bool IsValid(string number)
        {
            var digits = Digits(number);
            if (digits is not { Length: >= 12 and <= 19 })
                return false;

            var sum = 0;
            for (var index = 0; index < digits.Length; index++)
            {
                var digit = digits[digits.Length - 1 - index] - '0';
                if (index % 2 == 1)
                {
                    digit *= 2;
                    if (digit > 9)
                        digit -= 9;
                }
                sum += digit;
            }
            return sum % 10 == 0;
        }

        /// <summary>Digits in groups as printed on cards: 4-6-5 for American Express, groups of four otherwise.</summary>
        public static string Format(string number)
        {
            var digits = Digits(number);
            if (digits == null)
                return number;

            int[] groups = GetBrand(digits) == CardBrand.AmericanExpress && digits.Length == 15 ? [4, 6, 5] : [];
            var builder = new StringBuilder();
            var position = 0;
            foreach (var length in groups.Concat(Enumerable.Repeat(4, digits.Length)))
            {
                if (position >= digits.Length)
                    break;

                if (builder.Length > 0)
                    builder.Append(' ');
                var take = Math.Min(length, digits.Length - position);
                builder.Append(digits, position, take);
                position += take;
            }
            return builder.ToString();
        }

        /// <summary>The last four digits after a mask, for example <c>•••• 1234</c>; null without digits.</summary>
        public static string Mask(string number)
        {
            var digits = Digits(number);
            return digits is { Length: >= 4 } ? "•••• " + digits[^4..] : null;
        }
    }
}
