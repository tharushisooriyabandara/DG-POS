using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace POS_UI.Converters
{
    public class BalanceToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal balance)
            {
                if (balance > 0)
                {
                    // Positive balance (change due) - Green
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
                }
                else if (balance < 0)
                {
                    // Negative balance (insufficient cash) - Red
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
                }
                else
                {
                    // Zero balance (exact amount) - Blue
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1976D2"));
                }
            }
            
            // Default color
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#424242"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 