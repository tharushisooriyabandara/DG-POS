using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace POS_UI.Converters
{
    /// <summary>Maps a MaterialDesign icon name string to <see cref="PackIconKind"/> for bindings.</summary>
    public sealed class PackIconKindConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var s = value?.ToString();
            if (string.IsNullOrWhiteSpace(s))
            {
                return PackIconKind.MapMarker;
            }

            return Enum.TryParse(s, ignoreCase: true, out PackIconKind kind) ? kind : PackIconKind.MapMarker;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
