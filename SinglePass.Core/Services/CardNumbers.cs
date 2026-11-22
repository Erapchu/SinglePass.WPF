using SinglePass.Core.Models;
using System;
using System.Linq;
using System.Text;

namespace SinglePass.Core.Services
{
    /// <summary>Payment card numbers and fields: digits, brand, Luhn check, display forms and input formatting.</summary>
    public static class CardNumbers
    {
        public const int MaxNumberDigits = 19;
        public const char MaskChar = '•';

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
            if (Prefix(4) == 6011 || Prefix(3) is >= 644 and <= 649 || Prefix(2) == 65)
                return CardBrand.Discover;
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
            if (digits is not { Length: >= 12 and <= MaxNumberDigits })
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
            return digits == null ? number : Group(digits);
        }

        /// <summary>
        /// The number grouped as printed, with every digit but the last four replaced by dots,
        /// for example <c>•••• •••• •••• 4444</c>; null without at least four digits.
        /// </summary>
        public static string MaskAllButLast4(string number)
        {
            var digits = Digits(number);
            if (digits is not { Length: >= 4 })
                return null;

            return Group(new string(MaskChar, digits.Length - 4) + digits[^4..], digits);
        }

        /// <summary>The last four digits after a short mask, for example <c>•••• 1234</c>; null without digits.</summary>
        public static string Mask(string number)
        {
            var digits = Digits(number);
            return digits is { Length: >= 4 } ? new string(MaskChar, 4) + " " + digits[^4..] : null;
        }

        /// <summary>What the number field shows while typing: digits only, at most 19, grouped.</summary>
        public static string FormatNumberInput(string text)
        {
            var digits = OnlyDigits(text, MaxNumberDigits);
            return digits.Length == 0 ? string.Empty : Group(digits);
        }

        /// <summary>
        /// What the expiry field shows while typing: <c>MM/YY</c> from the digits typed. A first digit above 1
        /// is a month on its own ("4" becomes "04/"), and a month 00 or above 12 is cut to its first digit.
        /// After a complete month alone the separator is added only when <paramref name="typing"/>, so that
        /// deleting it with Backspace works.
        /// </summary>
        public static string FormatExpiryInput(string text, bool typing = true)
        {
            var digits = OnlyDigits(text, 4);
            if (digits.Length == 0)
                return string.Empty;

            if (digits[0] > '1')
                digits = ("0" + digits)[..Math.Min(4, digits.Length + 1)];
            if (digits.Length >= 2 && int.Parse(digits.AsSpan(0, 2)) is 0 or > 12)
                digits = digits[..1];

            return digits.Length switch
            {
                1 => digits,
                2 => typing ? digits + "/" : digits,
                _ => digits[..2] + "/" + digits[2..],
            };
        }

        /// <summary>Month and two-digit year of a complete <c>MM/YY</c> value.</summary>
        public static bool TryParseExpiry(string expiry, out int month, out int year)
        {
            month = year = 0;
            var digits = OnlyDigits(expiry, int.MaxValue);
            if (digits.Length != 4 || expiry.Trim() is not { Length: 5 } trimmed || trimmed[2] != '/')
                return false;

            month = int.Parse(digits.AsSpan(0, 2));
            year = int.Parse(digits.AsSpan(2, 2));
            return month is >= 1 and <= 12;
        }

        /// <summary>True when the card stops working before <paramref name="today"/> (it works through the whole month).</summary>
        public static bool IsExpired(string expiry, DateTime today) =>
            TryParseExpiry(expiry, out var month, out var year) &&
            new DateTime(2000 + year, month, 1).AddMonths(1) <= today.Date;

        /// <summary>Digits only, at most <paramref name="maxLength"/>.</summary>
        public static string OnlyDigits(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder();
            foreach (var character in text)
            {
                if (char.IsAsciiDigit(character))
                {
                    builder.Append(character);
                    if (builder.Length == maxLength)
                        break;
                }
            }
            return builder.ToString();
        }

        private static string Group(string digits) => Group(digits, digits);

        /// <summary>Splits <paramref name="text"/> into the groups that the brand of <paramref name="digits"/> uses.</summary>
        private static string Group(string text, string digits)
        {
            // American Express numbers have 15 digits; the 4-6-5 groups also apply while one is typed.
            int[] groups = GetBrand(digits) == CardBrand.AmericanExpress && digits.Length <= 15 ? [4, 6, 5] : [];
            var builder = new StringBuilder();
            var position = 0;
            foreach (var length in groups.Concat(Enumerable.Repeat(4, text.Length)))
            {
                if (position >= text.Length)
                    break;

                if (builder.Length > 0)
                    builder.Append(' ');
                var take = Math.Min(length, text.Length - position);
                builder.Append(text, position, take);
                position += take;
            }
            return builder.ToString();
        }
    }
}
