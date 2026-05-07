using System;
using System.Globalization;
using System.Windows.Data;

namespace POS_UI.Converters
{
    public class ModifierButtonTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Check if there are any selected modifiers
            if (value is System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<string>> selectedModifiers && selectedModifiers.Count > 0)
            {
                // Check if any modifier group has selected items
                foreach (var kvp in selectedModifiers)
                {
                    if (kvp.Value != null && kvp.Value.Count > 0)
                    {
                        return "Edit Modifiers";
                    }
                }
            }
            return "Add Modifiers";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 