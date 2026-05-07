using System;
using System.Globalization;
using System.Windows.Data;

namespace POS_UI.Converters
{
    public class DynamicScrollHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                // Base scroll height for 4 or fewer items
                if (count <= 4)
                    return 90.0;
                
                // Increase scroll height for more items
                if (count <= 6)
                    return 90.0;
                
                if (count <= 8)
                    return 150.0;
                
                // For more than 8 items, use maximum scroll height
                return 200.0;
            }
            
            return 100.0; // Default scroll height
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 