using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using POS_UI.Models;
using System.Threading.Tasks;
using System.Windows;
using MaterialDesignThemes.Wpf;
using POS_UI.View;

namespace POS_UI.ViewModels
{
    public class TransactionItemViewModel : BaseViewModel
    {
        private bool _isExpanded;
        public OrderTransactionModel Transaction { get; set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public string TransactionType => Transaction?.TransactionType ?? "";
        public decimal TransactionAmount => Transaction?.TransactionAmount ?? 0m;
        public string TransactionMode => Transaction?.TransactionMode ?? "";
        public DateTime CreatedAt => Transaction?.CreatedAt ?? DateTime.MinValue;
        public string Reason => Transaction?.Reason ?? "";
        public bool HasReason => !string.IsNullOrWhiteSpace(Reason);
    }

    public class TransactionDialogViewModel : BaseViewModel
    {
        private ObservableCollection<TransactionItemViewModel> _transactionItems;
        private OrderModel _order;
        private KitchenOrderDetailsDialogViewModel.DialogMode _dialogMode;

        public ObservableCollection<TransactionItemViewModel> TransactionItems
        {
            get => _transactionItems;
            set
            {
                _transactionItems = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasTransactions));
                OnPropertyChanged(nameof(HasNoTransactions));
            }
        }

        public bool HasTransactions => TransactionItems != null && TransactionItems.Count > 0;
        public bool HasNoTransactions => !HasTransactions;

        public ICommand ToggleExpandCommand { get; }
        public ICommand BackToOrderCommand { get; }

        
        // Order information
        public OrderModel Order
        {
            get => _order;
            set
            {
                _order = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OrderNumber));
                OnPropertyChanged(nameof(TableButtonText));
                OnPropertyChanged(nameof(ShippingMethodText));
                OnPropertyChanged(nameof(DeliveryPlatformName));
                OnPropertyChanged(nameof(PlatformLogo));
                OnPropertyChanged(nameof(PaymentStatus));
                OnPropertyChanged(nameof(PaymentMode));
            }
        }

        public string OrderNumber => Order?.OrderNumber ?? Order?.DisplayOrderId ?? "";
        
        public string TableButtonText
        {
            get
            {
                if (Order == null) return string.Empty;

                // Check if platform_id is 8, then display table name
                if (Order.PlatformId == 8 || Order.PlatformName == "TABLE_ORDER")
                {
                    return !string.IsNullOrWhiteSpace(Order.TableName) ? $"Table {Order.TableName}" : $"Time {Order.DeliveryDateTime.Value.ToUniversalTime().ToString("HH:mm")}";
                }

                return Order.OrderType switch
                {
                    OrderType.DineIn => Order.TableNumber.HasValue ? $"Table {Order.TableName}" : "Tableee",
                    OrderType.TakeAway or OrderType.Delivery => Order.DeliveryDateTime.HasValue 
                        ? $"Time {Order.DeliveryDateTime.Value.ToUniversalTime().ToString("HH:mm")}" 
                        : "00:00",
                    _ => "00:01"
                };
            }
        }

        public string ShippingMethodText
        {
            get
            {
                if (Order == null) return "N/A";
                
                // Check if platform is Table Order (PlatformId == 8 or PlatformId2 == 8)
                if (Order.PlatformId == 8 || Order.PlatformId2 == 8)
                {
                    return string.IsNullOrWhiteSpace(Order.TableOrderMethod) ? "N/A" : Order.TableOrderMethod.ToUpperInvariant();
                }
                
                return string.IsNullOrWhiteSpace(Order.ShippingMethod) ? "N/A" : Order.ShippingMethod;
            }
        }

        public string DeliveryPlatformName => Order?.DeliveryPlatfornName ?? "";
        public string PlatformLogo => Order?.PlatformLogo;
        public string PaymentStatus
        {
            get
            {
                if (Order == null) return "";
                /*var total = Order.ApiTotal ?? 0m;
                var balance = Order.RefundBalance;
                var isUnpaid = string.Equals(Order.PaymentStatus,"UNPAID",StringComparison.OrdinalIgnoreCase);
                if (total > 0 && balance <= 0 && !isUnpaid)
                    return "Refunded";
                if (total > 0 && balance < total && !isUnpaid)
                    return "Partially Refunded";*/
                var refundStatus = Order.RefundStatus;
                return string.IsNullOrWhiteSpace(refundStatus) ? Order.PaymentStatus ?? "" : refundStatus;
            }
        }
        public string PaymentMode => Order?.PaymentMode ?? "";

        public TransactionDialogViewModel()
        {
            TransactionItems = new ObservableCollection<TransactionItemViewModel>();
            ToggleExpandCommand = new RelayCommand<TransactionItemViewModel>(ToggleExpand);
            BackToOrderCommand = new RelayCommand(() => _ = BackToOrderAsync());
        }

        public TransactionDialogViewModel(System.Collections.Generic.List<OrderTransactionModel> transactions)
        {
             
            TransactionItems = new ObservableCollection<TransactionItemViewModel>();
            ToggleExpandCommand = new RelayCommand<TransactionItemViewModel>(ToggleExpand);
            BackToOrderCommand = new RelayCommand(() => _ = BackToOrderAsync());
            if (transactions != null)
            {
                foreach (var transaction in transactions)
                {
                    TransactionItems.Add(new TransactionItemViewModel { Transaction = transaction });
                }
            }
        }

        public TransactionDialogViewModel(OrderModel order, System.Collections.Generic.List<OrderTransactionModel> transactions, KitchenOrderDetailsDialogViewModel.DialogMode dialogMode = KitchenOrderDetailsDialogViewModel.DialogMode.Kitchen)
        {
            Order = order;
            _dialogMode = dialogMode;
            TransactionItems = new ObservableCollection<TransactionItemViewModel>();
            ToggleExpandCommand = new RelayCommand<TransactionItemViewModel>(ToggleExpand);
            BackToOrderCommand = new RelayCommand(() => _ = BackToOrderAsync());
            if (transactions != null)
            {
                foreach (var transaction in transactions)
                {
                    TransactionItems.Add(new TransactionItemViewModel { Transaction = transaction });
                }
            }
        }

        private void ToggleExpand(TransactionItemViewModel item)
        {
            if (item != null)
            {
                item.IsExpanded = !item.IsExpanded;
            }
        }

        private async Task BackToOrderAsync()
        {
            try
            {
                if (Order == null || Order.ApiId <= 0)
                {
                    return;
                }

                // Determine which dialog host identifier to use based on dialog mode
                string dialogHostId = _dialogMode == KitchenOrderDetailsDialogViewModel.DialogMode.Tables ? "RootDialogHost" : "RootDialog";

                // Close the transaction dialog first
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (DialogHost.IsDialogOpen(dialogHostId))
                        {
                            DialogHost.Close(dialogHostId, null);
                        }
                    }
                    catch { }
                });

                // Wait until dialog is actually closed
                while (DialogHost.IsDialogOpen(dialogHostId))
                {
                    await Task.Delay(50);
                }
                await Task.Delay(150); // Additional delay for animation to complete

                // Open KitchenOrderDetailsDialog with the correct mode
                var kitchenDialogViewModel = new KitchenOrderDetailsDialogViewModel(Order.ApiId, _dialogMode);
                var kitchenDialog = new KitchenOrderDetailsDialog { DataContext = kitchenDialogViewModel };

                await DialogHost.Show(kitchenDialog, dialogHostId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening order details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
