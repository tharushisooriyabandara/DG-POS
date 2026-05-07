using System.Collections.Generic;

namespace POS_UI.Models
{
    public class FeeTaxComputation
    {
        public int ShopFeeId { get; set; }
        public string Name { get; set; }
        public decimal Amount { get; set; }
        public decimal TaxAmount { get; set; }
        public int? TaxId { get; set; }
        public string TaxCode { get; set; }
        public decimal TaxRate { get; set; }
    }

    public class CartTaxResult
    {
        public List<TaxSummaryRow> SummaryRows { get; set; } = new List<TaxSummaryRow>();
        public Dictionary<System.Guid, List<TaxDetailModel>> ItemTaxDetails { get; set; } = new Dictionary<System.Guid, List<TaxDetailModel>>();
        public Dictionary<System.Guid, decimal> ItemTaxAmounts { get; set; } = new Dictionary<System.Guid, decimal>();
        public decimal DeliveryTaxAmount { get; set; }
        public int? DeliveryTaxId { get; set; }
        public string DeliveryTaxCode { get; set; }
        public decimal DeliveryTaxRate { get; set; }
        public List<OrderShopFeeModel> ShopFees { get; set; } = new List<OrderShopFeeModel>();
        public Dictionary<int, FeeTaxComputation> FeeTaxes { get; set; } = new Dictionary<int, FeeTaxComputation>();

        public decimal TotalTax
        {
            get
            {
                decimal total = 0m;
                foreach (var row in SummaryRows)
                {
                    total += row?.TaxAmount ?? 0m;
                }
                return total;
            }
        }
    }
}

