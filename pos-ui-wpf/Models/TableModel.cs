using System;
using System.Collections.Generic;

namespace POS_UI.Models
{
    public enum TableStatus
    {
        Available,
        Reserved,
        Drafted,
        Served,
        Unavailable
    }

    public class TableModel : System.ComponentModel.INotifyPropertyChanged
    {
        public int ApiId { get; set; } // API ID from the response
        public int TableNumber { get; set; }
        public string Name { get; set; } // Table name from API (e.g., "T1")
        public string Description { get; set; }
        public int SeatCount { get; set; }
        public int ShopId { get; set; }
        public int BrandId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        private TableStatus _status;
        public TableStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }
        
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }
        
        public decimal Amount { get; set; }

        //Table orderings ID 
        public int TableOrderingsId { get; set; }
        
        /// <summary>Session total amount when this table has a table order with a session. Set when loading tables.</summary>
        private decimal? _sessionTotalAmount;
        public decimal? SessionTotalAmount
        {
            get => _sessionTotalAmount;
            set { _sessionTotalAmount = value; OnPropertyChanged(nameof(SessionTotalAmount)); OnPropertyChanged(nameof(DisplayAmount)); }
        }

        // Order information if table has an active order
        public OrderModel Order { get; set; }
        
        // Helper property to get order amount if exists
        public decimal OrderAmount => Order?.DisplayTotal ?? 0m;

        /// <summary>Amount to show on the table button: session total when table order with session, otherwise order amount.</summary>
        public decimal DisplayAmount =>
            (Order?.IsTableOrder == true && Order?.OrderSessionId.HasValue == true && Order.OrderSessionId.Value > 0 && _sessionTotalAmount.HasValue)
                ? _sessionTotalAmount.Value
                : OrderAmount;
        
        // Helper property to get order number if exists
        public string OrderNumber => Order?.OrderNumber ?? "";
        
        // Helper property to get customer name if exists
        public string CustomerName => Order?.CustomerName ?? "";
        
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
} 