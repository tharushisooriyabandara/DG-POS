using System;
using System.Collections.Generic;

namespace POS_UI.Models
{
	public class PlatformSummaryModel
	{
		public string Name { get; set; }
		public int OrderCount { get; set; }
		public decimal Revenue { get; set; }
		public Dictionary<string, decimal> Metrics { get; set; } = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
		public string BrandName { get; set; }
	}
}


