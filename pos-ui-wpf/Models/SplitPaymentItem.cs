using System;
using System.Collections.Generic;
using System.Linq;

namespace POS_UI.Models
{
    /// <summary>One line for delivery-platform complete-order API when payment_mode is SPLIT.</summary>
    public class DeliveryPlatformSplitPaymentLine
    {
        public decimal PayingAmount { get; set; }
        /// <summary>API value, e.g. CASH or CARD.</summary>
        public string PaymentMode { get; set; }
    }

    /// <summary>Result from split payment dialog: whether user confirmed or cancelled, and the payment list (full list if confirmed, charged-only if cancelled).</summary>
    public class SplitPaymentDialogResult
    {
        public bool Confirmed { get; set; }
        public List<SplitPaymentItem> Payments { get; set; }
    }

    /// <summary>One part of a split payment: method, amount, and PaymentModel-style cash/card properties.</summary>
    public class SplitPaymentItem
    {
        public ViewModels.PaymentMethod PaymentMethod { get; set; }
        public decimal Amount { get; set; }

        /// <summary>Cash tendered (for cash payments).</summary>
        public decimal Cash { get; set; }
        /// <summary>Change/balance (for cash payments).</summary>
        public decimal Balance { get; set; }
        /// <summary>Card auth code (for card payments).</summary>
        public string AuthCode { get; set; }
        /// <summary>Card PAN / last digits (for card payments).</summary>
        public string CardPan { get; set; }
        /// <summary>Card scheme (for card payments).</summary>
        public string CardScheme { get; set; }
        /// <summary>Transaction/reference id (for card payments).</summary>
        public string TransactionId { get; set; }

        /// <summary>Convert to order-place PaymentModel (API format).</summary>
        public PaymentModel ToPaymentModel()
        {
            var pmUpper = PaymentMethod.ToString().ToUpper();
            var isCard = pmUpper == "CARD" || pmUpper == "MANUALCARD";
            return new PaymentModel
            {
                PaymentMethod = isCard ? "CARD" : "CASH",
                PayingAmount = Amount,
                Cash = isCard ? 0m : Cash,
                Balance = isCard ? 0m : Balance,
                AuthCode = AuthCode ?? "",
                CardPan = CardPan ?? "",
                CardScheme = CardScheme ?? "",
                TransactionId = TransactionId ?? ""
            };
        }

        /// <summary>
        /// Convert split items to API payments while merging all cash splits into one CASH payment model.
        /// Card and manual-card splits remain separate to preserve transaction details.
        /// </summary>
        public static List<PaymentModel> ToPaymentModelsForOrder(IEnumerable<SplitPaymentItem> splitItems)
        {
            var items = splitItems?.ToList() ?? new List<SplitPaymentItem>();
            var result = new List<PaymentModel>();

            var cashItems = items
                .Where(i => i != null && string.Equals(i.PaymentMethod.ToString(), "Cash", System.StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (cashItems.Count > 0)
            {
                result.Add(new PaymentModel
                {
                    PaymentMethod = "CASH",
                    PayingAmount = cashItems.Sum(i => i.Amount),
                    Cash = cashItems.Sum(i => i.Cash),
                    Balance = cashItems.Sum(i => i.Balance),
                    AuthCode = "",
                    CardPan = "",
                    CardScheme = "",
                    TransactionId = ""
                });
            }

            var cardItems = items
                .Where(i => i != null && !string.Equals(i.PaymentMethod.ToString(), "Cash", System.StringComparison.OrdinalIgnoreCase));

            result.AddRange(cardItems.Select(i => i.ToPaymentModel()));
            return result;
        }

        /// <summary>True when every split is cash (no card/manual-card). Used to send complete-order as payment_mode CASH instead of SPLIT.</summary>
        public static bool IsAllCashSplits(IEnumerable<SplitPaymentItem> splitItems)
        {
            var items = splitItems?.Where(i => i != null).ToList() ?? new List<SplitPaymentItem>();
            if (items.Count == 0) return false;
            return items.All(i => string.Equals(i.PaymentMethod.ToString(), "Cash", System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Builds split_payment[] for NotifyCompleteOrderToDeliveryPlatformAsync: all cash splits merge into one CASH row (total paying amount); card/manual-card rows stay separate.</summary>
        public static List<DeliveryPlatformSplitPaymentLine> ToDeliveryPlatformSplitLines(IEnumerable<SplitPaymentItem> splitItems)
        {
            var items = (splitItems ?? Enumerable.Empty<SplitPaymentItem>()).Where(i => i != null).ToList();
            var result = new List<DeliveryPlatformSplitPaymentLine>();

            var cashItems = items
                .Where(i => string.Equals(i.PaymentMethod.ToString(), "Cash", System.StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (cashItems.Count > 0)
            {
                var cashTotal = cashItems.Sum(i => i.Amount);
                result.Add(new DeliveryPlatformSplitPaymentLine
                {
                    PayingAmount = cashTotal * 100,
                    PaymentMode = "CASH"
                });
            }

            foreach (var i in items.Where(i => !string.Equals(i.PaymentMethod.ToString(), "Cash", System.StringComparison.OrdinalIgnoreCase)))
            {
                var pmUpper = i.PaymentMethod.ToString().ToUpperInvariant();
                var apiMode = pmUpper == "CARD" || pmUpper == "MANUALCARD" ? "CARD" : "CASH";
                result.Add(new DeliveryPlatformSplitPaymentLine
                {
                    PayingAmount = i.Amount * 100,
                    PaymentMode = apiMode
                });
            }

            return result;
        }

        /// <summary>
        /// Receipt text for split payments: one method/amount per line (newline-separated) for vertical printing.
        /// Merges all cash splits into one CASH total; each card/manual-card split is shown as CARD.
        /// </summary>
        public static string FormatReceiptPaymentSummary(IEnumerable<SplitPaymentItem> splitItems, string currencySymbol)
        {
            var items = splitItems?.Where(i => i != null).ToList() ?? new List<SplitPaymentItem>();
            if (items.Count == 0) return null;
            var cur = string.IsNullOrWhiteSpace(currencySymbol) ? "£" : currencySymbol.Trim();
            var parts = new List<string>();

            var cashItems = items
                .Where(i => string.Equals(i.PaymentMethod.ToString(), "Cash", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (cashItems.Count > 0)
            {
                var sum = cashItems.Sum(i => i.Amount);
                parts.Add($"CASH {cur} {sum:F2}");
            }

            foreach (var i in items.Where(i => !string.Equals(i.PaymentMethod.ToString(), "Cash", StringComparison.OrdinalIgnoreCase)))
            {
                parts.Add($"CARD {cur} {i.Amount:F2}");
            }

            if (parts.Count == 0) return null;
            return string.Join(Environment.NewLine, parts);
        }
    }
}
