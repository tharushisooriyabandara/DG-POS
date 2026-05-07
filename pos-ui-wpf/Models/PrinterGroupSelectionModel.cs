using System.Collections.Generic;

namespace POS_UI.Models
{
    public class PrinterGroupSelectionModel
    {
        public int PrinterGroupId { get; set; }
        public string PrinterGroupName { get; set; }
        public List<string> SelectedPrinterNames { get; set; } = new List<string>();
    }
}
