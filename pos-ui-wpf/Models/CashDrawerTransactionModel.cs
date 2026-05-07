using System;

namespace POS_UI.Models
{
    public class CashDrawerTransactionModel
    {
        public int CashDrawerId { get; set; }
        public int CashDrawerSessionId { get; set; }
        public int CashMovementId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string MovementType { get; set; }
        public decimal Amount { get; set; }
        public string UserName { get; set; }
        public string Note { get; set; }
    }
}
