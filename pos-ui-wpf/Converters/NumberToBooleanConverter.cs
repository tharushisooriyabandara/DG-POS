using System;
using System.Globalization;
using System.Windows.Data;

namespace POS_UI.Converters
{
    public class NumberToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue > 0;
            }
            
            if (value is decimal decimalValue)
            {
                return decimalValue > 0;
            }
            
            if (value is double doubleValue)
            {
                return doubleValue > 0;
            }
            
            // Try to parse as number if it's a string or other type
            if (double.TryParse(value?.ToString(), out double parsedValue))
            {
                return parsedValue > 0;
            }
            
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
