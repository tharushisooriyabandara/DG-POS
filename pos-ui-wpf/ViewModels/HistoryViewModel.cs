using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using POS_UI.Models;
using POS_UI.Services;
using POS_UI.ViewModels;

namespace POS_UI.ViewModels
{
    public class HistoryViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly SettingsService _settingsService;
        
        private List<OrderModel> _allOrders = new List<OrderModel>();
        private ObservableCollection<OrderModel> _paginatedOrders;
        private string _searchText;
        private bool _isLoading;
        private OrderModel _selectedOrder;
        
        // Pagination backing fields
        private int _pageSize = 10;
        private int _totalPages = 1;
        private int _currentPagination = 1;
        // Date filters
        private DateTime? _fromDate;
        private DateTime? _toDate;

        public string CurrentPage { get; set; }
        public HistoryViewModel()
        {
            _apiService = new ApiService();
            _settingsService = new SettingsService();
            CurrentPage = "History";
            _paginatedOrders = new ObservableCollection<OrderModel>();
            
            LoadOrdersCommand = new AsyncRelayCommand(LoadOrdersAsync, CanLoadOrders);
            SearchCommand = new RelayCommand(PerformSearch);
            NextPageCommand = new RelayCommand(NextPage, CanGoNextPage);
            PrevPageCommand = new RelayCommand(PrevPage, CanGoPrevPage);
            FirstPageCommand = new RelayCommand(FirstPage, CanGoPrevPage);
            LastPageCommand = new RelayCommand(LastPage, CanGoNextPage);
            ShowCommand = new AsyncRelayCommand(ShowAsync, CanLoadOrders);
            ((AsyncRelayCommand)ShowCommand).RaiseCanExecuteChanged();
            ClearDatesCommand = new RelayCommand(ClearDates);
            ExportCommand = new AsyncRelayCommand(ExportOrdersAsync, CanExportOrders);
            
            // Load orders when ViewModel is created
            _fromDate = DateTime.Today;
            _toDate = DateTime.Today;
            _ = LoadOrdersAsync();
        }

        public ObservableCollection<OrderModel> PaginatedOrders
        {
            get => _paginatedOrders;
            set
            {
                _paginatedOrders = value;
                OnPropertyChanged(nameof(PaginatedOrders));
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                PerformSearch();
            }
        }

        public DateTime? FromDate
        {
            get => _fromDate;
            set 
            { 
                _fromDate = value; 
                OnPropertyChanged(nameof(FromDate));
                ((AsyncRelayCommand)ExportCommand).RaiseCanExecuteChanged();
            }
        }

        public DateTime? ToDate
        {
            get => _toDate;
            set 
            { 
                _toDate = value; 
                OnPropertyChanged(nameof(ToDate));
                ((AsyncRelayCommand)ExportCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
                // Refresh Show button state when loading state changes
                ((AsyncRelayCommand)ShowCommand).RaiseCanExecuteChanged();
                // Refresh Export button state when loading state changes
                ((AsyncRelayCommand)ExportCommand).RaiseCanExecuteChanged();
            }
        }

        public OrderModel SelectedOrder
        {
            get => _selectedOrder;
            set
            {
                _selectedOrder = value;
                OnPropertyChanged(nameof(SelectedOrder));
            }
        }

        // Pagination properties
        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (value <= 0) return;
                if (_pageSize == value) return;
                _pageSize = value;
                OnPropertyChanged();
                // Recalculate pagination with current filtered orders
                var currentFilteredOrders = GetCurrentFilteredOrders();
                RecalculatePaging(currentFilteredOrders, resetToFirstPage: true);
            }
        }

        public int CurrentPagination
        {
            get => _currentPagination;
            private set
            {
                if (_currentPagination == value) return;
                _currentPagination = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageInfoText));
                RefreshPaginationCommands();
            }
        }

        public int TotalPages
        {
            get => _totalPages;
            private set
            {
                if (_totalPages == value) return;
                _totalPages = Math.Max(1, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageInfoText));
                RefreshPaginationCommands();
            }
        }

        public string PageInfoText => $"Page {CurrentPagination} of {TotalPages}";

        public ICommand LoadOrdersCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand FirstPageCommand { get; }
        public ICommand LastPageCommand { get; }
        public ICommand ShowCommand { get; }
        public ICommand ClearDatesCommand { get; }
        public ICommand ExportCommand { get; }
        
        private async Task LoadOrdersAsync()
        {
            try
            {
                IsLoading = true;
                
                // Get all orders without status filter, only platforms and outlet_id
                var (_, outletCode, _) = _settingsService.LoadSettings();
                var shopDetails = GlobalDataService.Instance.ShopDetails;
                
                // Get outlet_id
                var outletId = 0;
                if (shopDetails != null)
                {
                    if (shopDetails.DeliveryPlatform != null && shopDetails.DeliveryPlatform.OutletId > 0)
                    {
                        outletId = shopDetails.DeliveryPlatform.OutletId;
                    }
                    else if (shopDetails.Id > 0)
                    {
                        outletId = shopDetails.Id;
                    }
                }

                if (outletId <= 0)
                {
                    throw new Exception("Outlet ID is 0. Ensure shop details are loaded.");
                }

                // Get orders from all platforms (1,2,6,8,9)
                var platforms = "1,2,6,8,9";
                
                // Call GetOrdersAsync without status parameter to get all orders
                var orders = await _apiService.GetOrdersAsync(status: "", platforms: platforms, outletId: outletId, fromDate: _fromDate, toDate: _toDate);
                
                _allOrders = orders.OrderByDescending(o => o.CreatedAt).ToList();
                
                // Apply current search filter and pagination
                PerformSearch();
            }
            catch (Exception ex)
            {
                //System.Windows.MessageBox.Show($"Error loading orders: {ex.Message}", "Error", 
                  //  System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ShowAsync()
        {
            if (FromDate == null)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("From Date is required", "From Date is required. Please select a date.");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                return;
            }
            await LoadOrdersAsync();
        }

        private void ClearDates()
        {
            // clear ToDate and FromDate
            ToDate = null;
            FromDate = null;
        }

        private bool CanLoadOrders()
        {
            return !IsLoading;
        }

        private void PerformSearch()
        {
            List<OrderModel> filteredOrders;
            
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                // Show all orders if no search text
                filteredOrders = _allOrders.ToList();
            }
            else
            {
                // Filter orders based on search text
                var searchLower = SearchText.ToLower();
                filteredOrders = _allOrders.Where(order =>
                    (order.OrderNumber?.ToLower().Contains(searchLower) ?? false) ||
                    (order.ApiStatus?.ToLower().Contains(searchLower) ?? false) ||
                    (order.PlatformName?.ToLower().Contains(searchLower) ?? false) ||
                    (order.ShippingMethod?.ToLower().Contains(searchLower) ?? false)
                ).ToList();
            }

            // Update pagination with filtered results
            RecalculatePaging(filteredOrders, resetToFirstPage: true);
        }

        private void RecalculatePaging(List<OrderModel> orders, bool resetToFirstPage)
        {
            // Compute total pages
            int count = orders?.Count ?? 0;
            TotalPages = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
            if (resetToFirstPage)
            {
                CurrentPagination = 1;
            }
            else if (CurrentPagination > TotalPages)
            {
                CurrentPagination = TotalPages;
            }
            UpdatePagedOrders(orders);
        }

        private void UpdatePagedOrders(List<OrderModel> orders)
        {
            _paginatedOrders.Clear();
            if (orders == null || orders.Count == 0)
            {
                return;
            }

            int skip = (CurrentPagination - 1) * PageSize;
            foreach (var order in orders.Skip(skip).Take(PageSize))
            {
                _paginatedOrders.Add(order);
            }
        }

        private void RefreshPaginationCommands()
        {
            ((RelayCommand)NextPageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)PrevPageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)FirstPageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)LastPageCommand).RaiseCanExecuteChanged();
        }

        private bool CanGoNextPage() => CurrentPagination < TotalPages;
        private bool CanGoPrevPage() => CurrentPagination > 1;
        
        private void NextPage()
        {
            if (!CanGoNextPage()) return;
            CurrentPagination++;
            UpdatePagedOrders(GetCurrentFilteredOrders());
        }
        
        private void PrevPage()
        {
            if (!CanGoPrevPage()) return;
            CurrentPagination--;
            UpdatePagedOrders(GetCurrentFilteredOrders());
        }
        
        private void FirstPage()
        {
            if (!CanGoPrevPage()) return;
            CurrentPagination = 1;
            UpdatePagedOrders(GetCurrentFilteredOrders());
        }
        
        private void LastPage()
        {
            if (!CanGoNextPage()) return;
            CurrentPagination = TotalPages;
            UpdatePagedOrders(GetCurrentFilteredOrders());
        }

        private List<OrderModel> GetCurrentFilteredOrders()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return _allOrders;
            }
            
            var searchLower = SearchText.ToLower();
            return _allOrders.Where(order =>
                (order.OrderNumber?.ToLower().Contains(searchLower) ?? false) ||
                (order.ApiStatus?.ToLower().Contains(searchLower) ?? false) ||
                (order.PlatformName?.ToLower().Contains(searchLower) ?? false) ||
                (order.ShippingMethod?.ToLower().Contains(searchLower) ?? false)
            ).ToList();
        }

        private bool CanExportOrders()
        {
            return !IsLoading && _fromDate.HasValue;
        }

        private async Task ExportOrdersAsync()
        {
            try
            {
                IsLoading = true;
                ((AsyncRelayCommand)ExportCommand).RaiseCanExecuteChanged();

                // Call the ApiService to get the CSV data
                var csvContent = await _apiService.ExportOrdersAsync(
                    platforms: "1,2,4,6,8,9", 
                    outletId: null, // Let ApiService handle outlet ID from shop details
                    fromDate: _fromDate, 
                    toDate: _toDate
                );
                //MessageBox.Show("outlet id: " + outletId);

                // Generate filename based on date range
                var fromDateStr = FromDate?.ToString("yyyy.MM.dd") ?? "Unknown";
                var toDateStr = ToDate?.ToString("yyyy.MM.dd") ?? "Unknown";
                var fileName = ToDate.HasValue ? 
                    $"Report_{fromDateStr}-{toDateStr}.csv" : 
                    $"Report_{fromDateStr}.csv";

                // Show save file dialog
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    FileName = fileName
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    await File.WriteAllBytesAsync(saveFileDialog.FileName, csvContent);
                    
                    // Show success message
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateSuccess("Orders exported successfully", $"Orders exported successfully to:\n{saveFileDialog.FileName}");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                }
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to export orders", $"Failed to export orders: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            finally
            {
                IsLoading = false;
                ((AsyncRelayCommand)ExportCommand).RaiseCanExecuteChanged();
            }
        }
    }
}
