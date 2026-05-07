using System;

namespace POS_UI.Models
{
	public class PosStatsModel
	{
		public int TotalOrders { get; set; }
		public int TakeawayOrders { get; set; }
		public int DeliveryOrders { get; set; }
		public int DineInOrders { get; set; }

		public decimal GrossRevenue { get; set; }
		public decimal NetRevenue { get; set; }
		public decimal TakeawayRevenue { get; set; }
		public decimal DeliveryRevenue { get; set; }
		public decimal DineInRevenue { get; set; }

        public int CashOrders { get; set; }
		public int CardOrders { get; set; }
		public decimal CashRevenue { get; set; }
		public decimal CardRevenue { get; set; }

		public int CancelledOrders { get; set; }
		public decimal CancelledRevenue { get; set; }

		public TaxSummaryModel Tax { get; set; }
	}
}


