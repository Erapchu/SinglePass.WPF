using Erapkey.Core.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Erapkey.Desktop.Helpers
{
    public enum CardInputKind { None, Number, Expiry, Cvv, Pin }

    /// <summary>
    /// Keeps a card field to what the user may enter, whether typed, pasted or dropped:
    /// <c>CardInput.Kind="Number"</c> on a TextBox. The caret stays after the same digit.
    /// </summary>
    public static class CardInput
    {
        public const int MaxCvvDigits = 4;
        public const int MaxPinDigits = 12;

        public static readonly DependencyProperty KindProperty = DependencyProperty.RegisterAttached(
            "Kind", typeof(CardInputKind), typeof(CardInput), new PropertyMetadata(CardInputKind.None, OnKindChanged));

        // The text after the last formatting, to tell typing from deleting.
        private static readonly DependencyProperty LastTextProperty = DependencyProperty.RegisterAttached(
            "LastText", typeof(string), typeof(CardInput), new PropertyMetadata(string.Empty));

        public static CardInputKind GetKind(TextBox box) => (CardInputKind)box.GetValue(KindProperty);
        public static void SetKind(TextBox box, CardInputKind value) => box.SetValue(KindProperty, value);

        private static void OnKindChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not TextBox box)
                return;

            box.TextChanged -= Box_TextChanged;
            if ((CardInputKind)e.NewValue != CardInputKind.None)
                box.TextChanged += Box_TextChanged;
        }

        private static void Box_TextChanged(object sender, TextChangedEventArgs e)
        {
            var box = (TextBox)sender;
            var text = box.Text ?? string.Empty;
            var lastText = (string)box.GetValue(LastTextProperty);
            if (text == lastText)
                return;

            // Text set from the model (a draft being opened) is left as stored: values saved before
            // these rules, such as an expiry "12/2029", must not be cut. Only the user's input is formatted.
            if (!box.IsKeyboardFocusWithin)
            {
                box.SetValue(LastTextProperty, text);
                return;
            }

            var typing = text.Length > lastText.Length;
            var formatted = Format(GetKind(box), text, typing);
            box.SetValue(LastTextProperty, formatted);
            if (formatted == text)
                return;

            var digitsBeforeCaret = CountDigits(text.AsSpan(0, Math.Min(box.CaretIndex, text.Length)));
            box.Text = formatted;
            box.CaretIndex = CaretAfterDigits(formatted, digitsBeforeCaret);
        }

        public static string Format(CardInputKind kind, string text, bool typing) => kind switch
        {
            CardInputKind.Number => CardNumbers.FormatNumberInput(text),
            CardInputKind.Expiry => CardNumbers.FormatExpiryInput(text, typing),
            CardInputKind.Cvv => CardNumbers.OnlyDigits(text, MaxCvvDigits),
            CardInputKind.Pin => CardNumbers.OnlyDigits(text, MaxPinDigits),
            _ => text,
        };

        private static int CountDigits(ReadOnlySpan<char> text)
        {
            var count = 0;
            foreach (var character in text)
            {
                if (char.IsAsciiDigit(character))
                    count++;
            }
            return count;
        }

        /// <summary>Position after the <paramref name="digits"/>-th digit; the end when that is the last digit.</summary>
        private static int CaretAfterDigits(string text, int digits)
        {
            if (digits >= CountDigits(text))
                return text.Length;

            var seen = 0;
            for (var index = 0; index < text.Length; index++)
            {
                if (seen == digits)
                    return index;
                if (char.IsAsciiDigit(text[index]))
                    seen++;
            }
            return text.Length;
        }
    }
}
