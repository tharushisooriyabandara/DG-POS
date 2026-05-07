using System;
using System.Globalization;
using System.Windows.Data;

namespace POS_UI.Converters
{
    public class DivideByHundredConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return 0m;
            
            if (decimal.TryParse(value.ToString(), out decimal decimalValue))
            {
                return decimalValue / 100m;
            }
            
            return 0m;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return 0m;
            
            if (decimal.TryParse(value.ToString(), out decimal decimalValue))
            {
                return decimalValue * 100m;
            }
            
            return 0m;
        }
    }
}
