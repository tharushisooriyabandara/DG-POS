using POS_UI.Models;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using POS_UI.Services;
using System.Threading.Tasks;
using System;
using System.Windows;
using POS_UI.View;
using POS_UI.ViewModels;
using MaterialDesignThemes.Wpf;

namespace POS_UI.ViewModels
{
    public class KitchenViewModel : BaseViewModel
    {
        private readonly CartService _cartService = CartService.Instance;
        private readonly ApiService _apiService = new ApiService();

        private ObservableCollection<OrderModel> _queueOrders;
        private List<OrderModel> _allQueueOrders;
        private List<OrderModel> _allPreparingOrders;
        private List<OrderModel> _allReadyOrders;
        private List<OrderModel> _allServedOrders;
        private bool _isLoading;
        private readonly Dictionary<int, bool> _orderLoadingStates = new Dictionary<int, bool>();

        public ObservableCollection<OrderItem> OrderItems => _cartService.OrderItems;
        public decimal Total => _cartService.Total;
        public decimal SubTotal => _cartService.SubTotal;
        public int ItemCount => _cartService.ItemCount;
        public string OrderType => _cartService.OrderType;
        public string CustomerName => _cartService.CustomerName;
        public string CustomerPhone => _cartService.CustomerPhone;
        public int? TableNumber => _cartService.TableNumber;
        public string Note => _cartService.Note;
        public bool HasNote => _cartService.HasNote;
        public decimal DiscountAmount => _cartService.DiscountAmount;
        public string DiscountDescription => _cartService.DiscountDescription;
        public string CouponCode => _cartService.CouponCode;
        public bool HasCoupon => _cartService.HasCoupon;
        public decimal CouponAmount => _cartService.CouponAmount;
        public string CouponDescription => _cartService.CouponDescription;

        public ObservableCollection<OrderModel> QueueOrders
        {
            get => _queueOrders;
            set { _queueOrders = value; OnPropertyChanged(nameof(QueueOrders)); }
        }
        private ObservableCollection<OrderModel> _preparingOrders;
        public ObservableCollection<OrderModel> PreparingOrders
        {
            get => _preparingOrders;
            set { _preparingOrders = value; OnPropertyChanged(nameof(PreparingOrders)); }
        }
        private ObservableCollection<OrderModel> _readyOrders;
        public ObservableCollection<OrderModel> ReadyOrders
        {
            get => _readyOrders;
            set { _readyOrders = value; OnPropertyChanged(nameof(ReadyOrders)); }
        }
        private ObservableCollection<OrderModel> _servedOrders;
        public ObservableCollection<OrderModel> ServedOrders
        {
            get => _servedOrders;
            set { _servedOrders = value; OnPropertyChanged(nameof(ServedOrders)); }
        }
        public ObservableCollection<string> Platforms { get; set; }
        private string _selectedPlatform;
        public string SelectedPlatform
        {
            get => _selectedPlatform;
            set
            {
                if (_selectedPlatform != value)
                {
                    _selectedPlatform = value;
                    OnPropertyChanged(nameof(SelectedPlatform));
                    FilterOrders();
                }
            }
        }
        public string CurrentPage { get; set; }
        public ICommand MoveToPreparingCommand { get; }
        public ICommand MoveToQueueCommand { get; }
        public ICommand MoveToReadyCommand { get; }
        public ICommand MoveToServedCommand { get; }
        public ICommand MoveToDeliveredCommand { get; }
        public ICommand MoveToCompletedCommand { get; }
        public ICommand MoveToFinishedCommand { get; }
        public ICommand RefreshOrdersCommand { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
        }

        public bool IsOrderLoading(int orderId)
        {
            return _orderLoadingStates.ContainsKey(orderId) && _orderLoadingStates[orderId];
        }

        private void SetOrderLoading(int orderId, bool isLoading)
        {
            _orderLoadingStates[orderId] = isLoading;
            OnPropertyChanged($"IsOrderLoading_{orderId}");
            // Notify that the overall loading state might have changed
            OnPropertyChanged(nameof(HasAnyOrderLoading));
        }

        public bool HasAnyOrderLoading
        {
            get
            {
                var allOrders = QueueOrders.Cast<object>()
                    .Concat(PreparingOrders.Cast<object>())
                    .Concat(ReadyOrders.Cast<object>())
                    .Concat(ServedOrders.Cast<object>())
                    .OfType<OrderModel>();

                return allOrders.Any(order => IsOrderLoading(order.ApiId));
            }
        }

        private static void SafeRemove(ObservableCollection<OrderModel> col, int apiId)
        {
            for (int i = col.Count - 1; i >= 0; i--)
                if (col[i].ApiId == apiId) col.RemoveAt(i);
        }

        private static void SafeRemove(List<OrderModel> list, int apiId)
        {
            list.RemoveAll(o => o.ApiId == apiId);
        }

        private static void SafeAdd(ObservableCollection<OrderModel> col, OrderModel order)
        {
            if (!col.Any(o => o.ApiId == order.ApiId))
                col.Add(order);
        }

        private static void SafeAdd(List<OrderModel> list, OrderModel order)
        {
            if (!list.Any(o => o.ApiId == order.ApiId))
                list.Add(order);
        }

        private OrderType? _selectedOrderType;
        public OrderType? SelectedOrderType
        {
            get => _selectedOrderType;
            set { _selectedOrderType = value; OnPropertyChanged(nameof(SelectedOrderType)); FilterOrders(); }
        }

        public KitchenViewModel()
        {
            CurrentPage = "Kitchen";
            Platforms = new ObservableCollection<string> { "All Platforms", "Deliveroo", "UberEats", "Webshop", "Table Order", "DG POS" };

            _cartService.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
            
            // Initialize collections
            QueueOrders = new ObservableCollection<OrderModel>();
            PreparingOrders = new ObservableCollection<OrderModel>();
            ReadyOrders = new ObservableCollection<OrderModel>();
            ServedOrders = new ObservableCollection<OrderModel>();
            
            // Load orders from API
            _ = LoadOrdersFromApiAsync();

            // Initialize all orders lists
            _allQueueOrders = new List<OrderModel>();
            _allPreparingOrders = new List<OrderModel>();
            _allReadyOrders = new List<OrderModel>();
            _allServedOrders = new List<OrderModel>();

            // Now set the platform (this will trigger filtering)
            SelectedPlatform = Platforms[0];
            MoveToPreparingCommand = new RelayCommand<OrderModel>(MoveToPreparing);
            MoveToQueueCommand = new RelayCommand<OrderModel>(MoveToQueue);
            MoveToReadyCommand = new RelayCommand<OrderModel>(MoveToReady);
            MoveToServedCommand = new RelayCommand<OrderModel>(MoveToServed, CanMoveToServed);
            MoveToDeliveredCommand = new RelayCommand<OrderModel>(MoveToDelivered);
            MoveToCompletedCommand = new RelayCommand<OrderModel>(MoveToCompleted);
            MoveToFinishedCommand = new RelayCommand<OrderModel>(MoveToFinished);
            RefreshOrdersCommand = new RelayCommand(async () => await RefreshOrdersAsync());

            // Listen for status changes coming from dialogs
            GlobalDataService.Instance.OrderStatusChanged += OnExternalOrderStatusChanged;
            // Refresh orders when requested (e.g. after completing unpaid table order from checkout)
            GlobalDataService.Instance.KitchenRefreshRequested += OnKitchenRefreshRequested;
        }

        private async void OnKitchenRefreshRequested()
        {
            try { await RefreshOrdersAsync(); } catch { }
        }

        private void FilterOrders()
        {
            // When platform changes, reload orders from API with the new platform filter
            _ = LoadOrdersFromApiAsync();
        }

        private async void MoveToPreparing(OrderModel order)
        {
            if (order == null || IsOrderLoading(order.ApiId)) return;
            
            try
            {
                SetOrderLoading(order.ApiId, true);
                if (order.PlatformId == 1 || order.PlatformId == 2 || order.PlatformId == 6 || order.PlatformId == 8)
                {
                    var remoteOrderId = !string.IsNullOrWhiteSpace(order.RemoteOrderId)
                        ? order.RemoteOrderId
                        : (!string.IsNullOrWhiteSpace(order.DisplayOrderId) ? order.DisplayOrderId : (order.OrderNumber ?? order.ApiId.ToString()));
                    var result = await _apiService.NotifyPreparingToDeliveryPlatformAsync(remoteOrderId);
                    if (!result.IsSuccess)
                    {
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to Notify Delivery Platform", $"Failed to notify delivery platform: {result.ErrorMessage}");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                        return;
                    }
                }
                else
                {
                    await _apiService.UpdateOrderStatusAsync(order.ApiId, "PREPARING");
                }

                SafeRemove(QueueOrders, order.ApiId);
                SafeRemove(ReadyOrders, order.ApiId);
                SafeAdd(PreparingOrders, order);
                SafeRemove(_allQueueOrders, order.ApiId);
                SafeRemove(_allReadyOrders, order.ApiId);
                SafeAdd(_allPreparingOrders, order);

                try
                {
                    var displayKey = order?.OrderNumber ?? order?.DisplayOrderId;
                    if (!string.IsNullOrWhiteSpace(displayKey))
                    {
                        await POS_UI.Services.DineInOrderService.Instance.SeedOrUpdateFromOrderModelAsync(order, POS_UI.Models.DineInOrderItemStatus.PREPARE);
                    }
                }
                catch { }

                await RefreshPreparingOrdersAsync();
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error", "Error updating order status");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            finally
            {
                SetOrderLoading(order.ApiId, false);
            }
        }

        private async void MoveToQueue(OrderModel order)
        {
            if (order == null || IsOrderLoading(order.ApiId)) return;
            
            try
            {
                SetOrderLoading(order.ApiId, true);
                await _apiService.UpdateOrderStatusAsync(order.ApiId, "QUEUE");
                
                SafeRemove(PreparingOrders, order.ApiId);
                SafeAdd(QueueOrders, order);
                SafeRemove(_allPreparingOrders, order.ApiId);
                SafeAdd(_allQueueOrders, order);
            }
            catch (Exception ex)
            {
               var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error", "Error updating order status");
               var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
               MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            finally
            {
                SetOrderLoading(order.ApiId, false);
            }
        }

        private async void MoveToReady(OrderModel order)
        {
            if (order == null || IsOrderLoading(order.ApiId)) return;
            
            try
            {
                SetOrderLoading(order.ApiId, true);
                if (order.PlatformId == 1 || order.PlatformId == 2 || order.PlatformId == 6 || order.PlatformId == 8)
                {
                    var remoteOrderId = !string.IsNullOrWhiteSpace(order.RemoteOrderId)
                        ? order.RemoteOrderId
                        : (!string.IsNullOrWhiteSpace(order.DisplayOrderId) ? order.DisplayOrderId : (order.OrderNumber ?? order.ApiId.ToString()));
                    var result = await _apiService.NotifyReadyToPickupToDeliveryPlatformAsync(remoteOrderId);
                    if (!result.IsSuccess)
                    {
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to Notify Delivery Platform", $"Failed to notify delivery platform: {result.ErrorMessage}");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                        return;
                    }
                }
                else
                {
                    await _apiService.UpdateOrderStatusAsync(order.ApiId, "READY");
                }
                
                SafeRemove(PreparingOrders, order.ApiId);
                SafeRemove(ServedOrders, order.ApiId);
                SafeAdd(ReadyOrders, order);
                SafeRemove(_allPreparingOrders, order.ApiId);
                SafeRemove(_allServedOrders, order.ApiId);
                SafeAdd(_allReadyOrders, order);

                await RefreshReadyOrdersAsync();
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error", "Error updating order status");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            finally
            {
                SetOrderLoading(order.ApiId, false);
            }
        }

        private async void OnExternalOrderStatusChanged(int orderId, string newStatus)
        {
            try
            {
                OrderModel order = QueueOrders.FirstOrDefault(o => o.ApiId == orderId)
                    ?? PreparingOrders.FirstOrDefault(o => o.ApiId == orderId)
                    ?? ReadyOrders.FirstOrDefault(o => o.ApiId == orderId)
                    ?? ServedOrders.FirstOrDefault(o => o.ApiId == orderId);

                if (order == null) return;

                SafeRemove(QueueOrders, orderId);
                SafeRemove(PreparingOrders, orderId);
                SafeRemove(ReadyOrders, orderId);
                SafeRemove(ServedOrders, orderId);
                SafeRemove(_allQueueOrders, orderId);
                SafeRemove(_allPreparingOrders, orderId);
                SafeRemove(_allReadyOrders, orderId);
                SafeRemove(_allServedOrders, orderId);

                switch (newStatus)
                {
                    case "QUEUE":
                    case "ACCEPTED":
                        SafeAdd(QueueOrders, order);
                        SafeAdd(_allQueueOrders, order);
                        break;
                    case "PREPARING":
                        SafeAdd(PreparingOrders, order);
                        SafeAdd(_allPreparingOrders, order);
                        await RefreshPreparingOrdersAsync();
                        break;
                    case "READY":
                    case "READY_FOR_PICKUP":
                        SafeAdd(ReadyOrders, order);
                        SafeAdd(_allReadyOrders, order);
                        await RefreshReadyOrdersAsync();
                        break;
                    case "SERVED":
                    case "DELIVERED":
                        SafeAdd(ServedOrders, order);
                        SafeAdd(_allServedOrders, order);
                        await RefreshServedDispatchedOrdersAsync();
                        break;
                    case "COMPLETED":
                    case "CANCELLED":
                        break;
                }
                order.ApiStatus = newStatus;
            }
            catch { }
        }

        private async void MoveToServed(OrderModel order)
        {
            if (order == null || IsOrderLoading(order.ApiId)) return;
            
            try
            {
                SetOrderLoading(order.ApiId, true);

                // Table order (platform 8) Dine-in: notify delivery platform that order is served
                if (order.IsTableOrderDineIn)
                {
                    var remoteOrderId = !string.IsNullOrWhiteSpace(order.RemoteOrderId)
                        ? order.RemoteOrderId
                        : (!string.IsNullOrWhiteSpace(order.DisplayOrderId) ? order.DisplayOrderId : (order.OrderNumber ?? order.ApiId.ToString()));
                    var result = await _apiService.NotifyServeOrderToDeliveryPlatformAsync(remoteOrderId);
                    if (!result.IsSuccess)
                    {
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to Notify Served", result.ErrorMessage ?? "Failed to notify delivery platform.");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                        return;
                    }
                }
                else {
                    // Update status via API
                    await _apiService.UpdateOrderStatusAsync(order.ApiId, "SERVED");
                }
                
                SafeRemove(ReadyOrders, order.ApiId);
                SafeRemove(PreparingOrders, order.ApiId);
                SafeAdd(ServedOrders, order);
                SafeRemove(_allReadyOrders, order.ApiId);
                SafeRemove(_allPreparingOrders, order.ApiId);
                SafeAdd(_allServedOrders, order);

                await RefreshServedDispatchedOrdersAsync();
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error", "Error updating order status");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            finally
            {
                SetOrderLoading(order.ApiId, false);
            }
        }

        private bool CanMoveToServed(OrderModel order)
        {
            return order != null && order.ShowMoveToServedInReady;
        }

        private async void MoveToDelivered(OrderModel order)
        {
            if (order == null || IsOrderLoading(order.ApiId)) return;
            
            try
            {
                SetOrderLoading(order.ApiId, true);
                await _apiService.UpdateOrderStatusAsync(order.ApiId, "DELIVERED");
                
                SafeRemove(ReadyOrders, order.ApiId);
                SafeRemove(PreparingOrders, order.ApiId);
                SafeRemove(_allReadyOrders, order.ApiId);
                SafeRemove(_allPreparingOrders, order.ApiId);
                
                SafeAdd(ServedOrders, order);
                SafeAdd(_allServedOrders, order);

                await RefreshServedDispatchedOrdersAsync();
                
                order.ApiStatus = "DELIVERED";
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error", "Error updating order status");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            finally
            {
                SetOrderLoading(order.ApiId, false);
            }
        }

        private async void MoveToFinished(OrderModel order)
        {
            if (order == null || IsOrderLoading(order.ApiId)) return;
            try
            {
                SetOrderLoading(order.ApiId, true);
                await MoveToFinishedStatic(order);
            }
            finally
            {
                SetOrderLoading(order.ApiId, false);
            }
        }

        /// <summary>
        /// Static method to handle MoveToFinished logic that can be called from other ViewModels
        /// </summary>
        /// <param name="order">The order to finish</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task MoveToFinishedStatic(OrderModel order)
        {
            if (order == null) return;
            try
            {
                if (order.OrderType == Models.OrderType.DineIn)
                {
                    
                    // Check if the order is a PAY_LATER order
                    if (order.PaymentStatus == "PAID")
                    {
                        var apiService = new ApiService();
                        await apiService.UpdateOrderStatusAsync(order.ApiId, "COMPLETED");
                        order.ApiStatus = "COMPLETED";
                        GlobalDataService.Instance.NotifyOrderStatusChanged(order.ApiId, "COMPLETED");
                        
                    }
                    else {
                        // Set loading state to disable buttons during transition
                    var cashierViewModel = GetCashierViewModel();
                    if (cashierViewModel != null)
                    {
                        cashierViewModel.IsFinishOrderLoading = true;
                    }

                    var apiService = new ApiService();
                    var fullOrder = await apiService.GetOrderByIdAsync(order.ApiId);
                    if (fullOrder == null)
                    {
                        //MessageBox.Show("Unable to load order details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error", "Unable to load order details.");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                        return;
                    }
                    if (await TryShowSplitPaymentCompleteForPendingCartSplitAsync(apiService, "RootDialog").ConfigureAwait(true))
                        return;
                    // Load into cart
                    CartService.Instance.LoadFromOrderModel(fullOrder);

                    // Also store globally (follow existing update-order flow)
                    GlobalDataService.Instance.CurrentOrderForEdit = fullOrder;
                    GlobalDataService.Instance.IsFinishFlow = true;

                    // Navigate to Cashier page
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.MainFrame.Navigate(new CashierHomePage());
                    }
                    else
                    {
                        var currentWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                        if (currentWindow != null)
                        {
                            var frame = currentWindow.FindName("MainFrame") as System.Windows.Controls.Frame;
                            if (frame != null)
                            {
                                frame.Navigate(new CashierHomePage());
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"Loaded dine-in order {fullOrder.DisplayOrderId ?? fullOrder.OrderNumber} into cart and navigated to Cashier.");
                    return;
                    }
                    
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error loading order into cart: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error", "Error loading order into cart");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            finally
            {
                // Clear loading state after completion or error
                var cashierViewModel = GetCashierViewModel();
                if (cashierViewModel != null)
                {
                    cashierViewModel.IsFinishOrderLoading = false;
                }
            }
        }

        private static CashierHomeViewModel GetCashierViewModel()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow?.DataContext is CashierHomeViewModel viewModel)
                {
                    return viewModel;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// When the cashier cart's display order has split temp payments, shows <see cref="SplitPaymentCompleteDialog"/> and returns true.
        /// </summary>
        private static async Task<bool> TryShowSplitPaymentCompleteForPendingCartSplitAsync(ApiService apiService, string dialogHostId)
        {
            var cart = CartService.Instance;
            var cartDisplayId = (cart?.DisplayOrderId ?? cart?.CashierSessionDisplayOrderId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cartDisplayId))
                return false;
            try
            {
                var (_, _, cartTempList) = await apiService.GetTempPaymentsByDisplayOrderIdAsync(cartDisplayId).ConfigureAwait(true);
                if (cartTempList != null && cartTempList.Count > 0)
                {
                    var splitCompleteDlg = new SplitPaymentCompleteDialog();
                    await DialogHost.Show(splitCompleteDlg, dialogHostId).ConfigureAwait(true);
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        public static async Task MoveToCompletedStatic(OrderModel order, bool fromSessionOrdersDialog = false, string dialogHostId = "RootDialog")
        {
            if (order == null) return;
            try
            {
                // Ensure host is closed before showing (avoids "DialogHost is already open" when called after Finish Order)
                int waitMs = 0;
                while (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen(dialogHostId) && waitMs < 2500)
                {
                    await Task.Delay(50);
                    waitMs += 50;
                }

                var statusText = order.PaymentStatus?.Trim() ?? string.Empty;
                var paidByStatus = statusText.StartsWith("PAID", System.StringComparison.OrdinalIgnoreCase);
                var methodText = order.PaymentMethod?.Trim() ?? string.Empty;
                // Normalize method: keep letters only and uppercase, to support variants like "PAY ON DELIVERY", "cash_on_delivery", etc.
                var normalizedMethod = new string(methodText.Where(char.IsLetter).ToArray()).ToUpperInvariant();
                var isCashMethod = normalizedMethod == "CASH" || normalizedMethod == "CASHONDELIVERY";
                var isPayOnDelivery = normalizedMethod == "PAYONDELIVERY";
                var isPayOnCollection = normalizedMethod == "PAYONCOLLECTION";
                var isPayOnTakeaway = normalizedMethod == "PAYONTAKEAWAY";
                var isCod = normalizedMethod == "COD";
                var isCot = normalizedMethod == "COT"; // Collect on Takeaway from POS checkout
                var requiresCheckoutByMethod = isCashMethod || isPayOnDelivery || isPayOnCollection || isCod || isCot || isPayOnTakeaway;
                var isUnpaid = !order.IsPaid && !paidByStatus;
                var isPayLater = normalizedMethod == "PAYLATER";
                var apiService = new ApiService();

                // Show checkout when unpaid and (method requires checkout OR user already confirmed session orders and clicked Next)
                var shouldOpenCheckout = isUnpaid && (requiresCheckoutByMethod || fromSessionOrdersDialog);

                // Table orders (platform 8) from session dialog: session PaymentStatus only — do not use order IsPaid/PaymentStatus here
                var isTableOrder = order.PlatformId == 8 || order.PlatformId2 == 8;
                if (isTableOrder && order.OrderSessionId.HasValue && order.OrderSessionId.Value > 0 && fromSessionOrdersDialog)
                {
                    try
                    {
                        var sessionResponse = await apiService.GetSessionOrdersAsync(order.OrderSessionId.Value);
                        var sessionPayment = sessionResponse?.Data?.PaymentStatus?.Trim() ?? string.Empty;
                        var sessionPaid = sessionPayment.StartsWith("PAID", StringComparison.OrdinalIgnoreCase);
                        shouldOpenCheckout = !sessionPaid;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"MoveToCompletedStatic: session payment check failed: {ex.Message}");
                        // On failure, keep shouldOpenCheckout from order-level flags
                    }
                }

                if (shouldOpenCheckout)
                {
                    if (await TryShowSplitPaymentCompleteForPendingCartSplitAsync(apiService, dialogHostId).ConfigureAwait(true))
                        return;

                    var checkoutVm = new POS_UI.ViewModels.KitchenCheckoutViewModel(order, dialogHostId);
                    var displayId = !string.IsNullOrWhiteSpace(order?.DisplayOrderId)
                        ? order.DisplayOrderId
                        : (order?.OrderNumber ?? order?.ApiId.ToString() ?? "");
                    var hasSessionId = order?.OrderSessionId.HasValue == true && order.OrderSessionId.Value > 0;
                    var tempPaymentTypeId = hasSessionId ? order.OrderSessionId.Value.ToString() : displayId;
                    var hasExistingTempPayments = false;
                    if (!string.IsNullOrWhiteSpace(tempPaymentTypeId))
                    {
                        try
                        {
                            var (_, _, tempList) = await apiService.GetTempPaymentsByDisplayOrderIdAsync(tempPaymentTypeId);
                            hasExistingTempPayments = tempList != null && tempList.Count > 0;
                        }
                        catch
                        {
                            hasExistingTempPayments = false;
                        }
                    }

                    if (hasExistingTempPayments)
                    {
                        await checkoutVm.OpenSplitPaymentDialogFromCompletionAsync(dialogHostId);
                    }
                    else
                    {
                        var checkoutDialog = new POS_UI.View.CheckoutDialog { DataContext = checkoutVm };
                        await MaterialDesignThemes.Wpf.DialogHost.Show(checkoutDialog, dialogHostId);
                    }
                    return;
                }

                //var apiService = new ApiService();

                // POS-origin orders: PlatformId2 (from platform_id) is the authoritative order source;
                // PlatformId (from delivery_platform_id) can be the shop's delivery integration, not the order's origin.
                if (order.PlatformId2 == 9 || (order.PlatformId2 == 0 && order.PlatformId == 9))
                {
                    await apiService.UpdateOrderStatusAsync(order.ApiId, "COMPLETED");
                    order.ApiStatus = "COMPLETED";
                    GlobalDataService.Instance.NotifyOrderStatusChanged(order.ApiId, "COMPLETED");
                    return;
                }

                // External platforms (Deliveroo=1, UberEats=2, Webshop=6)
                var isDeliveryPlatform = order.PlatformId == 1 || order.PlatformId == 2 || order.PlatformId == 6
                                          || order.PlatformId2 == 1 || order.PlatformId2 == 2 || order.PlatformId2 == 6;
                if (isDeliveryPlatform)
                {
                    var remoteOrderId = !string.IsNullOrWhiteSpace(order.RemoteOrderId)
                        ? order.RemoteOrderId
                        : (!string.IsNullOrWhiteSpace(order.DisplayOrderId) ? order.DisplayOrderId : (order.OrderNumber ?? order.ApiId.ToString()));
                    var result = await apiService.NotifyCompleteOrderToDeliveryPlatformAsync(remoteOrderId,"CARD");
                    if (!result.IsSuccess)
                    {
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to Notify Delivery Platform", $"Failed to notify delivery platform: {result.ErrorMessage}");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, dialogHostId);
                        return;
                    }

                    order.ApiStatus = "COMPLETED";
                    GlobalDataService.Instance.NotifyOrderStatusChanged(order.ApiId, "COMPLETED");
                    return;
                }
                else if (order.PlatformId == 8 || order.PlatformId2 == 8)
                {
                    // When not from SessionOrdersDialog: show session orders dialog for unpaid TableOrder so user confirms all items ready
                    if (order.OrderSessionId.HasValue && order.OrderSessionId.Value > 0 && !fromSessionOrdersDialog)
                    {
                        var sessionVm = new POS_UI.ViewModels.SessionOrdersDialogViewModel(order.OrderSessionId.Value, order, dialogHostId);
                        var sessionDialog = new POS_UI.View.SessionOrdersDialog { DataContext = sessionVm };
                        await MaterialDesignThemes.Wpf.DialogHost.Show(sessionDialog, dialogHostId);
                        return;
                    }

                    /*int[] tableIds = null;
                    if (order.OrderSessionId.HasValue && order.OrderSessionId.Value > 0)
                    {
                        tableIds = await apiService.GetTableIdsFromSessionAsync(order.OrderSessionId.Value);
                    }
                    if ((tableIds == null || tableIds.Length == 0) && order.TableNumber.HasValue && order.TableNumber.Value > 0)
                    {
                        tableIds = new[] { order.TableNumber.Value };
                    }
                    if (tableIds != null && tableIds.Length > 0)
                    {
                        foreach (var id in tableIds)
                            await apiService.UpdateTableStatusAsync(id, "AVAILABLE", 0);
                    }
                    */
                    var remoteOrderId = !string.IsNullOrWhiteSpace(order.RemoteOrderId)
                        ? order.RemoteOrderId
                        : (!string.IsNullOrWhiteSpace(order.DisplayOrderId) ? order.DisplayOrderId : (order.OrderNumber ?? order.ApiId.ToString()));
                    var result = await apiService.NotifyCompleteOrderToDeliveryPlatformAsync(remoteOrderId,"CARD");
                    if (!result.IsSuccess)
                    {
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to Notify Delivery Platform", $"Failed to notify delivery platform: {result.ErrorMessage}");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, dialogHostId);
                        return;
                    }

                    order.ApiStatus = "COMPLETED";
                    GlobalDataService.Instance.NotifyOrderStatusChanged(order.ApiId, "COMPLETED");
                    GlobalDataService.Instance.RequestKitchenRefresh();
                    if (LiveOrdersViewModel.Instance != null)
                        _ = LiveOrdersViewModel.Instance.LoadOrdersAsync();
                    //Request Tables Refresh
                    POS_UI.Services.GlobalDataService.Instance.RequestTablesRefresh();
                    return;
                }
                
                // POS-origin orders
                await apiService.UpdateOrderStatusAsync(order.ApiId, "COMPLETED");
                order.ApiStatus = "COMPLETED";
                GlobalDataService.Instance.NotifyOrderStatusChanged(order.ApiId, "COMPLETED");
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error", $"Error updating order status: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, dialogHostId);
            }
        }

        public async void MoveToCompleted(OrderModel order)
        {
            if (order == null || IsOrderLoading(order.ApiId)) return;
            
            try
            {
                SetOrderLoading(order.ApiId, true);
                await MoveToCompletedStatic(order);
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error updating order status: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error", $"Error updating order status: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            finally
            {
                SetOrderLoading(order.ApiId, false);
            }
        }

        private string GetPlatformIds()
        {
            // Return all platform IDs for initial load: DELIVEROO: 1, UBER_EATS: 2, WEBSHOP: 6, TABLE_ORDER: 8, DG_POS: 9
            return "1,2,6,8,9";
        }

        private string GetSelectedPlatformId()
        {
            return SelectedPlatform switch
            {
                "Deliveroo" => "1",
                "UberEats" => "2", 
                "Webshop" => "6",
                "Table Order" => "8",
                "DG POS" => "9",
                _ => GetPlatformIds() // "All Platforms" returns all IDs
            };
        }

        private async Task LoadOrdersFromApiAsync()
        {
            try
            {
                IsLoading = true;
                
                // Check if user is logged in
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    //MessageBox.Show("Please log in first to view orders.", "Authentication Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get platform IDs based on selection
                var platformIds = GetSelectedPlatformId();

                // Load orders for each status
                var queueOrders = await _apiService.GetOrdersAsync("QUEUE", platformIds);
                var acceptedOrders = await _apiService.GetOrdersAsync("ACCEPTED", platformIds);
                var preparingOrders = await _apiService.GetOrdersAsync("PREPARING", platformIds);
                var readyOrders = await _apiService.GetOrdersAsync("READY", platformIds);
                var readyForPickupOrders = await _apiService.GetOrdersAsync("READY_FOR_PICKUP", platformIds);
                var servedOrders = await _apiService.GetOrdersAsync("SERVED", platformIds);
                var deliveredOrders = await _apiService.GetOrdersAsync("DELIVERED", platformIds);

                // Filter by order type if selected
                if (SelectedOrderType.HasValue)
                {
                    queueOrders = queueOrders.Where(o => o.OrderType == SelectedOrderType.Value).ToList();
                    acceptedOrders = acceptedOrders.Where(o => o.OrderType == SelectedOrderType.Value).ToList();
                    preparingOrders = preparingOrders.Where(o => o.OrderType == SelectedOrderType.Value).ToList();
                    readyOrders = readyOrders.Where(o => o.OrderType == SelectedOrderType.Value).ToList();
                    readyForPickupOrders = readyForPickupOrders.Where(o => o.OrderType == SelectedOrderType.Value).ToList();
                    servedOrders = servedOrders.Where(o => o.OrderType == SelectedOrderType.Value).ToList();
                    deliveredOrders = deliveredOrders.Where(o => o.OrderType == SelectedOrderType.Value).ToList();
                }
                // Combine statuses and deduplicate by ApiId
                var dedupQueue = queueOrders.Concat(acceptedOrders)
                    .GroupBy(o => o.ApiId).Select(g => g.First())
                    .OrderBy(o => o.CreatedAt).ToList();
                var dedupPreparing = preparingOrders
                    .GroupBy(o => o.ApiId).Select(g => g.First())
                    .OrderBy(o => o.CreatedAt).ToList();
                var dedupReady = readyOrders.Concat(readyForPickupOrders)
                    .GroupBy(o => o.ApiId).Select(g => g.First())
                    .OrderBy(o => o.CreatedAt).ToList();
                var dedupServed = servedOrders.Concat(deliveredOrders)
                    .GroupBy(o => o.ApiId).Select(g => g.First())
                    .OrderBy(o => o.CreatedAt).ToList();

                // Update backing lists to the deduplicated results
                _allQueueOrders = dedupQueue;
                _allPreparingOrders = dedupPreparing;
                _allReadyOrders = dedupReady;
                _allServedOrders = dedupServed;

                // Rebuild observable collections
                QueueOrders.Clear();
                PreparingOrders.Clear();
                ReadyOrders.Clear();
                ServedOrders.Clear();

                foreach (var order in dedupQueue) QueueOrders.Add(order);
                foreach (var order in dedupPreparing) PreparingOrders.Add(order);
                foreach (var order in dedupReady) ReadyOrders.Add(order);
                foreach (var order in dedupServed) ServedOrders.Add(order);
            }
            catch (Exception ex)
            {
                // Handle error - could show a message to user
                //MessageBox.Show($"Error loading orders: {ex.Message}");
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error", "Error loading orders");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task RefreshOrdersAsync()
        {
            await LoadOrdersFromApiAsync();
        }

        private async Task RefreshPreparingOrdersAsync()
        {
            try
            {
                // Get platform IDs based on selection
                var platformIds = GetSelectedPlatformId();

                // Load preparing orders from API
                var preparingOrders = await _apiService.GetOrdersAsync("PREPARING", platformIds);

                // Filter by order type if selected
                if (SelectedOrderType.HasValue)
                {
                    preparingOrders = preparingOrders.Where(o => o.OrderType == SelectedOrderType.Value).ToList();
                }

                // Update the all preparing orders list
                _allPreparingOrders = preparingOrders;

                // Clear and update the observable collection
                PreparingOrders.Clear();
                foreach (var order in preparingOrders.OrderBy(o => o.CreatedAt))
                {
                    PreparingOrders.Add(order);
                }
            }
            catch (Exception ex)
            {
                // Handle error silently to avoid disrupting the main flow
                System.Diagnostics.Debug.WriteLine($"Error refreshing preparing orders: {ex.Message}");
            }
        }

        private async Task RefreshReadyOrdersAsync()
        {
            try
            {
                var platformIds = GetSelectedPlatformId();

                var readyOrders = await _apiService.GetOrdersAsync("READY", platformIds);
                var readyForPickupOrders = await _apiService.GetOrdersAsync("READY_FOR_PICKUP", platformIds);

                if (SelectedOrderType.HasValue)
                {
                    readyOrders = readyOrders.Where(o => o.OrderType == SelectedOrderType.Value).ToList();
                    readyForPickupOrders = readyForPickupOrders.Where(o => o.OrderType == SelectedOrderType.Value).ToList();
                }

                var combinedReady = readyOrders.Concat(readyForPickupOrders)
                    .GroupBy(o => o.ApiId).Select(g => g.First())
                    .OrderBy(o => o.CreatedAt).ToList();

                _allReadyOrders = combinedReady;

                ReadyOrders.Clear();
                foreach (var order in combinedReady)
                {
                    ReadyOrders.Add(order);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing ready orders: {ex.Message}");
            }
        }

		private async Task RefreshServedDispatchedOrdersAsync()
		{
			try
			{
				var platformIds = GetSelectedPlatformId();

				var servedOrders = await _apiService.GetOrdersAsync("SERVED", platformIds);
				var deliveredOrders = await _apiService.GetOrdersAsync("DELIVERED", platformIds);

				if (SelectedOrderType.HasValue)
				{
					servedOrders = servedOrders.Where(o => o.OrderType == SelectedOrderType.Value).ToList();
					deliveredOrders = deliveredOrders.Where(o => o.OrderType == SelectedOrderType.Value).ToList();
				}

				var combinedServed = servedOrders.Concat(deliveredOrders)
					.GroupBy(o => o.ApiId).Select(g => g.First())
					.OrderBy(o => o.CreatedAt).ToList();

				_allServedOrders = combinedServed;

				ServedOrders.Clear();
				foreach (var order in combinedServed)
				{
					ServedOrders.Add(order);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error refreshing served/dispatched orders: {ex.Message}");
			}
		}
    }
} 