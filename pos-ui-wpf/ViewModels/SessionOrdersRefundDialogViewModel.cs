using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using POS_UI.Models;
using POS_UI.Services;

namespace POS_UI.ViewModels
{
    public class SessionRefundOrderDisplayItem
    {
        public string DisplayOrderId { get; set; }
        public string Status { get; set; }
    }

    public class SessionOrdersRefundDialogViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly string _dialogHostId;
        private string _orderStatus;
        private string _paymentStatus;
        private string _sessionStatus;
        private string _totalAmount;
        private bool _isLoading;

        public SessionOrdersRefundDialogViewModel(
            int sessionId,
            OrderModel order,
            string dialogHostId = "RootDialog",
            KitchenOrderDetailsDialogViewModel.DialogMode dialogMode = KitchenOrderDetailsDialogViewModel.DialogMode.Kitchen)
        {
            _apiService = new ApiService();
            _dialogHostId = dialogHostId ?? "RootDialog";
            SessionId = sessionId;
            Order = order;
            DialogMode = dialogMode;

            OrderStatus = Order?.ApiStatus ?? "UNKNOWN";
            PaymentStatus = "LOADING...";
            SessionStatus = "LOADING...";
            TotalAmount = "0.00";

            OrderDetails = new ObservableCollection<SessionRefundOrderDisplayItem>();
            RefundCommand = new RelayCommand(() => _ = OnRefundAsync(), () => !IsLoading);
            CloseCommand = DialogHost.CloseDialogCommand;

            _ = LoadSessionOrdersAsync();
        }

        public int SessionId { get; }
        public OrderModel Order { get; }
        public KitchenOrderDetailsDialogViewModel.DialogMode DialogMode { get; }
        public ObservableCollection<SessionRefundOrderDisplayItem> OrderDetails { get; }
        public ICommand RefundCommand { get; }
        public ICommand CloseCommand { get; }
        public Action OnRefundRequested { get; set; }

        public string PaymentMode => Order?.PaymentMode ?? "";

        public string OrderStatus
        {
            get => _orderStatus;
            set { _orderStatus = value; OnPropertyChanged(); }
        }

        public string PaymentStatus
        {
            get
            {
                if (Order == null) return _paymentStatus ?? "";
                /*var total = Order.ApiTotal ?? 0m;
                var isUnpaid = string.Equals(Order.PaymentStatus, "UNPAID", StringComparison.OrdinalIgnoreCase);
                var balance = Order.RefundBalance;
                if (total > 0 && balance <= 0 && !isUnpaid)
                    return "Refunded";
                if (total > 0 && balance < total && !isUnpaid)
                    return "Partially Refunded";*/
                var refundStatus = Order.RefundStatus;
                return string.IsNullOrWhiteSpace(refundStatus) ? Order.PaymentStatus ?? "" : refundStatus;
            }
            set { _paymentStatus = value; OnPropertyChanged(); }
        }

        public string SessionStatus
        {
            get => _sessionStatus;
            set { _sessionStatus = value; OnPropertyChanged(); }
        }

        public string TotalAmount
        {
            get => _totalAmount;
            set { _totalAmount = value; OnPropertyChanged(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                if (RefundCommand is RelayCommand cmd)
                {
                    cmd.RaiseCanExecuteChanged();
                }
            }
        }

        private async Task LoadSessionOrdersAsync()
        {
            try
            {
                IsLoading = true;
                var response = await _apiService.GetSessionOrdersAsync(SessionId);
                if (response?.Data == null)
                {
                    return;
                }

                SessionStatus = response.Data.SessionStatus ?? "UNKNOWN";
                OrderStatus = response.Data.Status ?? OrderStatus;
                PaymentStatus = response.Data.PaymentStatus ?? PaymentStatus;
                TotalAmount = !string.IsNullOrWhiteSpace(response.Data.TotalAmount) ? response.Data.TotalAmount : "0.00";

                OrderDetails.Clear();
                if (response.Data.OrderDetails != null)
                {
                    foreach (var detail in response.Data.OrderDetails)
                    {
                        OrderDetails.Add(new SessionRefundOrderDisplayItem
                        {
                            DisplayOrderId = detail.DisplayOrderId ?? "N/A",
                            Status = detail.Status ?? "UNKNOWN"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading session orders: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task OnRefundAsync()
        {
            DialogHost.CloseDialogCommand.Execute(null, null);
            await Task.Delay(150);
            OnRefundRequested?.Invoke();
        }
    }
}
