using System;
using System.Globalization;
using System.Windows.Data;
using POS_UI.Services;

namespace POS_UI.Converters
{
    public class CountryBasedDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                var shopDetails = GlobalDataService.Instance.ShopDetails;
                if (shopDetails != null && !string.IsNullOrWhiteSpace(shopDetails.CountryCode))
                {
                    var countryCode = shopDetails.CountryCode.Trim().ToUpper();
                    
                    // Format based on country code
                    switch (countryCode)
                    {
                        case "LK":
                            return dateTime.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
                        case "GB":
                            return dateTime.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                        default:
                            // Default format for other countries
                            return dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    }
                }
                
                // Default format if no country code is available
                return dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string dateString && !string.IsNullOrWhiteSpace(dateString))
            {
                var shopDetails = GlobalDataService.Instance.ShopDetails;
                if (shopDetails != null && !string.IsNullOrWhiteSpace(shopDetails.CountryCode))
                {
                    var countryCode = shopDetails.CountryCode.Trim().ToUpper();
                    
                    // Parse based on country code format
                    switch (countryCode)
                    {
                        case "LK":
                            if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime lkDate))
                                return lkDate;
                            break;
                        case "GB":
                            if (DateTime.TryParseExact(dateString, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime ukDate))
                                return ukDate;
                            break;
                        default:
                            // Try default format for other countries
                            if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime defaultDate))
                                return defaultDate;
                            break;
                    }
                }
                
                // Try default format if no country code is available
                if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fallbackDate))
                    return fallbackDate;
            }
            
            return null;
        }
    }
}
