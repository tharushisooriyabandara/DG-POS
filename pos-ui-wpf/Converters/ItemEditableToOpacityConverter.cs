using System;
using System.Globalization;
using System.Windows.Data;
using POS_UI.Models;
using POS_UI.ViewModels;

namespace POS_UI.Converters
{
    public class ItemEditableToOpacityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return 1.0;

            var item = values[0] as OrderItem;
            var viewModel = values[1] as CashierHomeViewModel;

            if (item == null || viewModel == null) return 1.0;

            // If the item is editable, show full opacity (1.0)
            // If the item is not editable, show faded opacity (0.4)
            return viewModel.IsItemEditable(item) ? 1.0 : 0.4;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
