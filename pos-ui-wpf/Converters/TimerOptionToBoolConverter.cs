using System;
using System.Globalization;
using System.Windows.Data;

namespace POS_UI.Converters
{
    /// <summary>
    /// Converts SelectedOption (string) to bool for RadioButton IsChecked: true when value equals parameter.
    /// ConvertBack: when checked (true), returns the parameter string to set SelectedOption.
    /// </summary>
    public class TimerOptionToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter != null)
                return parameter.ToString();
            return Binding.DoNothing;
        }
    }
}
