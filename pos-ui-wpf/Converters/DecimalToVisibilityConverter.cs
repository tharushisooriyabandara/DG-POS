using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace POS_UI.Converters
{
    public class DecimalToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                return decimalValue > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            
            // Try to parse as decimal if it's a string or other numeric type
            if (decimal.TryParse(value?.ToString(), out decimal parsedValue))
            {
                return parsedValue > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
