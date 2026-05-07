using System.Collections.Generic;

namespace POS_UI.Models
{
    public class TaxSummaryModel
    {
        public decimal TotalTaxAmount { get; set; }
        public decimal TotalOrderAmount { get; set; }
        public Dictionary<string, TaxBreakdownItem> TaxBreakdown { get; set; } = new Dictionary<string, TaxBreakdownItem>();
    }

    public class TaxBreakdownItem
    {
        public string TaxRate { get; set; }
        public string TaxCode { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal OrderAmount { get; set; }
        
        public string TaxRateDisplay
        {
            get
            {
                if (decimal.TryParse(TaxRate, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rate))
                {
                    return $"{rate:F2}%";
                }
                return TaxRate + "%";
            }
        }
    }
}

