using POS_UI.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using POS_UI.Services;
using System.Threading.Tasks;
using System;

namespace POS_UI.ViewModels
{
    public enum LiveOrdersFilter
    {
        All,
        Paid,
        Unpaid,
        DineIn,
        Takeaway,
        Delivery
    }

    public class LiveOrdersViewModel : BaseViewModel
    {
        public static LiveOrdersViewModel Instance { get; private set; }

        private readonly ApiService _apiService = new ApiService();
        private List<OrderModel> _allOrders = new List<OrderModel>();
        private ObservableCollection<OrderModel> _orders;
        private bool _isLoading;
        private LiveOrdersFilter _selectedFilter = LiveOrdersFilter.All;

        public ObservableCollection<OrderModel> Orders
        {
            get => _orders;
            set { _orders = value; OnPropertyChanged(nameof(Orders)); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
        }

        public LiveOrdersFilter SelectedFilter
        {
            get => _selectedFilter;
            set { _selectedFilter = value; OnPropertyChanged(nameof(SelectedFilter)); ApplyFilter(); }
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;

            set
            {
                if (_isProcessing == value) return;
                _isProcessing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotProcessing));
            }
        }
        public bool IsNotProcessing => !IsProcessing;

        public bool IsFilterAll => SelectedFilter == LiveOrdersFilter.All;
        public bool IsFilterUnpaid => SelectedFilter == LiveOrdersFilter.Unpaid;
        public bool IsFilterPaid => SelectedFilter == LiveOrdersFilter.Paid;
        public bool IsFilterDineIn => SelectedFilter == LiveOrdersFilter.DineIn;
        public bool IsFilterTakeaway => SelectedFilter == LiveOrdersFilter.Takeaway;
        public bool IsFilterDelivery => SelectedFilter == LiveOrdersFilter.Delivery;


        public string CurrentPage { get; set; }
        public ICommand RefreshCommand { get; }
        public ICommand SetFilterAllCommand { get; }
        public ICommand SetFilterUnpaidCommand { get; }
        public ICommand SetFilterPaidCommand { get; }
        public ICommand SetFilterDineInCommand { get; }
        public ICommand SetFilterTakeawayCommand { get; }
        public ICommand SetFilterDeliveryCommand { get; }
        public ICommand PaymentOrderCommand { get; }
        public ICommand CompleteOrderCommand { get; }
        public ICommand ReadyOrderCommand { get; }
        public ICommand ViewOrderCommand { get; }

        public LiveOrdersViewModel()
        {
            Instance = this;
            CurrentPage = "LiveOrders";
            _orders = new ObservableCollection<OrderModel>();
            ViewOrderCommand = new RelayCommand<OrderModel>(async o => await ViewOrderAsync(o));
            RefreshCommand = new RelayCommand(async () => await LoadOrdersAsync());
            SetFilterAllCommand = new RelayCommand(() => { SelectedFilter = LiveOrdersFilter.All; });
            SetFilterUnpaidCommand = new RelayCommand(() => { SelectedFilter = LiveOrdersFilter.Unpaid; });
            SetFilterPaidCommand = new RelayCommand(() => { SelectedFilter = LiveOrdersFilter.Paid; });
            SetFilterDineInCommand = new RelayCommand(() => { SelectedFilter = LiveOrdersFilter.DineIn; });
            SetFilterTakeawayCommand = new RelayCommand(() => { SelectedFilter = LiveOrdersFilter.Takeaway; });
            SetFilterDeliveryCommand = new RelayCommand(() => { SelectedFilter = LiveOrdersFilter.Delivery; });
            PaymentOrderCommand = new RelayCommand<OrderModel>(async o => { await RunPaymentOrCompleteAsync(o); });
            CompleteOrderCommand = new RelayCommand<OrderModel>(async o => { await RunPaymentOrCompleteAsync(o); });
            ReadyOrderCommand = new RelayCommand<OrderModel>(async o => { await MoveToReadyAsync(o); });
            _ = LoadOrdersAsync();
            GlobalDataService.Instance.OrderStatusChanged += OnOrderStatusChanged;
        }

        private async System.Threading.Tasks.Task RunPaymentOrCompleteAsync(OrderModel order)
        {
            if (order == null) return;
            if (IsProcessing) return;
            try
            {
                IsProcessing = true;
            if( string.Equals(order.ShippingMethod,"Dine-in",StringComparison.OrdinalIgnoreCase))
            {
                await KitchenViewModel.MoveToFinishedStatic(order);
            }
            else
            {
                await KitchenViewModel.MoveToCompletedStatic(order);
            }
            }
            catch (Exception ex)
            {
                var vm = StatusDialogViewModel.CreateError("Error", ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            finally
            {
                IsProcessing = false;
            }
            await LoadOrdersAsync();
        }

        private async System.Threading.Tasks.Task MoveToReadyAsync(OrderModel order)
        {
            if (order == null) return;

            if (IsProcessing) return;
            try
            {
                IsProcessing = true;
                if (order.PlatformId == 1 || order.PlatformId == 2 || order.PlatformId == 6 || order.PlatformId == 8)
                {
                    var remoteOrderId = !string.IsNullOrWhiteSpace(order.RemoteOrderId)
                        ? order.RemoteOrderId
                        : (!string.IsNullOrWhiteSpace(order.DisplayOrderId) ? order.DisplayOrderId : (order.OrderNumber ?? order.ApiId.ToString()));
                    var result = await _apiService.NotifyReadyToPickupToDeliveryPlatformAsync(remoteOrderId);
                    if (!result.IsSuccess)
                    {
                        var vm = StatusDialogViewModel.CreateError("Failed to Notify Delivery Platform", result.ErrorMessage ?? "Unknown error");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                        return;
                    }
                }
                await LoadOrdersAsync();
            }
            catch (Exception ex)
            {
                var vm = StatusDialogViewModel.CreateError("Error", ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void OnOrderStatusChanged(int orderId, string newStatus)
        {
            if (string.Equals(newStatus, "COMPLETED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(newStatus, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            {
                var order = _allOrders.FirstOrDefault(o => o.ApiId == orderId);
                if (order != null)
                {
                    _allOrders.Remove(order);
                    ApplyFilter();
                }
            }
        }

        private void ApplyFilter()
        {
            IEnumerable<OrderModel> filtered = _allOrders;
            switch (SelectedFilter)
            {
                case LiveOrdersFilter.Unpaid:
                    filtered = _allOrders.Where(o => string.Equals(o.PaymentStatus, "UNPAID", StringComparison.OrdinalIgnoreCase));
                    break;
                case LiveOrdersFilter.Paid:
                    filtered = _allOrders.Where(o => string.Equals(o.PaymentStatus, "PAID", StringComparison.OrdinalIgnoreCase));
                    break;
                case LiveOrdersFilter.DineIn:
                    filtered = _allOrders.Where(o => 
                        o.PlatformId == 8 
                        ? string.Equals(o.TableOrderMethod, "DINE-IN", StringComparison.OrdinalIgnoreCase)
                        : o.OrderType == OrderType.DineIn);
                    break;
                case LiveOrdersFilter.Takeaway:
                    filtered = _allOrders.Where(o => 
                        o.PlatformId == 8 
                        ? string.Equals(o.TableOrderMethod, "TAKEAWAY", StringComparison.OrdinalIgnoreCase)
                        : o.OrderType == OrderType.TakeAway || o.OrderType == OrderType.Collection);
                    break;
                case LiveOrdersFilter.Delivery:
                    filtered = _allOrders.Where(o => o.OrderType == OrderType.Delivery);
                    break;
            }
            Orders.Clear();
            foreach (var o in filtered.OrderBy(x => x.CreatedAtActual ?? x.CreatedAt))
                Orders.Add(o);
            OnPropertyChanged(nameof(IsFilterAll));
            OnPropertyChanged(nameof(IsFilterUnpaid));
            OnPropertyChanged(nameof(IsFilterPaid));
            OnPropertyChanged(nameof(IsFilterDineIn));
            OnPropertyChanged(nameof(IsFilterTakeaway));
            OnPropertyChanged(nameof(IsFilterDelivery));
        }

        private static string GetAllPlatformIds()
        {
            return "1,2,6,8,9";
        }

        public async Task LoadOrdersAsync()
        {
            try
            {
                IsLoading = true;
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                    return;

                var platformIds = GetAllPlatformIds();
                var statuses = new[] { "QUEUE", "ACCEPTED", "PREPARING", "READY", "READY_FOR_PICKUP", "SERVED", "DELIVERED" };
                var combined = new List<OrderModel>();
                foreach (var status in statuses)
                {
                    try
                    {
                        var list = await _apiService.GetOrdersAsync(status, platformIds);
                        combined.AddRange(list);
                    }
                    catch { /* skip failed status */ }
                }

                _allOrders = combined
                    .GroupBy(o => o.ApiId)
                    .Select(g => g.First())
                    .OrderBy(o => o.CreatedAtActual ?? o.CreatedAt)
                    .ToList();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                var vm = StatusDialogViewModel.CreateError("Error", "Error loading orders: " + ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ViewOrderAsync(OrderModel order)
        {
            if (order == null) return;

            try
            {
                var dialog = new POS_UI.View.KitchenOrderDetailsDialog
                {
                    DataContext = new POS_UI.ViewModels.KitchenOrderDetailsDialogViewModel(order.ApiId, POS_UI.ViewModels.KitchenOrderDetailsDialogViewModel.DialogMode.LiveOrders)
                };
                await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog");
            }
            catch (Exception ex)
            {
                var vm = StatusDialogViewModel.CreateError("Error", "Error showing order details: " + ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
        }
    }
}
