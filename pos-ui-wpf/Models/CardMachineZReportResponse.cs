namespace POS_UI.Models
{
	public class CardMachineZReportResponse
	{
		public int cashbackAmount { get; set; }
		public int cashbackCount { get; set; }
		public int completionAmount { get; set; }
		public int completionCount { get; set; }
		public int gratuityAmount { get; set; }
		public int gratuityCount { get; set; }
		public int penniesAmount { get; set; }
		public int penniesCount { get; set; }
		public int refundAmount { get; set; }
		public int refundCount { get; set; }
		public bool reportResponse { get; set; }
		public string reportType { get; set; }
		public int saleAmount { get; set; }
		public int saleCount { get; set; }
	}
}


