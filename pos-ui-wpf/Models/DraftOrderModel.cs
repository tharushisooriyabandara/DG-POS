using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace POS_UI.Models
{
    public class DraftOrderModel : INotifyPropertyChanged
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string CustomerName { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string OrderType { get; set; } // Take Away, Delivery, Dine In
        public string TableNumber { get; set; } // null for non-table orders
        public string TableName { get; set; } // e.g., "T1" or descriptive label from API
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
        
        // Additional order details
        public string Note { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal DiscountPercent { get; set; }
        public string DiscountModeApplied { get; set; } = "percentage"; // "percentage" or "value"
        public string DiscountDescription { get; set; }
        public string CouponCode { get; set; }
        public decimal CouponAmount { get; set; }
        public string CouponDescription { get; set; }
        public decimal DeliveryCharge { get; set; }
        public string CustomerPhone { get; set; }
        
        // Scheduled time for delivery/takeaway orders
        public DateTime? ScheduledTime { get; set; }
        
        // Modifier information for each item
        public Dictionary<string, Dictionary<int, List<string>>> ItemModifiers { get; set; } = new Dictionary<string, Dictionary<int, List<string>>>();
        public Dictionary<string, Dictionary<string, List<string>>> ItemNestedModifiers { get; set; } = new Dictionary<string, Dictionary<string, List<string>>>();
        
        // Removed optional shop fees (to restore when loading draft)
        public List<int> RemovedShopFeeIds { get; set; } = new List<int>();
        public List<string> RemovedShopFeeNames { get; set; } = new List<string>();

        public string ElapsedTimeText
        {
            get
            {
                var span = DateTime.Now - CreatedAt;
                if (span.TotalMinutes < 1)
                    return "Just now";
                if (span.TotalMinutes < 60)
                {
                    var minutes = (int)span.TotalMinutes;
                    return $"{minutes} {(minutes == 1 ? "min" : "mins")} ago";
                }
                if (span.TotalHours < 24)
                {
                    var hours = (int)span.TotalHours;
                    return $"{hours} {(hours == 1 ? "hour" : "hours")} ago";
                }
                var days = (int)span.TotalDays;
                return $"{days} {(days == 1 ? "day" : "days")} ago";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnElapsedTimeChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ElapsedTimeText)));
        }

        // Helper label to display beside customer name in drafts for dine-in
        public string DisplayTableLabel
        {
            get
            {
                if (!string.Equals(OrderType, "Dine In", StringComparison.OrdinalIgnoreCase)) return string.Empty;
                var label = !string.IsNullOrWhiteSpace(TableName) ? TableName : (!string.IsNullOrWhiteSpace(TableNumber) ? $"T{TableNumber}" : string.Empty);
                return string.IsNullOrWhiteSpace(label) ? string.Empty : $"{label}";
            }
        }
    }
} 