using System;
using System.Globalization;
using System.Windows.Data;

namespace POS_UI.Converters
{
    public class QuantityToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int quantity)
            {
                return quantity == 1 ? $"{quantity} Item" : $"{quantity} Items";
            }
            
            return "0 Items";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
