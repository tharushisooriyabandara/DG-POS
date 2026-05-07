using System;
using System.Globalization;
using System.Windows.Data;
using POS_UI.Services;

namespace POS_UI.Converters
{
    public class CurrencyFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return string.Empty;
            }

            decimal amount;
            try
            {
                amount = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return string.Empty;
            }

            var currency = GlobalDataService.Instance?.ShopDetails?.Currency ?? "£";
            if (amount == decimal.Zero)
            {
                return parameter is string str && str.Equals("DashOnZero", StringComparison.OrdinalIgnoreCase)
                    ? "-"
                    : $"{currency} 0.00";
            }

            return $"{currency} {amount:F2}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


