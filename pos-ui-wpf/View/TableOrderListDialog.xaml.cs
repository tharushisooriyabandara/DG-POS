using System.Windows.Controls;

namespace POS_UI.View
{
    /// <summary>
    /// User control dialog that shows a table name and id in the header and a list of orders
    /// (DisplayOrderId, total amount, View button) for that table.
    /// </summary>
    public partial class TableOrderListDialog : UserControl
    {
        public TableOrderListDialog()
        {
            InitializeComponent();
        }
    }
}
