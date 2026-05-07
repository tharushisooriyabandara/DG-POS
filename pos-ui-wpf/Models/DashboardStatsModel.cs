using System;
using System.Collections.Generic;

namespace POS_UI.Models
{
	public class DashboardStatsModel
	{
		public int TotalOrders { get; set; }
		public decimal Revenue { get; set; }
		public int AcceptedOrders { get; set; }
		public int DeclinedOrders { get; set; }
		public int CompletedOrders { get; set; }
		public int ReadyForPickupOrders { get; set; }
		public int CancelledOrders { get; set; }
		public BestPlatformModel BestPlatform { get; set; }
		
		public decimal NetRevenue { get; set; }
		
    }
	
	public class BestPlatformModel
	{
		public string Name { get; set; }
		public string Url { get; set; }
		public int Count { get; set; }
	}
}
