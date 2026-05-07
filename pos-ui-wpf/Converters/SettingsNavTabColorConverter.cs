using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace POS_UI.Converters
{
    /// <summary>
    /// Same selection logic as <see cref="StringToColorConverter"/> but inactive uses #EEEEEE
    /// to match the Cashier floor-plan nav tabs (Select Table dialog).
    /// </summary>
    public class SettingsNavTabColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string current && parameter is string target)
            {
                return current == target
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1976D2"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEEEEE"));
            }

            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEEEEE"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
