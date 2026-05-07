using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace POS_UI.Converters
{
    public class CategorySelectionToForegroundMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || !(values[0] is string) || !(values[1] is string))
                return Brushes.Black;

            string currentCategory = (string)values[0];
            string selectedCategory = (string)values[1];

            return currentCategory.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase) ? Brushes.White : Brushes.Black;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 