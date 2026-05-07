using System;
using System.Globalization;
using System.Windows.Data;

namespace POS_UI.Converters
{
    public class StringEqualsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;

            string value1 = values[0]?.ToString() ?? string.Empty;
            string value2 = values[1]?.ToString() ?? string.Empty;

            return string.Equals(value1, value2, StringComparison.OrdinalIgnoreCase);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
