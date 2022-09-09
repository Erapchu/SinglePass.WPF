using MaterialDesignThemes.Wpf;
using System;
using System.Globalization;
using System.Windows.Data;

namespace SinglePass.WPF.Converters
{
    [ValueConversion(typeof(int), typeof(PackIcon))]
    internal class BaseThemeToSelectionConverter : IValueConverter
    {
        private readonly PackIcon _selectionPackIcon;

        public BaseThemeToSelectionConverter()
        {
            _selectionPackIcon = new PackIcon
            {
                Kind = PackIconKind.Tick
            };
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is BaseTheme baseTheme && parameter is BaseTheme param)
            {
                return baseTheme == param ? _selectionPackIcon : null;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
