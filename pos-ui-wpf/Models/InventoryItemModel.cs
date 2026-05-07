using System;

namespace POS_UI.Models
{
    public class InventoryItemModel
    {
        public string Name { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public DateTime LastUpdated { get; set; }
        public string QuantityWithUnit => $"{Quantity} {Unit}";
        public string LastUpdatedDisplay => $"Last Updated {GetTimeAgo(LastUpdated)} ago";

        private string GetTimeAgo(DateTime dateTime)
        {
            var span = DateTime.Now - dateTime;
            if (span.TotalDays >= 1)
                return $"{(int)span.TotalDays}d";
            if (span.TotalHours >= 1)
                return $"{(int)span.TotalHours}h";
            if (span.TotalMinutes >= 1)
                return $"{(int)span.TotalMinutes}m";
            return $"{(int)span.TotalSeconds}s";
        }
    }
} 