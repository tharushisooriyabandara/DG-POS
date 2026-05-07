using System;
using System.Windows.Markup;
using POS_UI.Services;

namespace POS_UI.Converters
{
    public class CountryToLanguageExtension : MarkupExtension
    {
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var shopDetails = GlobalDataService.Instance.ShopDetails;
            string languageCode = "en-GB"; // Default
            
            if (shopDetails != null && !string.IsNullOrWhiteSpace(shopDetails.CountryCode))
            {
                var countryCode = shopDetails.CountryCode.Trim().ToUpper();
                
                // Map country codes to language codes
                switch (countryCode)
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
                        languageCode = "en-GB";
                        break;
                }
            }
            
            // Return XmlLanguage directly for DatePicker Language property
            return XmlLanguage.GetLanguage(languageCode);
        }
    }
}

