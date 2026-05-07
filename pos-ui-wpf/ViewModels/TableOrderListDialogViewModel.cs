using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using POS_UI.Models;
using POS_UI.View;

namespace POS_UI.ViewModels
{
    /// <summary>
    /// Display item for a single order row in the table order list.
    /// </summary>
    public class TableOrderListItem : INotifyPropertyChanged
    {
        public string DisplayOrderId { get; set; }
        public decimal TotalAmount { get; set; }
        /// <summary>Order API id used to open order details.</summary>
        public int OrderApiId { get; set; }
        public int SessionId { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class TableOrderListDialogViewModel : BaseViewModel
    {
        private string _tableName;
        private int _tableId;
        private int _sessionId;
        private Func<Task> _reopenTableOrderListCallback;

        public TableOrderListDialogViewModel(string tableName, int tableId, ObservableCollection<TableOrderListItem> orders, int sessionId = 0)
        {
            _tableName = tableName ?? "";
            _tableId = tableId;
            _sessionId = sessionId;
            Orders = orders ?? new ObservableCollection<TableOrderListItem>();
            // Use explicit close so the dialog closes when shown via DialogHost.Show(..., "RootDialogHost")
            CloseCommand = new RelayCommand(() => DialogHost.Close("RootDialogHost", null));
            ViewOrderCommand = new RelayCommand<TableOrderListItem>(OnViewOrder);
        }

        public string TableName
        {
            get => _tableName;
            set { _tableName = value; OnPropertyChanged(nameof(TableName)); }
        }

        public int TableId
        {
            get => _tableId;
            set { _tableId = value; OnPropertyChanged(nameof(TableId)); }
        }

        /// <summary>Session ID for the table order session (displayed in header).</summary>
        public int SessionId
        {
            get => _sessionId;
            set { _sessionId = value; OnPropertyChanged(nameof(SessionId)); }
        }

        public ObservableCollection<TableOrderListItem> Orders { get; }

        public ICommand CloseCommand { get; }
        public ICommand ViewOrderCommand { get; }

        /// <summary>
        /// Sets a callback to run when the user closes KitchenOrderDetailsDialog so the Tables page can re-show this table order list dialog.
        /// </summary>
        public void SetReopenTableOrderListCallback(Func<Task> callback)
        {
            _reopenTableOrderListCallback = callback;
        }

        private async void OnViewOrder(TableOrderListItem item)
        {
            if (item == null || item.OrderApiId <= 0) return;

            DialogHost.Close("RootDialogHost", null);
            await Task.Delay(150);

            var dialogViewModel = new KitchenOrderDetailsDialogViewModel(item.OrderApiId, KitchenOrderDetailsDialogViewModel.DialogMode.Tables);
            var dialog = new KitchenOrderDetailsDialog { DataContext = dialogViewModel };
            await DialogHost.Show(dialog, "RootDialogHost");

            // Re-show the table order list dialog when user closes the order details dialog
            if (_reopenTableOrderListCallback != null && dialogViewModel.ShouldReopenTableOrderListAfterClose)
            {
                await _reopenTableOrderListCallback();
            }
        }

        /// <summary>
        /// Create a view model for a table, with a single order from the table model (current API returns one order per table).
        /// </summary>
        public static TableOrderListDialogViewModel FromTable(TableModel table)
        {
            var orders = new ObservableCollection<TableOrderListItem>();
            if (table?.Order != null)
            {
                orders.Add(new TableOrderListItem
                {
                    DisplayOrderId = !string.IsNullOrWhiteSpace(table.Order.DisplayOrderId) ? table.Order.DisplayOrderId : (table.Order.OrderNumber ?? table.Order.ApiId.ToString()),
                    TotalAmount = table.Order.DisplayTotal,
                    OrderApiId = table.Order.ApiId
                });
            }
            return new TableOrderListDialogViewModel(table?.Name ?? "Table", table?.ApiId ?? 0, orders);
        }

        /// <summary>
        /// Create a view model with custom table name/id and a list of orders (e.g. from a future API that returns multiple orders per table).
        /// </summary>
        public static TableOrderListDialogViewModel Create(string tableName, int tableId, System.Collections.Generic.IEnumerable<OrderModel> orders)
        {
            var list = new ObservableCollection<TableOrderListItem>();
            if (orders != null)
            {
                foreach (var o in orders)
                {
                    list.Add(new TableOrderListItem
                    {
                        DisplayOrderId = !string.IsNullOrWhiteSpace(o.DisplayOrderId) ? o.DisplayOrderId : (o.OrderNumber ?? o.ApiId.ToString()),
                        TotalAmount = o.DisplayTotal,
                        OrderApiId = o.ApiId
                    });
                }
            }
            return new TableOrderListDialogViewModel(tableName, tableId, list);
        }

        /// <summary>
        /// Create a view model from session orders response (e.g. when platform id is 8 and we loaded session orders via GetSessionOrdersAsync).
        /// Displays session id, order id and order total for each order in the session.
        /// </summary>
        public static TableOrderListDialogViewModel CreateFromSessionOrders(string tableName, int tableId, SessionOrdersResponse response, int sessionId = 0)
        {
            var list = new ObservableCollection<TableOrderListItem>();
            if (response?.Data?.OrderDetails != null)
            {
                foreach (var d in response.Data.OrderDetails)
                {
                    list.Add(new TableOrderListItem
                    {
                        DisplayOrderId = !string.IsNullOrWhiteSpace(d.DisplayOrderId) ? d.DisplayOrderId : "N/A",
                        TotalAmount = d.TotalAmount,
                        OrderApiId = d.OrderApiId
                    });
                }
            }
            return new TableOrderListDialogViewModel(tableName, tableId, list, sessionId);
        }
    }
}
