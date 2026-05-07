namespace POS_UI.Models
{
    public class PaymentModel
    {
        public string PaymentMethod { get; set; }
        public decimal PayingAmount { get; set; }
        public decimal Cash { get; set; }
        public decimal Balance { get; set; }
        public string AuthCode { get; set; }
        public string CardPan { get; set; }
        public string CardScheme { get; set; }
        public string TransactionId { get; set; }
    }
} 