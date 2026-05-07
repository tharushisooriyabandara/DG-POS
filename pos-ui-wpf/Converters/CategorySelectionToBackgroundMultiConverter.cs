using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace POS_UI.Converters
{
    public class CategorySelectionToBackgroundMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || !(values[0] is string) || !(values[1] is string))
                return Brushes.White;

            string currentCategory = (string)values[0];
            string selectedCategory = (string)values[1];

            return currentCategory.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase) ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1976D2")) : Brushes.White;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 