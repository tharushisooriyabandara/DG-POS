using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using POS_UI.Services;

namespace POS_UI.Converters
{
    public class CountryToLanguageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string countryCode = null;
            
            // First, try to use the value from the binding (if provided)
            if (value != null && value != DependencyProperty.UnsetValue)
            {
                countryCode = value.ToString();
            }
            
            // If no value from binding, fall back to GlobalDataService
            if (string.IsNullOrWhiteSpace(countryCode))
            {
                var shopDetails = GlobalDataService.Instance.ShopDetails;
                if (shopDetails != null && !string.IsNullOrWhiteSpace(shopDetails.CountryCode))
                {
                    countryCode = shopDetails.CountryCode;
                }
            }
            
            // Determine language code based on country code
            string languageCode = "en-GB"; // Default
            
            if (!string.IsNullOrWhiteSpace(countryCode))
            {
                var countryCodeUpper = countryCode.Trim().ToUpper();
                
                // Map country codes to language codes
                switch (countryCodeUpper)
                {
                    case "GB":
                        languageCode = "en-GB";
                        break;
                    case "US":
                        languageCode = "en-US";
                        break;
                    case "LK":
                        languageCode = "en-LK";
                        break;
                    default:
                        // Default to en-GB for other countries
                        languageCode = "en-GB";
                        break;
                }
            }
            
            // Return string and let WPF's type converter handle it (same as direct XAML usage)
            // This matches the behavior when you write Language="en-GB" directly in XAML
            return languageCode;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter is one-way only
            throw new NotImplementedException();
        }
    }
}
