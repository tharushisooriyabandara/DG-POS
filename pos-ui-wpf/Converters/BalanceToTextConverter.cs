using System;
using System.Globalization;
using System.Windows.Data;

namespace POS_UI.Converters
{
    public class BalanceToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal balance)
            {
                if (balance > 0)
                {
                    return "Change Due";
                }
                else if (balance < 0)
                {
                    return "Amount Due";
                }
                else
                {
                    return "Exact Amount";
                }
            }
            
            return "Change Due";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 