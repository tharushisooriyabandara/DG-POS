using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace POS_UI.Models
{
    public class DineInOrderModel
    {
        public string DisplayOrderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModified { get; set; }
        public string OrderStatus { get; set; } = "ACTIVE"; // ACTIVE, COMPLETED, CANCELLED
        public decimal TotalAmount { get; set; }
        public string Notes { get; set; }
        public List<DineInOrderItemModel> Items { get; set; } = new List<DineInOrderItemModel>();
        public string TableNumber { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
    }

    public class DineInOrderItemModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string ItemStatus { get; set; } = "QUEUE"; // QUEUE, PREPARE, READY, SERVED
        public string Notes { get; set; }
        public List<DineInOrderModifierModel> Modifiers { get; set; } = new List<DineInOrderModifierModel>();
        public DateTime ItemCreatedAt { get; set; }
        public DateTime ItemLastModified { get; set; }
        public bool IsNewItem { get; set; } = true; // Track if this is a newly added item
    }

    public class DineInOrderModifierModel
    {
        public int ModifierId { get; set; }
        public string ModifierName { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }

    public static class DineInOrderStatus
    {
        public const string QUEUE = "QUEUE";
        public const string PREPARE = "PREPARE";
        public const string READY = "READY";
        public const string SERVED = "SERVED";
    }

    public static class DineInOrderItemStatus
    {
        public const string QUEUE = "QUEUE";
        public const string PREPARE = "PREPARE";
        public const string READY = "READY";
        public const string SERVED = "SERVED";
    }
}
