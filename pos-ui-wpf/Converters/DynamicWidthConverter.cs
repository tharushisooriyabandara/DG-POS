using System;
using System.Globalization;
using System.Windows.Data;

namespace POS_UI.Converters
{
    public class DynamicWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                // Base width for 4 or fewer items
                if (count <= 4)
                    return 380.0;
                
                // Decrease width for more items to make it more compact
                if (count <= 6)
                    return 450.0;
                
                if (count <= 8)
                    return 480.0;
                
                if (count <= 12)
                    return 500.0;
                
                // For more than 12 items, use maximum width
                return 520.0;
            }
            
            return 320.0; // Default width
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 