using System;
using System.Collections.Generic;

namespace POS_UI.Models
{
    public class ShiftModel
    {
        public int UserId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int OrderCount { get; set; }
        public decimal TotalCashAmount { get; set; }    
        public decimal TotalCardAmount { get; set; }
        public decimal TotalOrderAmount { get; set; }
        public List<ShiftDetailModel> ShiftDetails { get; set; } = new List<ShiftDetailModel>();
    }

    public class ShiftDetailModel
    {
        public int ShiftId { get; set; }
        public bool ActiveShift { get; set; }
        public DateTime LoginTime { get; set; }
        public DateTime? LogoutTime { get; set; }
        public string ShiftDuration { get; set; }
        public int OrderCount { get; set; }
        //public decimal TotalCardAmount { get; set; }
        //public decimal TotalOrderAmount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    // New model for shop shift info API response
    public class ShopShiftInfoModel
    {
        public int ShopId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int OrderCount { get; set; }
        public decimal TotalCashAmount { get; set; }
        public decimal TotalCardAmount { get; set; }
        public decimal TotalOrderAmount { get; set; }
        public List<ShopShiftDetailModel> ShiftDetails { get; set; } = new List<ShopShiftDetailModel>();
    }

    public class ShopShiftDetailModel
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public int OrderCount { get; set; }
        public decimal CashAmount { get; set; }
        public decimal CardAmount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
