using System;
using System.Globalization;
using System.Windows.Data;
using System.Collections.Generic; // Added for Dictionary

namespace POS_UI.Converters
{
    public class ModifierSelectionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 3 && values[0] is int groupId && values[1] is int itemId && values[2] is string itemName)
            {
                return $"{groupId}:{itemId}:{itemName}";
            }
            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 