using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SinglePass.WPF.Converters
{
    public sealed class InlineCredentialGroupHeaderVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
            values[0] is CollectionViewGroup group && ReferenceEquals(group, values[1])
                ? Visibility.Collapsed
                : Visibility.Visible;

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
