using System;
using System.Globalization;
using System.Windows.Data;
using POS_UI.Models;

namespace POS_UI.Converters
{
    /// <summary>
    /// Floor plan editor "Add Table" picker: only <see cref="TableStatus.Unavailable"/> tables are disabled;
    /// reserved / drafted / served etc. do not block adding a table to the layout.
    /// </summary>
    public class FloorPlanAddTableEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not TableModel table)
            {
                return false;
            }

            return table.Status != TableStatus.Unavailable;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
