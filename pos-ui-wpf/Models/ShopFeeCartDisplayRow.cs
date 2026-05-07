namespace POS_UI.Models
{
    /// <summary>Shop fee row for cashier cart UI, including optional fees the user removed (shown faded, not in totals).</summary>
    public sealed class ShopFeeCartDisplayRow
    {
        public int ShopFeeId { get; set; }
        public string Name { get; set; }
        public decimal Amount { get; set; }
        public bool IsMandatory { get; set; }
        public bool IsRemoved { get; set; }
        public string FeeType { get; set; }
        public decimal FeeValue { get; set; }
    }
}
