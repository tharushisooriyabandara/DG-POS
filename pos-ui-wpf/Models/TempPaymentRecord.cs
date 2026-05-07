using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace POS_UI.Models
{
    /// <summary>One row from GET /api/v1/temp-payments/{display_order_id}.</summary>
    public class TempPaymentRecord
    {
        [JsonPropertyName("order_id")]
        public string OrderId { get; set; }

        [JsonPropertyName("payment_mode")]
        public string PaymentMode { get; set; }

        [JsonPropertyName("payment_amount")]
        public decimal PaymentAmount { get; set; }

        [JsonPropertyName("transaction_id")]
        public string TransactionId { get; set; }
    }

    internal sealed class TempPaymentsListApiResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("data")]
        public List<TempPaymentRecord> Data { get; set; }
    }
}
