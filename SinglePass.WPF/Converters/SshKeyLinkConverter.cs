using SinglePass.Core.Localization;
using SinglePass.WPF.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace SinglePass.WPF.Converters
{
    /// <summary>
    /// The SSH key a host is linked to: values are the key id and <see cref="PasswordsViewModel.SshKeyOptions"/>.
    /// A link to an entry that no longer exists gives an option marked as missing; no link gives null.
    /// </summary>
    public sealed class SshKeyLinkConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not Guid id)
                return null;

            return (values[1] as IEnumerable<PasswordsViewModel.SshKeyOption>)?.FirstOrDefault(option => option.Id == id)
                ?? new PasswordsViewModel.SshKeyOption(id, LocalizationManager.Instance["SshKeyNotFound"], null, IsMissing: true);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
