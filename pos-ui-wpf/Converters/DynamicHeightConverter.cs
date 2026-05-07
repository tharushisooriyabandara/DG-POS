using System;
using System.Globalization;
using System.Windows.Data;

namespace POS_UI.Converters
{
    public class DynamicHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                // Base height for 4 or fewer items
                if (count <= 4)
                    return 600.0;
                
                // Increase height for more items
                if (count <= 6)
                    return 700.0;
                
                if (count <= 8)
                    return 800.0;
                
                if (count <= 12)
                    return 900.0;
                
                // For more than 12 items, use maximum height
                return 1000.0;
            }
            
            return 600.0; // Default height
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 