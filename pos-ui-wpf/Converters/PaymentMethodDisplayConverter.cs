using System;
using System.Globalization;
using System.Windows.Data;
using POS_UI.ViewModels;

namespace POS_UI.Converters
{
    public class PaymentMethodDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PaymentMethod pm)
            {
                return pm switch
                {
                    PaymentMethod.Cash => "Cash",
                    PaymentMethod.ManualCard => "Manual Card",
                    PaymentMethod.Card => "Card",
                    PaymentMethod.COD => "POD",
                    PaymentMethod.COT => "POT",
                    PaymentMethod.PAY_LATER => "Pay Later",
                    _ => value?.ToString() ?? ""
                };
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
