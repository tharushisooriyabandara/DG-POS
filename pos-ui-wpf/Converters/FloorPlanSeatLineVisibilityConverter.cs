using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using POS_UI.Models;

namespace POS_UI.Converters
{
    /// <summary><see cref="Visibility.Visible"/> when the placement is a table (seat line applies); collapsed for custom items.</summary>
    public sealed class FloorPlanSeatLineVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is FloorPlanElementKind k)
            {
                return k == FloorPlanElementKind.Table ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
