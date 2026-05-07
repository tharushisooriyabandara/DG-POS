using System;

namespace POS_UI.Models
{
    public class CashDrawerSessionModel
    {
        public int Id { get; set; }
        public int CashDrawerId { get; set; }
        public int SessionStartedUserId { get; set; }
        public string SessionStartedUser { get; set; }
        public string SessionEndedUser { get; set; }
        public decimal OtherSalesAmount { get; set; }
        public int? SessionEndedUserId { get; set; }
        public DateTime OpenedAt { get; set; }
        public decimal OpeningBalance { get; set; }
        public DateTime? ClosedAt { get; set; }
        public decimal? ClosingBalanceCounted { get; set; }
        public decimal ClosingBalanceExpected { get; set; }
        public decimal Difference { get; set; }
        public decimal TotalInAmount { get; set; }
        public decimal TotalOutAmount { get; set; }
        public decimal TotalSalesAmount { get; set; }
        public decimal TotalRefundAmount { get; set; }
        public decimal TotalCashSaleCashRefundAmount { get; set; }
        public decimal TotalCardSaleCashRefundAmount { get; set; }
        public decimal TotalOtherCashSaleCashRefundAmount { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
