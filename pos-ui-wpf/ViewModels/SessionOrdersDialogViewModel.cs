using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using POS_UI.Models;
using POS_UI.Services;
using System.Windows;

namespace POS_UI.ViewModels
{
    public class SessionOrderDisplayItem
    {
        public string DisplayOrderId { get; set; }
        public string Status { get; set; }
    }

    public class SessionOrdersDialogViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly string _dialogHostId;
        private SessionOrdersResponse _sessionOrdersData;
        private bool _isLoading;
        private string _orderStatus;
        private string _paymentStatus;
        private string _sessionStatus;
        private string _totalAmount;
        private bool _showWarningMessage;

        public SessionOrdersDialogViewModel(int sessionId, OrderModel order, string dialogHostId = "RootDialog")
        {
            _apiService = new ApiService();
            _dialogHostId = dialogHostId ?? "RootDialog";
            SessionId = sessionId;
            Order = order;
            OrderDetails = new ObservableCollection<SessionOrderDisplayItem>();
            OrderDetails.CollectionChanged += (s, e) => 
            {
                UpdateReadyStatus();
                if (NextCommand is AsyncRelayCommand cmd)
                {
                    cmd.RaiseCanExecuteChanged();
                }
            };
            NextCommand = new AsyncRelayCommand(OnNextAsync, () => !IsLoading && AreAllOrdersReady);
            CloseCommand = DialogHost.CloseDialogCommand;
            
            _orderStatus = Order?.ApiStatus ?? "UNKNOWN";
            _paymentStatus = "LOADING...";
            _sessionStatus = "LOADING...";
            _totalAmount = "0.00";
            
            LoadSessionOrdersAsync();
        }

        public int SessionId { get; }
        public OrderModel Order { get; }

        public string OrderStatus
        {
            get => _orderStatus;
            set { _orderStatus = value; OnPropertyChanged(nameof(OrderStatus)); }
        }

        public string PaymentStatus
        {
            get => _paymentStatus;
            set { _paymentStatus = value; OnPropertyChanged(nameof(PaymentStatus)); OnPropertyChanged(nameof(NextButtonText)); }
        }

        /// <summary>Shows "Complete" when session payment status is PAID, otherwise "Next".</summary>
        public string NextButtonText => string.Equals(PaymentStatus?.Trim(), "PAID", StringComparison.OrdinalIgnoreCase) ? "Complete" : "Next";

        public string SessionStatus
        {
            get => _sessionStatus;
            set { _sessionStatus = value; OnPropertyChanged(nameof(SessionStatus)); }
        }

        public ObservableCollection<SessionOrderDisplayItem> OrderDetails { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set 
            { 
                _isLoading = value; 
                OnPropertyChanged(nameof(IsLoading));
                // Notify command to re-evaluate CanExecute
                if (NextCommand is AsyncRelayCommand cmd)
                {
                    cmd.RaiseCanExecuteChanged();
                }
            }
        }

        public string TotalAmount
        {
            get => _totalAmount;
            set { _totalAmount = value; OnPropertyChanged(nameof(TotalAmount)); }
        }

        public bool ShowWarningMessage
        {
            get => _showWarningMessage;
            set { _showWarningMessage = value; OnPropertyChanged(nameof(ShowWarningMessage)); }
        }

        public bool AreAllOrdersReady
        {
            get
            {
                if (OrderDetails == null || OrderDetails.Count == 0)
                    return false;

                return OrderDetails.All(order => 
                    !string.IsNullOrEmpty(order.Status) && 
                    (order.Status.Equals("READY_FOR_PICKUP", StringComparison.OrdinalIgnoreCase) ||
                     order.Status.Equals("Ready for pickup", StringComparison.OrdinalIgnoreCase) ||
                     order.Status.Equals("READY", StringComparison.OrdinalIgnoreCase) ||
                     order.Status.Equals("SERVED", StringComparison.OrdinalIgnoreCase)
                     ));
            }
        }

        public ICommand NextCommand { get; }
        public ICommand CloseCommand { get; }

        private async void LoadSessionOrdersAsync()
        {
            try
            {
                IsLoading = true;
                var response = await _apiService.GetSessionOrdersAsync(SessionId);
                _sessionOrdersData = response;

                if (response?.Data != null)
                {
                    // Update session status
                    SessionStatus = response.Data.SessionStatus ?? "UNKNOWN";
                    
                    // Update total amount
                    if (!string.IsNullOrEmpty(response.Data.TotalAmount))
                    {
                        TotalAmount = response.Data.TotalAmount;
                    }

                    // Update order status from data
                    if (!string.IsNullOrEmpty(response.Data.Status))
                    {
                        OrderStatus = response.Data.Status;
                    }

                    // Update payment status from data Status
                    if (!string.IsNullOrEmpty(response.Data.PaymentStatus))
                    {
                        PaymentStatus = response.Data.PaymentStatus;
                    }

                    // Populate order details
                    if (response.Data.OrderDetails != null)
                    {
                        OrderDetails.Clear();
                        foreach (var detail in response.Data.OrderDetails)
                        {
                            OrderDetails.Add(new SessionOrderDisplayItem
                            {
                                DisplayOrderId = detail.DisplayOrderId ?? "N/A",
                                Status = detail.Status ?? "UNKNOWN"
                            });
                        }
                        
                        // Check if all orders are ready and update warning message
                        UpdateReadyStatus();
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle error - could show error message
                System.Diagnostics.Debug.WriteLine($"Error loading session orders: {ex.Message}");
                MessageBox.Show($"Error loading session orders: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                SessionStatus = "ERROR";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateReadyStatus()
        {
            ShowWarningMessage = !AreAllOrdersReady;
            OnPropertyChanged(nameof(AreAllOrdersReady));
            if (NextCommand is AsyncRelayCommand cmd)
            {
                cmd.RaiseCanExecuteChanged();
            }
        }

        private async Task OnNextAsync()
        {
            // Check if all orders are ready before proceeding
            if (!AreAllOrdersReady)
            {
                ShowWarningMessage = true;
                return;
            }

            if (Order == null) return;

            // Close the SessionOrdersDialog first to avoid nested hosts
            DialogHost.CloseDialogCommand.Execute(null, null);
            await Task.Delay(150);

            // Use shared completion logic; fromSessionOrdersDialog=true so we open CheckoutDialog instead of re-showing this dialog
            await KitchenViewModel.MoveToCompletedStatic(Order, fromSessionOrdersDialog: true, dialogHostId: _dialogHostId);
        }
    }
}

