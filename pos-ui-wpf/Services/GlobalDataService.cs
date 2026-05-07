using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using POS_UI.Helpers;
using POS_UI.Models;
using POS_UI.View;
using static POS_UI.ViewModels.SettingsViewModel;
using MaterialDesignThemes.Wpf;

namespace POS_UI.Services
{
    public class GlobalDataService
    {
        private static GlobalDataService _instance;
        private static readonly object _lock = new object();
        
        private readonly ApiService _apiService;
        private readonly LocalStorageService _localStorageService;
        private readonly SettingsService _settingsService;
        private readonly FirebaseService _firebaseService;
        
        private CurrentUserModel _currentUser;
        private ShopModel _shopDetails;
        private OrderModel _currentOrderForEdit;
        private int _incomingOrdersCount;
        // Tracks the currently displayed New Order Alert popup's display order id (if any)
        private string _currentNewOrderAlertDisplayOrderId;
        // Tracks the currently open OrderDetailsDialog's display order id (if any)
        private string _currentOrderDetailsDialogDisplayOrderId;
        // Indicates navigation to Cashier came from a Dine-In Finish action
        public bool IsFinishFlow { get; set; }
        // Indicates CurrentOrderForEdit was hydrated from terminal ongoing_order config (resume cart after login)
        public bool HasOngoingOrderFromConfig { get; set; }
        // Queue orders received while not on Cashier page
        //private readonly System.Collections.Generic.List<string> _pendingIncomingOrdersJson = new System.Collections.Generic.List<string>();
        
        // Persistent storage for incoming order banners (survives page navigation)
        private readonly System.Collections.Generic.List<IncomingOrderBannerItem> _persistentIncomingOrderBanners = new System.Collections.Generic.List<IncomingOrderBannerItem>();
        
        // Menu data cache (loaded once on login, refreshed only when requested)
        private System.Collections.Generic.List<string> _cachedCategories;
        private System.Collections.Generic.List<ProductItemModel> _cachedProducts;
        private MenuConfigModel _cachedMenuConfig;
        private List<FloorPlanModel>? _cachedFloorPlans;
        private List<FloorPlanCustomItemTypeModel>? _cachedFloorPlanCustomItemTypes;
        private bool _floorPlanLayoutEnabled;
        
        // Incoming order banner item class (moved from CashierHomeViewModel)
        public class IncomingOrderBannerItem
        {
            public string DisplayOrderId { get; set; }
            public string OrderJson { get; set; }
            public string PlatformName { get; set; }
            public string PlatformLogo { get; set; }
        }

        public static GlobalDataService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new GlobalDataService();
                        }
                    }
                }
                return _instance;
            }
        }

        // Last cash payment context for receipt printing
        public decimal? LastCashGiven { get; set; }
        public decimal? LastCashBalance { get; set; }

        private GlobalDataService()
        {
            _apiService = new ApiService();
            _localStorageService = new LocalStorageService();
            _settingsService = new SettingsService();
            _firebaseService = new FirebaseService();

            // Subscribe to Firebase collection changes
            _firebaseService.OnCollectionChanged += OnFirebaseCollectionChanged;

            try { _itemDiscountPresets = new ItemDiscountService().LoadPresets(); } catch { }
        }

        public CurrentUserModel CurrentUser
        {
            get => _currentUser;
            private set
            {
                _currentUser = value;
                CurrentUserChanged?.Invoke(value);
            }
        }

        public ShopModel ShopDetails
        {
            get => _shopDetails;
            private set
            {
                _shopDetails = value;
                ShopDetailsChanged?.Invoke(value);
            }
        }

        // Property to store order details for editing
        public OrderModel CurrentOrderForEdit
        {
            get => _currentOrderForEdit;
            set
            {
                _currentOrderForEdit = value;
                CurrentOrderForEditChanged?.Invoke(value);
            }
        }
        
        // Menu data cache properties (loaded once on login)
        public System.Collections.Generic.List<string> CachedCategories => _cachedCategories;
        public System.Collections.Generic.List<ProductItemModel> CachedProducts => _cachedProducts;
        public MenuConfigModel CachedMenuConfig => _cachedMenuConfig;
        /// <summary>Last known floor plans (clones) for instant Settings → Floor Plan tab display; null until first successful sync.</summary>
        public IReadOnlyList<FloorPlanModel>? CachedFloorPlans => _cachedFloorPlans;

        /// <summary>Merged floor-plan custom item type catalog from last successful GET <c>floor_plan</c> (or null before first load).</summary>
        public IReadOnlyList<FloorPlanCustomItemTypeModel>? CachedFloorPlanCustomItemTypes => _cachedFloorPlanCustomItemTypes;

        /// <summary>From floor plan config JSON; when true, Cashier dine-in table selection uses floor layouts.</summary>
        public bool IsFloorPlanLayoutEnabled => _floorPlanLayoutEnabled;

        public bool IsMenuDataLoaded => _cachedCategories != null && _cachedProducts != null && _cachedMenuConfig != null;

        public void UpdateCachedFloorPlans(
            IEnumerable<FloorPlanModel>? plans,
            bool? floorPlanLayoutEnabled = null,
            IReadOnlyList<FloorPlanCustomItemTypeModel>? customItemTypes = null)
        {
            _cachedFloorPlans = plans == null
                ? new List<FloorPlanModel>()
                : plans.Select(p => p.Clone()).ToList();
            if (floorPlanLayoutEnabled.HasValue)
            {
                _floorPlanLayoutEnabled = floorPlanLayoutEnabled.Value;
            }

            if (customItemTypes != null)
            {
                _cachedFloorPlanCustomItemTypes = customItemTypes.Select(t => t.Clone()).ToList();
            }
        }

        public event Action<CurrentUserModel> CurrentUserChanged;
        public event Action<ShopModel> ShopDetailsChanged;
        public event Action<OrderModel> CurrentOrderForEditChanged;
        public event Action MenuDataRefreshed; // Event to notify when menu data is refreshed
        // Raised when an order status is changed from dialogs so other views can update UI immediately
        public event Action<int, string> OrderStatusChanged;
        public event Action<int> IncomingOrdersCountChanged;
        // kitchen page refresh orders
        public event Action KitchenRefreshRequested;
        public void RequestKitchenRefresh()
        {
            try { KitchenRefreshRequested?.Invoke(); } catch { }
        }
        // Tables page refresh (e.g. after completing a table order via checkout)
        public event Action TablesRefreshRequested;
        public void RequestTablesRefresh()
        {
            try { TablesRefreshRequested?.Invoke(); } catch { }
        }
        // Raised to request any passive notification sounds to stop (e.g., when user views orders)
        public event Action StopIncomingOrderSoundRequested;
        // Configure Orders: when false, sidebar hides Live Orders button (Orders Page selected)
        private bool _useLiveOrdersPage;
        public event Action UseLiveOrdersPageChanged;
        public bool UseLiveOrdersPage
        {
            get => _useLiveOrdersPage;
            set
            {
                if (_useLiveOrdersPage == value) return;
                _useLiveOrdersPage = value;
                try { UseLiveOrdersPageChanged?.Invoke(); } catch { }
            }
        }

        // Auto-complete order config (from GetOrderConfig)
        public bool IsTakeawayAutoCompleteEnabled { get; set; }
        public int TakeawayAutoCompleteTimerMins { get; set; }
        public bool IsDineInAutoCompleteEnabled { get; set; }
        public int DineInAutoCompleteTimerMins { get; set; }
        public bool IsDeliveryAutoCompleteEnabled { get; set; }
        public int DeliveryAutoCompleteTimerMins { get; set; }
        public int IdleLogoutMinutes { get; set; } = 10;

        private System.Collections.Generic.List<decimal> _itemDiscountPresets = new System.Collections.Generic.List<decimal> { 10, 20 };
        public System.Collections.Generic.List<decimal> ItemDiscountPresets
        {
            get => _itemDiscountPresets;
            set => _itemDiscountPresets = value ?? new System.Collections.Generic.List<decimal> { 10, 20 };
        }

        //Call after saving auto-complete config from Settings so the background checker uses latest values
        public void UpdateAutoCompleteOrderConfig(bool isTakeaway, int takeawayMins, bool isDineIn, int dineInMins, bool isDelivery, int deliveryMins)
        {
            IsTakeawayAutoCompleteEnabled = isTakeaway;
            TakeawayAutoCompleteTimerMins = Math.Max(0, takeawayMins);
            IsDineInAutoCompleteEnabled = isDineIn;
            DineInAutoCompleteTimerMins = Math.Max(0, dineInMins);
            IsDeliveryAutoCompleteEnabled = isDelivery;
            DeliveryAutoCompleteTimerMins = Math.Max(0, deliveryMins);
        }

        public void RequestStopIncomingOrderSound()
        {
            try { StopIncomingOrderSoundRequested?.Invoke(); } catch { }
        }
        // Raised to show a lightweight alert overlay when away from Cashier
        //public event Action TransientNewOrderAlertRequested;

        // Expose current incoming orders count for UI initialization
        public int CurrentIncomingOrdersCount => _incomingOrdersCount;

        public void NotifyOrderStatusChanged(int orderId, string newStatus)
        {
            try
            {
                OrderStatusChanged?.Invoke(orderId, (newStatus ?? string.Empty).ToUpperInvariant());
            }
            catch { /* ignore subscriber errors */ }
        }
        
        private async void OnFirebaseCollectionChanged(string changeType, string documentId)
        {
            // Global handler for Firebase collection changes
            // This will be called from any page when the collection changes
            
            System.Diagnostics.Debug.WriteLine($"=== Firebase Collection Changed ===");
            System.Diagnostics.Debug.WriteLine($"Change Type: {changeType}");
            System.Diagnostics.Debug.WriteLine($"Document ID: {documentId}");
            System.Diagnostics.Debug.WriteLine($"Timestamp: {DateTime.Now}");
            System.Diagnostics.Debug.WriteLine("=== End Firebase Collection Changed ===");
            
            // OPTIMIZED APPROACH: Different behavior based on current page
            // - On Cashier page: Full API call with count update and UI refresh
            // - On other pages: Lightweight API call for count only
            await HandleFirebaseTriggerOptimizedAsync();
        }

        /// <summary>
        /// Optimized Firebase trigger handling based on current page
        /// </summary>
        private async Task HandleFirebaseTriggerOptimizedAsync()
        {
            try
            {
                // Check if user is currently on Cashier page
                bool isOnCashierPage = IsUserOnCashierPage();
                System.Diagnostics.Debug.WriteLine($"Firebase trigger: User on Cashier page = {isOnCashierPage}");
                
                if (isOnCashierPage)
                {
                    // On Cashier page: Full API call with UI updates
                    await CallLaravelOrdersApiAsync();
                }
                else
                {
                    // On other pages: Lightweight count-only update
                    await UpdateIncomingOrdersCountOnlyAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in optimized Firebase trigger handling: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the user is currently on the Cashier page
        /// </summary>
        private bool IsUserOnCashierPage()
        {
            try
            {
                if (Application.Current?.MainWindow == null) return false;
                
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow?.MainFrame?.Content == null) return false;
                
                return mainWindow.MainFrame.Content is CashierHomePage;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lightweight API call that only updates the count without full UI processing
        /// Used when user is not on Cashier page
        /// </summary>
        private async Task UpdateIncomingOrdersCountOnlyAsync()
        {
            try
            {
                // Check if we have shop details
                if (_shopDetails == null)
                {
                    System.Diagnostics.Debug.WriteLine("Shop details not available for count-only update");
                    return;
                }

                // Get Laravel bearer token
                var laravelService = new LaravelPassportService();
                var bearerToken = await laravelService.GetAccessTokenAsync();
                
                if (string.IsNullOrEmpty(bearerToken))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to get Laravel bearer token for count-only update");
                    return;
                }

                // Build the API URL with franchise and shop parameters
                var apiUrl = $"/api/v1/admin/orders/status/CREATED?franchise={_shopDetails.FranchiseId}&shop={_shopDetails.Id}&paginate=false";
                
                // Get tenant code from settings
                var (tenantCode, _, _) = _settingsService.LoadSettings();
                
                // Call the Laravel API using the existing method in ApiService
                var apiService = new ApiService();
                var response = await apiService.CallLaravelApiAsync(apiUrl, bearerToken, tenantCode, System.Net.Http.HttpMethod.Get);
                
                System.Diagnostics.Debug.WriteLine($"Count-only API Response: {response}");
                
                // Parse the JSON response and update count only
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(response);
                    
                    int orderCount = 0;
                    
                    // Check if the response has a 'data' property (common Laravel API structure)
                    if (doc.RootElement.TryGetProperty("data", out var dataElement))
                    {
                        // Check if data has an 'orders' property (nested structure)
                        if (dataElement.TryGetProperty("orders", out var ordersElement))
                        {
                            if (ordersElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                orderCount = ordersElement.GetArrayLength();
                            }
                        }
                        else
                        {
                            // If data is directly an array (fallback for other API structures)
                            if (dataElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                orderCount = dataElement.GetArrayLength();
                            }
                        }
                    }
                    else
                    {
                        // If no 'data' property, check if root is an array
                        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            orderCount = doc.RootElement.GetArrayLength();
                        }
                    }
                    
                    // Update count only (no UI processing)
                    UpdateIncomingOrdersCount(orderCount);
                    System.Diagnostics.Debug.WriteLine($"Count-only update: Set count to {orderCount}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing count-only API response: {ex.Message}");
                    UpdateIncomingOrdersCount(0);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in count-only update: {ex.Message}");
                // Don't update count on error - keep existing count
            }
        }

        private void UpdateIncomingOrdersCount(int newCount)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"UpdateIncomingOrdersCount: Setting count to {newCount}");
                _incomingOrdersCount = newCount;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"UpdateIncomingOrdersCount: Invoking event with count {_incomingOrdersCount}");
                    IncomingOrdersCountChanged?.Invoke(_incomingOrdersCount);
                });
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"UpdateIncomingOrdersCount: Exception caught: {ex.Message}");
                /* ignore subscriber errors */ 
            }
        }

        /// <summary>
        /// On Cashier, the sidebar badge includes persistent banner rows plus any popup + queued CREATED orders
        /// not yet moved to the banner (e.g. when several orders arrive at once).
        /// </summary>
        private void RecalculateCashierIncomingOrdersBadgeCount()
        {
            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null) return;
                void Core()
                {
                    if (!IsUserOnCashierPage()) return;
                    int n = _persistentIncomingOrderBanners.Count;
                    if (Application.Current.MainWindow is MainWindow mainWin
                        && mainWin.MainFrame.Content is CashierHomePage cashierPage
                        && cashierPage.DataContext is ViewModels.CashierHomeViewModel vm)
                    {
                        n += vm.GetCashierIncomingOrdersPendingSurfaceCount();
                    }
                    else if (!string.IsNullOrWhiteSpace(_currentNewOrderAlertDisplayOrderId))
                    {
                        var popupId = _currentNewOrderAlertDisplayOrderId;
                        bool inPersistent = _persistentIncomingOrderBanners.Any(i =>
                            string.Equals(i?.DisplayOrderId, popupId, StringComparison.OrdinalIgnoreCase));
                        if (!inPersistent) n += 1;
                    }
                    UpdateIncomingOrdersCount(n);
                }
                if (dispatcher.CheckAccess()) Core();
                else dispatcher.Invoke(Core);
            }
            catch { /* ignore */ }
        }

        /// <summary>Recomputes sidebar incoming-order count on Cashier (popup + queue + persistent).</summary>
        public void RefreshCashierIncomingOrdersBadgeCount()
        {
            RecalculateCashierIncomingOrdersBadgeCount();
        }

        private static System.Collections.Generic.List<string> CollectOrderJsonStringsFromArray(System.Text.Json.JsonElement arrayElement)
        {
            var list = new System.Collections.Generic.List<string>();
            if (arrayElement.ValueKind != System.Text.Json.JsonValueKind.Array) return list;
            foreach (var ord in arrayElement.EnumerateArray())
                list.Add(ord.ToString());
            return list;
        }

        /// <summary>Cashier VM calls this when a new-order alert becomes visible (badge / tracking).</summary>
        public void NotifyNewOrderAlertShowing(string orderJson)
        {
            TrySetCurrentPopupIdFromOrderJson(orderJson);
        }

        /// <summary>After batching alerts into the banner, clear popup-only tracking.</summary>
        public void ClearNewOrderAlertPopupTracking()
        {
            try
            {
                _currentNewOrderAlertDisplayOrderId = null;
                RecalculateCashierIncomingOrdersBadgeCount();
            }
            catch { /* ignore */ }
        }

        /// <summary>
        /// Full API call with UI processing - used when user is on Cashier page
        /// </summary>
        private async Task CallLaravelOrdersApiAsync()
        {
            try
            {
                // Check if we have shop details
                if (_shopDetails == null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show("Shop details not available. Cannot call Laravel API.", 
                            "API Error", 
                            System.Windows.MessageBoxButton.OK, 
                            System.Windows.MessageBoxImage.Warning);
                    });
                    return;
                }

                // Get Laravel bearer token
                var laravelService = new LaravelPassportService();
                var bearerToken = await laravelService.GetAccessTokenAsync();
                
                if (string.IsNullOrEmpty(bearerToken))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to get Laravel bearer token.");
                    return;
                }

                // Build the API URL with franchise and shop parameters
                var apiUrl = $"/api/v1/admin/orders/status/CREATED?franchise={_shopDetails.FranchiseId}&shop={_shopDetails.Id}&paginate=false";
                
                // Get tenant code from settings
                var (tenantCode, _, _) = _settingsService.LoadSettings();
                
                // Call the Laravel API using the existing method in ApiService
                var apiService = new ApiService();
                var response = await apiService.CallLaravelApiAsync(apiUrl, bearerToken, tenantCode, System.Net.Http.HttpMethod.Get);
                
                // Log the API response to debug output
                System.Diagnostics.Debug.WriteLine("=== Laravel API Response ===");
                System.Diagnostics.Debug.WriteLine($"URL: {apiUrl}");
                System.Diagnostics.Debug.WriteLine($"Tenant Code: {tenantCode}");
                System.Diagnostics.Debug.WriteLine($"Response: {response}");
                System.Diagnostics.Debug.WriteLine("=== End Laravel API Response ===");
                
                // Parse the JSON response to check if there are orders
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(response);
                    
                    // Check if the response has a 'data' property (common Laravel API structure)
                    if (doc.RootElement.TryGetProperty("data", out var dataElement))
                    {
                        // Check if data has an 'orders' property (nested structure)
                        if (dataElement.TryGetProperty("orders", out var ordersElement))
                        {
                            // If orders is an array and has items, show new order alert
                            if (ordersElement.ValueKind == System.Text.Json.JsonValueKind.Array && ordersElement.GetArrayLength() > 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"Found {ordersElement.GetArrayLength()} orders in data.orders - showing new order alert");
                                // Reconcile incoming banner with the latest API list
                                try
                                {
                                    var latestIds = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                    foreach (var ord in ordersElement.EnumerateArray())
                                    {
                                        if (ord.TryGetProperty("display_order_id", out var dispEl))
                                        {
                                            var id = dispEl.GetString();
                                            if (!string.IsNullOrWhiteSpace(id)) latestIds.Add(id);
                                        }
                                    }
                                    ReconcileIncomingOrderBanners(latestIds);
                                    // If the currently open popup's id is not present anymore, close it
                                    if (!string.IsNullOrEmpty(_currentNewOrderAlertDisplayOrderId) && !latestIds.Contains(_currentNewOrderAlertDisplayOrderId))
                                    {
                                        CloseNewOrderAlertPopup();
                                    }
                                }
                                catch { }
                                
                                var orderJsonListForCashier = CollectOrderJsonStringsFromArray(ordersElement);
                                if (orderJsonListForCashier.Count > 0)
                                    System.Diagnostics.Debug.WriteLine($"First order ID: {ordersElement[0].GetProperty("id").GetString()}");

                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    System.Diagnostics.Debug.WriteLine("Attempting to sync new order alert(s) from API...");
                                    if (System.Windows.Application.Current.MainWindow is MainWindow mainWin
                                        && mainWin.MainFrame.Content is CashierHomePage cashierPage
                                        && cashierPage.DataContext is ViewModels.CashierHomeViewModel viewModel)
                                    {
                                        viewModel.SyncIncomingOrderAlertsFromApi(orderJsonListForCashier);
                                    }
                                    else
                                    {
                                        var mw = System.Windows.Application.Current.MainWindow as MainWindow;
                                        System.Diagnostics.Debug.WriteLine($"Current page is not CashierHomePage. Frame content: {mw?.MainFrame?.Content?.GetType().Name ?? "null"}");
                                        var json = ordersElement[0].ToString();
                                        if (!string.IsNullOrWhiteSpace(json))
                                            AddIncomingOrderToPersistentBanner(json);
                                    }
                                });
                            }
                            else
                            {
                                // No CREATED orders - clear banners and count
                                HideIncomingOrderBannerUI();
                                // Also close any open new order alert popup when none remain
                                CloseNewOrderAlertPopup();
                                // Close OrderDetailsDialog if open (no orders means all are no longer CREATED)
                                CloseOrderDetailsDialogIfOrderNotCreated(new System.Collections.Generic.HashSet<string>());
                                System.Diagnostics.Debug.WriteLine("No orders found in data.orders (empty array) - no action taken");
                            }
                        }
                        else
                        {
                            // If data is directly an array (fallback for other API structures)
                            if (dataElement.ValueKind == System.Text.Json.JsonValueKind.Array && dataElement.GetArrayLength() > 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"Found {dataElement.GetArrayLength()} orders in data array - showing new order alert");
                                // Reconcile incoming banner with the latest API list
                                try
                                {
                                    var latestIds = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                    foreach (var ord in dataElement.EnumerateArray())
                                    {
                                        if (ord.TryGetProperty("display_order_id", out var dispEl))
                                        {
                                            var id = dispEl.GetString();
                                            if (!string.IsNullOrWhiteSpace(id)) latestIds.Add(id);
                                        }
                                    }
                                    ReconcileIncomingOrderBanners(latestIds);
                                    // If the currently open popup's id is not present anymore, close it
                                    if (!string.IsNullOrEmpty(_currentNewOrderAlertDisplayOrderId) && !latestIds.Contains(_currentNewOrderAlertDisplayOrderId))
                                    {
                                        CloseNewOrderAlertPopup();
                                    }
                                }
                                catch { }
                                
                                var orderJsonListDataArray = CollectOrderJsonStringsFromArray(dataElement);
                                if (orderJsonListDataArray.Count > 0)
                                    System.Diagnostics.Debug.WriteLine($"First order ID: {dataElement[0].GetProperty("id").GetString()}");

                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    System.Diagnostics.Debug.WriteLine("Attempting to sync new order alert(s) from API (data array)...");
                                    if (System.Windows.Application.Current.MainWindow is MainWindow mainWin
                                        && mainWin.MainFrame.Content is CashierHomePage cashierPage
                                        && cashierPage.DataContext is ViewModels.CashierHomeViewModel viewModel)
                                    {
                                        viewModel.SyncIncomingOrderAlertsFromApi(orderJsonListDataArray);
                                    }
                                    else
                                    {
                                        var mw = System.Windows.Application.Current.MainWindow as MainWindow;
                                        System.Diagnostics.Debug.WriteLine($"Current page is not CashierHomePage. Frame content: {mw?.MainFrame?.Content?.GetType().Name ?? "null"}");
                                        var json = dataElement[0].ToString();
                                        if (!string.IsNullOrWhiteSpace(json))
                                            AddIncomingOrderToPersistentBanner(json);
                                    }
                                });
                            }
                            else
                            {
                                // No CREATED orders - clear banners and count
                                HideIncomingOrderBannerUI();
                                // Also close any open new order alert popup when none remain
                                CloseNewOrderAlertPopup();
                                // Close OrderDetailsDialog if open (no orders means all are no longer CREATED)
                                CloseOrderDetailsDialogIfOrderNotCreated(new System.Collections.Generic.HashSet<string>());
                                System.Diagnostics.Debug.WriteLine("No orders found in data (empty array) - no action taken");
                            }
                        }
                    }
                    else
                    {
                        // If no 'data' property, check if root is an array
                        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"Found {doc.RootElement.GetArrayLength()} orders in root array - showing new order alert");
                            // Reconcile incoming banner with the latest API list
                            try
                            {
                                var latestIds = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                foreach (var ord in doc.RootElement.EnumerateArray())
                                {
                                    if (ord.TryGetProperty("display_order_id", out var dispEl))
                                    {
                                        var id = dispEl.GetString();
                                        if (!string.IsNullOrWhiteSpace(id)) latestIds.Add(id);
                                    }
                                }
                                ReconcileIncomingOrderBanners(latestIds);
                                // If the currently open popup's id is not present anymore, close it
                                if (!string.IsNullOrEmpty(_currentNewOrderAlertDisplayOrderId) && !latestIds.Contains(_currentNewOrderAlertDisplayOrderId))
                                {
                                    CloseNewOrderAlertPopup();
                                }
                                // If the currently open OrderDetailsDialog's order is not in CREATED anymore, close it
                                CloseOrderDetailsDialogIfOrderNotCreated(latestIds);
                            }
                            catch { }
                            
                            var orderJsonListRoot = CollectOrderJsonStringsFromArray(doc.RootElement);
                            if (orderJsonListRoot.Count > 0)
                                System.Diagnostics.Debug.WriteLine($"First order ID: {doc.RootElement[0].GetProperty("id").GetString()}");

                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                System.Diagnostics.Debug.WriteLine("Attempting to sync new order alert(s) from API (root array)...");
                                if (System.Windows.Application.Current.MainWindow is MainWindow mainWin
                                    && mainWin.MainFrame.Content is CashierHomePage cashierPage
                                    && cashierPage.DataContext is ViewModels.CashierHomeViewModel viewModel)
                                {
                                    viewModel.SyncIncomingOrderAlertsFromApi(orderJsonListRoot);
                                }
                                else
                                {
                                    var json = doc.RootElement[0].ToString();
                                    if (!string.IsNullOrWhiteSpace(json))
                                        AddIncomingOrderToPersistentBanner(json);
                                }
                            });
                        }
                        else
                        {
                            // No CREATED orders - clear banners and count
                            HideIncomingOrderBannerUI();
                            // Also close any open new order alert popup when none remain
                            CloseNewOrderAlertPopup();
                            // Close OrderDetailsDialog if open (no orders means all are no longer CREATED)
                            CloseOrderDetailsDialogIfOrderNotCreated(new System.Collections.Generic.HashSet<string>());
                            System.Diagnostics.Debug.WriteLine("No orders found (empty array) - no action taken");
                        }
                    }
                }
                catch (System.Text.Json.JsonException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing JSON response: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                // Check if it's a network connectivity issue
                var networkService = POS_UI.Services.NetworkConnectivityService.Instance;
                if (!networkService.IsConnected)
                {
                    // Don't show error message if there's no internet connection
                    // The internet connection dialog will handle this
                    return;
                }
                
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show($"Error calling Laravel API: {ex.Message}", 
                        "API Error", 
                        System.Windows.MessageBoxButton.OK, 
                        System.Windows.MessageBoxImage.Error);
                });
            }
        }

        // Remove any persistent incoming banners that are not present in the latest API results
        private void ReconcileIncomingOrderBanners(System.Collections.Generic.HashSet<string> latestDisplayOrderIds)
        {
            try
            {
                if (latestDisplayOrderIds == null) return;
                // Remove items not in latest set
                for (int i = _persistentIncomingOrderBanners.Count - 1; i >= 0; i--)
                {
                    var item = _persistentIncomingOrderBanners[i];
                    if (string.IsNullOrWhiteSpace(item?.DisplayOrderId) || !latestDisplayOrderIds.Contains(item.DisplayOrderId))
                    {
                        _persistentIncomingOrderBanners.RemoveAt(i);
                    }
                }
                // Include NewOrderAlertPopup order in badge (not yet in persistent list)
                RecalculateCashierIncomingOrdersBadgeCount();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (System.Windows.Application.Current.MainWindow is MainWindow mainWin && mainWin.MainFrame.Content is CashierHomePage cashierPage)
                    {
                        if (cashierPage.DataContext is ViewModels.CashierHomeViewModel vm)
                        {
                            vm.RefreshIncomingOrdersBannerFromPersistentStorage();
                        }
                    }
                });
                
                // Also check and close OrderDetailsDialog if the order is no longer in CREATED
                CloseOrderDetailsDialogIfOrderNotCreated(latestDisplayOrderIds);
            }
            catch { }
        }

        public void ShowOrderDetailsDialog(string orderJsonData)
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Parse the order JSON data
                    using var doc = System.Text.Json.JsonDocument.Parse(orderJsonData);
                    var order = doc.RootElement;
                    
                    // Create the order details view model
                    var orderDetailsVm = new ViewModels.OrderDetailsDialogViewModel();
                    
                    // Extract order details from JSON
                    orderDetailsVm.OrderNumber = order.GetProperty("display_order_id").GetString();
                    // Track the currently visible alert popup's display id
                    _currentNewOrderAlertDisplayOrderId = orderDetailsVm.OrderNumber;
                    // Track the currently open OrderDetailsDialog's display order id
                    _currentOrderDetailsDialogDisplayOrderId = orderDetailsVm.OrderNumber;
                    orderDetailsVm.DeliveryType = order.GetProperty("delivery_type").GetString();
                    orderDetailsVm.OrderTypeTime = $"{order.GetProperty("delivery_type").GetString()} - {order.GetProperty("delivery_time").GetString()}";
                    orderDetailsVm.Contact = order.GetProperty("delivergate_customer").GetProperty("phone").GetString();
                    // Contact access code (UberEats)
                    orderDetailsVm.ContactAccessCode = order.GetProperty("contact_access_code").GetString();
                    
                    orderDetailsVm.Platform = order.GetProperty("platform").GetString();
                    // Table Order method (e.g., QR, Waiter) shown only for Table order platform
                    if (string.Equals(orderDetailsVm.Platform, "Table order", StringComparison.OrdinalIgnoreCase))
                    {
                        if (order.TryGetProperty("table_order_method", out var tom) && tom.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            orderDetailsVm.TableOrderMethod = tom.GetString();
                        }
                    }
                    //MessageBox.Show($"Table Order Method: {orderDetailsVm.TableOrderMethod}");
                    //orderDetailsVm.PlatformName = order.GetProperty("platform_name").GetString();
                    orderDetailsVm.PlatformId = (order.TryGetProperty("platform_id", out var platformId)
                        && platformId.ValueKind == System.Text.Json.JsonValueKind.Number)
                        ? platformId.GetInt32()
                        : 0;
                    orderDetailsVm.TableId = order.TryGetProperty("table_id", out var tableId) ? tableId.GetString() : "N/A";
                    orderDetailsVm.SelectedTableName = order.TryGetProperty("table_name", out var tableName) ? tableName.GetString() : "";
                    orderDetailsVm.TableOrderMethodId = (order.TryGetProperty("table_order_method_id", out var tableOrderMethodId)
                        && tableOrderMethodId.ValueKind == System.Text.Json.JsonValueKind.Number)
                        ? tableOrderMethodId.GetInt32()
                        : 0;
                    
                    // Extract is_table_order flag
                    orderDetailsVm.IsTableOrderFlag = (order.TryGetProperty("is_table_order", out var isTableOrder)
                        && isTableOrder.ValueKind == System.Text.Json.JsonValueKind.Number)
                        ? isTableOrder.GetInt32()
                        : 0;

                    // Incoming order's order_session_id (for table selection: enable reserved tables with same session)
                    orderDetailsVm.OrderSessionId = (order.TryGetProperty("order_session_id", out var orderSessionIdEl)
                        && orderSessionIdEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                        ? orderSessionIdEl.GetInt32()
                        : (int?)null;

                    //MessageBox.Show($"Is Table Order Flag: {orderDetailsVm.IsTableOrderFlag}");
                    
                    // Extract customer name
                    if (order.TryGetProperty("customer_name", out var customerName))
                    {
                        orderDetailsVm.CustomerName = customerName.GetString();
                    }
                    else if (order.TryGetProperty("delivergate_customer", out var customerObj))
                    {
                        // Try to get customer name from delivergate_customer object
                        if (customerObj.TryGetProperty("first_name", out var firstName) && customerObj.TryGetProperty("last_name", out var lastName))
                        {
                            var first = firstName.GetString() ?? "";
                            var last = lastName.GetString() ?? "";
                            orderDetailsVm.CustomerName = $"{first} {last}".Trim();
                        }
                    }
                    
                    // Extract delivery address if present
                    // Priority: shipping_details -> delivergate_customer.address
                    if (order.TryGetProperty("shipping_details", out var shippingDetails) && shippingDetails.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        if (shippingDetails.TryGetProperty("address_line_1", out var s1) && s1.ValueKind != System.Text.Json.JsonValueKind.Null)
                        {
                            orderDetailsVm.AddressLine1 = s1.GetString();
                        }
                        if (shippingDetails.TryGetProperty("address_line_2", out var s2) && s2.ValueKind != System.Text.Json.JsonValueKind.Null)
                        {
                            orderDetailsVm.AddressLine2 = s2.GetString();
                        }
                    }
                    else if (order.TryGetProperty("delivergate_customer", out var dgc) && dgc.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        // Fallback: use consolidated address field if provided
                        if (dgc.TryGetProperty("address", out var addrProp) && addrProp.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var fullAddress = addrProp.GetString();
                            if (!string.IsNullOrWhiteSpace(fullAddress))
                            {
                                orderDetailsVm.AddressLine1 = fullAddress;
                                orderDetailsVm.AddressLine2 = null; // ensure old value doesn't linger
                            }
                        }
                    }
                   

                    // Prefer platform_logo if available; fallback to delivery_platform_logo
                    if (order.TryGetProperty("platform_logo", out var pltLogo) && pltLogo.ValueKind != System.Text.Json.JsonValueKind.Null)
                    {
                        orderDetailsVm.PlatformLogoUrl = pltLogo.GetString();
                    }
                    
                    // Extract payment status (check for refunds)
                    if (order.TryGetProperty("payment_status", out var paymentStatus))
                    {
                        var statusText = paymentStatus.GetString() ?? "";
                        decimal orderTotal = 0m;
                        decimal refundBal = 0m;
                        if (order.TryGetProperty("total", out var totalProp) && totalProp.ValueKind == System.Text.Json.JsonValueKind.Number)
                            orderTotal = totalProp.GetDecimal();
                        if (order.TryGetProperty("refund_balance", out var rbProp) && rbProp.ValueKind == System.Text.Json.JsonValueKind.Number)
                            refundBal = rbProp.GetDecimal();

                        if (orderTotal > 0 && refundBal <= 0)
                            orderDetailsVm.PaymentStatus = "Refunded";
                        else if (orderTotal > 0 && refundBal < orderTotal)
                            orderDetailsVm.PaymentStatus = "Partially Refunded";
                        else
                            orderDetailsVm.PaymentStatus = statusText;
                    }
                    
                    orderDetailsVm.OrderNotes = order.GetProperty("note").GetString() ?? "No notes";
                    // Subtotal can be number or string or null
                    orderDetailsVm.Subtotal = 0m;
                    if (order.TryGetProperty("sub_total", out var subTotalEl))
                    {
                        if (subTotalEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            orderDetailsVm.Subtotal = subTotalEl.GetDecimal();
                        }
                        else if (subTotalEl.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var parsedSub = 0m;
                            decimal.TryParse(subTotalEl.GetString()?.Replace(" ", ""), out parsedSub);
                            orderDetailsVm.Subtotal = parsedSub;
                        }
                    }
                    
                    // Set BOGO discount
                    if (order.TryGetProperty("bogo_discount", out var bogoDisc))
                    {
                        if (bogoDisc.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            decimal.TryParse(bogoDisc.GetString().Replace(" ", ""), out var bogoVal);
                            orderDetailsVm.BogoDiscount = bogoVal;
                        }
                        else if (bogoDisc.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            orderDetailsVm.BogoDiscount = bogoDisc.GetDecimal();
                        }
                    }
                    
                    // Parse shop_fees array to display individual fees
                    if (order.TryGetProperty("shop_fees", out var shopFeesArray) && shopFeesArray.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        decimal totalShopFee = 0;
                        foreach (var shopFee in shopFeesArray.EnumerateArray())
                        {
                            if (shopFee.TryGetProperty("amount", out var amount))
                            {
                                decimal feeAmount = 0m;
                                if (amount.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    decimal.TryParse(amount.GetString().Replace(" ", ""), out feeAmount);
                                }
                                else if (amount.ValueKind == System.Text.Json.JsonValueKind.Number)
                                {
                                    feeAmount = amount.GetDecimal();
                                }
                                
                                if (feeAmount > 0)
                                {
                                    var feeName = shopFee.TryGetProperty("fee_name", out var nameProp) ? nameProp.GetString() : "Fee";
                                    var feeType = shopFee.TryGetProperty("fee_type", out var typeProp) ? typeProp.GetString() : "VALUE";
                                    var feeValue = shopFee.TryGetProperty("fee", out var feeProp) ? feeProp.GetString() : "0";
                                    
                                    var bracket = feeType?.Trim().ToUpperInvariant() == "PERCENTAGE" ? ($"{feeValue}%") : "value";
                                    var label = $"{feeName}({bracket})";
                                    
                                    orderDetailsVm.ShopFeeRows.Add(new ViewModels.OrderDetailsDialogViewModel.ShopFeeDisplayModel 
                                    { 
                                        Label = label, 
                                        Amount = feeAmount 
                                    });
                                    
                                    totalShopFee += feeAmount;
                                }
                            }
                        }
                        orderDetailsVm.ShopFee = totalShopFee;
                    }
                    // Fallback: Set shop fee (from shopFee array) for backward compatibility
                    else if (order.TryGetProperty("shopFee", out var shopFeeArray) && shopFeeArray.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        decimal totalShopFee = 0;
                        foreach (var shopFee in shopFeeArray.EnumerateArray())
                        {
                            decimal feeAmount = 0m;
                            if (shopFee.TryGetProperty("amount", out var amount))
                            {
                                if (amount.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    decimal.TryParse(amount.GetString().Replace(" ", ""), out feeAmount);
                                }
                                else if (amount.ValueKind == System.Text.Json.JsonValueKind.Number)
                                {
                                    feeAmount = amount.GetDecimal();
                                }
                            }

                            if (feeAmount > 0)
                            {
                                totalShopFee += feeAmount;
                                var feeName = shopFee.TryGetProperty("name", out var nameProp2) ? (nameProp2.GetString() ?? "Fee") : "Fee";
                                // No type provided in this legacy array; default to (value)
                                var label = $"{feeName}(value)";
                                orderDetailsVm.ShopFeeRows.Add(new ViewModels.OrderDetailsDialogViewModel.ShopFeeDisplayModel
                                {
                                    Label = label,
                                    Amount = feeAmount
                                });
                            }
                        }
                        orderDetailsVm.ShopFee = totalShopFee;
                    }
                    
                    // Set delivery charges
                    if (order.TryGetProperty("shipping_total", out var shippingTotal))
                    {
                        if (shippingTotal.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            decimal.TryParse(shippingTotal.GetString().Replace(" ", ""), out var shippingVal);
                            orderDetailsVm.DeliveryCharges = shippingVal;
                        }
                        else if (shippingTotal.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            orderDetailsVm.DeliveryCharges = shippingTotal.GetDecimal();
                        }
                    }
                    
                    // Set reward discount (loyalty redeemed amount)
                    if (order.TryGetProperty("loyalty", out var loyaltyObj) && loyaltyObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        if (loyaltyObj.TryGetProperty("redeemed_amount", out var redeemedAmount))
                        {
                            if (redeemedAmount.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                decimal.TryParse(redeemedAmount.GetString().Replace(" ", ""), out var rewardVal);
                                orderDetailsVm.RewardDiscount = rewardVal / 100m;
                            }
                            else if (redeemedAmount.ValueKind == System.Text.Json.JsonValueKind.Number)
                            {
                                orderDetailsVm.RewardDiscount = redeemedAmount.GetDecimal() / 100m;
                            }
                        }
                    }
                    
                    // Set tips
                    if (order.TryGetProperty("tip", out var tip))
                    {
                        if (tip.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            decimal.TryParse(tip.GetString().Replace(" ", ""), out var tipVal);
                            orderDetailsVm.Tips = tipVal;
                        }
                        else if (tip.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            orderDetailsVm.Tips = tip.GetDecimal();
                        }
                    }
                    
                    // Set tip percentage
                    if (order.TryGetProperty("tip_percentage", out var tipPercentage))
                    {
                        if (tipPercentage.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            decimal.TryParse(tipPercentage.GetString(), out var tipPercVal);
                            orderDetailsVm.TipPercentage = tipPercVal;
                        }
                        else if (tipPercentage.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            orderDetailsVm.TipPercentage = tipPercentage.GetDecimal();
                        }
                    }
                    
                    // Set order level discount_percentage and discount separately
                    if (order.TryGetProperty("discount_percentage", out var discountPercent))
                    {
                        decimal discountPercentValue = 0m;
                        if (discountPercent.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            decimal.TryParse(discountPercent.GetString(), out discountPercentValue);
                        }
                        else if (discountPercent.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            discountPercentValue = discountPercent.GetDecimal();
                        }
                        orderDetailsVm.DiscountPercentage = discountPercentValue;
                    }

                    if (order.TryGetProperty("discount", out var orderDisc))
                    {
                        decimal discountValue = 0m;
                        if (orderDisc.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            decimal.TryParse(orderDisc.GetString().Replace(" ", ""), out discountValue);
                        }
                        else if (orderDisc.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            discountValue = orderDisc.GetDecimal();
                        }
                        orderDetailsVm.OrderDiscount = discountValue;
                    }

                    // Parse discount mode
                    if (order.TryGetProperty("discount_mode_applied", out var discountMode))
                    {
                        string mode = discountMode.GetString() ?? "percentage";
                        orderDetailsVm.DiscountModeApplied = mode;
                    }
                    else
                    {
                        // Default to percentage mode for backward compatibility
                        orderDetailsVm.DiscountModeApplied = "percentage";
                    }
                    // Total can be number or string or null
                    orderDetailsVm.Total = 0m;
                    if (order.TryGetProperty("total_amount", out var totalAmountEl))
                    {
                        if (totalAmountEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            orderDetailsVm.Total = totalAmountEl.GetDecimal();
                        }
                        else if (totalAmountEl.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var parsedTotal = 0m;
                            decimal.TryParse(totalAmountEl.GetString()?.Replace(" ", ""), out parsedTotal);
                            orderDetailsVm.Total = parsedTotal;
                        }
                    }
                    
                    // Extract items with discount, notes, and modifiers (including nested)
                    var items = order.GetProperty("items");
                    foreach (var item in items.EnumerateArray())
                    {
                        var vmItem = new ViewModels.OrderItemViewModel
                        {
                            Quantity = (item.TryGetProperty("quantity", out var qtyEl)
                                ? (qtyEl.ValueKind == System.Text.Json.JsonValueKind.Number
                                    ? qtyEl.GetInt32()
                                    : (int.TryParse(qtyEl.GetString(), out var qv) ? qv : 1))
                                : 1),
                            Name = item.GetProperty("item_name").GetString(),
                            Notes = item.TryGetProperty("note", out var noteEl) && noteEl.ValueKind != System.Text.Json.JsonValueKind.Null ? noteEl.GetString() : string.Empty,
                            // Keep Price for compatibility; but the modal will show ApiItemTotal directly
                            Price = decimal.TryParse(item.GetProperty("display_price").GetString().Replace(" ", ""), out var p) ? p : 0m,
                            ApiItemTotal = (item.TryGetProperty("total", out var totalEl)
                                ? (totalEl.ValueKind == System.Text.Json.JsonValueKind.Number
                                    ? Math.Round(totalEl.GetDecimal() / 100m, 2, MidpointRounding.AwayFromZero)
                                    : (decimal.TryParse(totalEl.GetString(), out var tv) ? Math.Round(tv / 100m, 2, MidpointRounding.AwayFromZero) : 0m))
                                : 0m),
                            DiscountAmount = item.TryGetProperty("discount_amount", out var disEl) 
                                ? (disEl.ValueKind == System.Text.Json.JsonValueKind.Number 
                                    ? Math.Round(disEl.GetDecimal() / 100m, 2, MidpointRounding.AwayFromZero) 
                                    : (decimal.TryParse(disEl.GetString(), out var dsv) ? Math.Round(dsv / 100m, 2, MidpointRounding.AwayFromZero) : 0m))
                                : 0m
                        };

                        // Parse modifiers recursively into ModifierDetailsForDisplay
                        if (item.TryGetProperty("modifiers", out var modifiersEl) && modifiersEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var mod in modifiersEl.EnumerateArray())
                            {
                                ParseModifierGroup(mod, vmItem.ModifierDetailsForDisplay, "");
                            }
                        }

                        // Parse printer_groups if they exist
                        if (item.TryGetProperty("printer_groups", out var printerGroupsEl) && printerGroupsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            var printerGroups = new List<Models.PrinterGroupModel>();
                            foreach (var printerGroupElement in printerGroupsEl.EnumerateArray())
                            {
                                try
                                {
                                    var printerGroup = new Models.PrinterGroupModel
                                    {
                                        Id = printerGroupElement.TryGetProperty("id", out var pgIdElement) && pgIdElement.ValueKind == System.Text.Json.JsonValueKind.Number 
                                            ? pgIdElement.GetInt32() 
                                            : 0,
                                        Name = printerGroupElement.TryGetProperty("name", out var pgNameElement) 
                                            ? pgNameElement.GetString() 
                                            : null,
                                        Description = printerGroupElement.TryGetProperty("description", out var pgDescElement) 
                                            ? pgDescElement.GetString() 
                                            : null,
                                        Status = printerGroupElement.TryGetProperty("status", out var pgStatusElement) && 
                                                (pgStatusElement.ValueKind == System.Text.Json.JsonValueKind.True || 
                                                 (pgStatusElement.ValueKind == System.Text.Json.JsonValueKind.Number && pgStatusElement.GetInt32() == 1) ||
                                                 (pgStatusElement.ValueKind == System.Text.Json.JsonValueKind.String && string.Equals(pgStatusElement.GetString(), "1", StringComparison.OrdinalIgnoreCase))),
                                        CreatedAt = printerGroupElement.TryGetProperty("created_at", out var createdProp) && createdProp.ValueKind == System.Text.Json.JsonValueKind.String 
                                            ? DateTime.Parse(createdProp.GetString()) 
                                            : DateTime.MinValue,
                                        UpdatedAt = printerGroupElement.TryGetProperty("updated_at", out var updatedProp) && updatedProp.ValueKind == System.Text.Json.JsonValueKind.String 
                                            ? DateTime.Parse(updatedProp.GetString()) 
                                            : DateTime.MinValue
                                    };
                                    printerGroups.Add(printerGroup);
                                }
                                catch
                                {
                                    // Ignore malformed printer group entries
                                }
                            }
                            vmItem.PrinterGroups = printerGroups;
                        }

                        orderDetailsVm.Items.Add(vmItem);
                    }
                    
                    // Extract order ID before disposing the JSON document
                    var orderId = order.GetProperty("id").GetString();
                    int orderId2 = 0;
                    if (order.TryGetProperty("order_id", out var orderIdEl))
                    {
                        if (orderIdEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            orderId2 = orderIdEl.GetInt32();
                        }
                        else if (orderIdEl.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(orderIdEl.GetString(), out var ordParsed))
                        {
                            orderId2 = ordParsed;
                        }
                    }
                    
                    // Add Accept and Reject commands with loading state management
                    orderDetailsVm.AcceptCommand = new ViewModels.RelayCommand(async () =>
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"Accepting order: {orderDetailsVm.OrderNumber}");
                            orderDetailsVm.IsAccepting = true;
                            orderDetailsVm.IsLoading = true;
                            //MessageBox.Show($" Table ID: {orderDetailsVm.TableId}, Table Order Method ID: {orderDetailsVm.TableOrderMethodId}, Platform: {orderDetailsVm.Platform}, Selected Table Name: {orderDetailsVm.SelectedTableName}");
                            // Check if platform is "Table Order" and show table selection dialog
                            if (orderDetailsVm.IsTableOrderFlag == 1 && orderDetailsVm.TableOrderMethod == "Dine-in")                               
                            {
                                // Show table selection dialog
                                var selectedTable = await ShowTableSelectionDialogAsync();
                                if (selectedTable == null)
                                {
                                    // User cancelled table selection
                                    orderDetailsVm.IsAccepting = false;
                                    orderDetailsVm.IsLoading = false;
                                    return;
                                }
                                
                                // Store the selected table information for the order acceptance
                                orderDetailsVm.SelectedTableId = selectedTable.ApiId;
                                orderDetailsVm.SelectedTableOrderingsId = selectedTable.TableOrderingsId;
                                orderDetailsVm.SelectedTableName = selectedTable.Name;
                                //MessageBox.Show($"Selected table: {orderDetailsVm.SelectedTableName}");
                                //MessageBox.Show($"orderId: {orderId2}");
                                // Update table status to RESERVED with ongoing order id (use table_orderings_id for reserve flow)
                                try
                                {
                                    int ongoingOrderId = 0;
                                    try { ongoingOrderId = orderId2; } catch { ongoingOrderId = 0; }
                                    if (orderDetailsVm.SelectedTableId.HasValue)
                                    {
                                        await _apiService.UpdateTableStatusAsync(orderDetailsVm.SelectedTableId.Value, "RESERVED", ongoingOrderId);
                                    }
                                }
                                catch (Exception tableEx)
                                {
                                    MessageBox.Show($"Failed to update table status to RESERVED: {tableEx.Message}");
                                    // Continue acceptance even if table status update fails
                                }
                            }
                            
                            // Process the order acceptance first
                            await AcceptOrderAsync(orderId, orderJsonData, orderDetailsVm.SelectedTableId, orderDetailsVm.SelectedTableName);
                            MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, null);
                            // Close popup only after API call is finished
                            orderDetailsVm.OnDialogClosed();
                        }
                        catch (Exception ex)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                System.Windows.MessageBox.Show($"Error accepting order: {ex.Message}", 
                                    "Accept Order Error", 
                                    System.Windows.MessageBoxButton.OK, 
                                    System.Windows.MessageBoxImage.Error);
                            });
                        }
                        finally
                        {
                            orderDetailsVm.IsAccepting = false;
                            orderDetailsVm.IsLoading = false;
                        }
                    });
                    
                    orderDetailsVm.RejectCommand = new ViewModels.RelayCommand(async () =>
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"Rejecting order: {orderDetailsVm.OrderNumber}");
                            orderDetailsVm.IsRejecting = true;
                            orderDetailsVm.IsLoading = true;
                            
                            // Process the order rejection first
                            await RejectOrderAsync(orderId, orderJsonData);
                            
                            // Close popup only after API call is finished
                            orderDetailsVm.OnDialogClosed();
                        }
                        catch (Exception ex)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                System.Windows.MessageBox.Show($"Error rejecting order: {ex.Message}", 
                                    "Reject Order Error", 
                                    System.Windows.MessageBoxButton.OK, 
                                    System.Windows.MessageBoxImage.Error);
                            });
                        }
                        finally
                        {
                            orderDetailsVm.IsRejecting = false;
                            orderDetailsVm.IsLoading = false;
                        }
                    });
                    
                    // Show via MaterialDesign DialogHost so it is properly modal and does not overlay system dialogs
                    var dialog = new View.OrderDetailsDialog { DataContext = orderDetailsVm };
                    orderDetailsVm.DialogClosed += () => 
                    { 
                        MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, null); 
                        _currentNewOrderAlertDisplayOrderId = null;
                        _currentOrderDetailsDialogDisplayOrderId = null;
                    };
                    _ = MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "AddItemDialogHost");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing order details dialog: {ex.Message}");
            }
        }

        private static void ParseModifierGroup(System.Text.Json.JsonElement group,
            System.Collections.Generic.List<Models.ModifierDetailModel> output,
            string indent)
        {
            try
            {
                // Expected structure:
                // { "title": string, "selected_item": [ { title, price_per_item/display_price, modifiers: [ ... ] } ] }
                var groupTitle = group.TryGetProperty("title", out var t) ? t.GetString() : "";
                if (group.TryGetProperty("selected_item", out var selectedEl) && selectedEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var sel in selectedEl.EnumerateArray())
                    {
                        var itemTitle = sel.TryGetProperty("title", out var it) ? it.GetString() : "";
                        decimal price = 0m;
                        if (sel.TryGetProperty("price_per_item", out var ppi))
                        {
                            if (ppi.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                decimal.TryParse(ppi.GetString().Replace(" ", ""), out price);
                            }
                            else if (ppi.ValueKind == System.Text.Json.JsonValueKind.Number)
                            {
                                price = ppi.GetDecimal();
                            }
                        }
                        else if (sel.TryGetProperty("display_price", out var dp))
                        {
                            if (dp.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                decimal.TryParse(dp.GetString().Replace(" ", ""), out price);
                            }
                            else if (dp.ValueKind == System.Text.Json.JsonValueKind.Number)
                            {
                                price = dp.GetDecimal();
                            }
                        }

                        // Incoming UI modifier prices are in cents; convert to major units
                        price = Math.Round(price / 100m, 2, MidpointRounding.AwayFromZero);

                        var name = string.IsNullOrWhiteSpace(groupTitle) ? itemTitle : $"{groupTitle}: {itemTitle}";
                        var isNested = !string.IsNullOrEmpty(indent);
                        output.Add(new Models.ModifierDetailModel(name, price, isNested, indent));

                        // Recurse into nested modifiers if present
                        if (sel.TryGetProperty("modifiers", out var nested) && nested.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var nestedGroup in nested.EnumerateArray())
                            {
                                ParseModifierGroup(nestedGroup, output, indent + "    ");
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignore malformed modifier entries
            }
        }

        private async Task AcceptOrderAsync(string orderId, string orderJsonData, int? selectedTableId = null, string selectedTableName = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Accepting Order ===");
                System.Diagnostics.Debug.WriteLine($"Order ID: {orderId}");
                
                // Extract display order ID from JSON for banner removal
                string displayOrderId = null;
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(orderJsonData);
                    var order = doc.RootElement;
                    displayOrderId = order.GetProperty("display_order_id").GetString();
                }
                catch { /* ignore JSON parsing errors */ }
                
                // Get Laravel bearer token
                var laravelService = new LaravelPassportService();
                var bearerToken = await laravelService.GetAccessTokenAsync();
                
                if (string.IsNullOrEmpty(bearerToken))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to get Laravel bearer token for order acceptance");
                    return;
                }

                // Get tenant code from settings
                var (tenantCode, _, _) = _settingsService.LoadSettings();
                
                // Build the API URL for accepting the order
                var apiUrl = $"/api/v1/accept-order/{orderId}";
                
                // Create request body for accept order
                object requestBody;
                if (selectedTableId.HasValue && !string.IsNullOrEmpty(selectedTableName))
                {
                    // Include table information for table orders
                    requestBody = new { 
                        reason = new[] { "accepted" },
                        table_id = selectedTableId.Value,
                        table_name = selectedTableName
                    };
                    System.Diagnostics.Debug.WriteLine($"Accepting table order with table ID: {selectedTableId.Value}, table name: {selectedTableName}");
                }
                else
                {
                    // Standard accept order request
                    requestBody = new { reason = new[] { "accepted" } };
                }
                
                // Call the Laravel API to accept the order
                var apiService = new ApiService();
                var response = await apiService.CallLaravelApiAsync(apiUrl, bearerToken, tenantCode, System.Net.Http.HttpMethod.Post, requestBody);
                
                System.Diagnostics.Debug.WriteLine($"Accept Order Response: {response}");
                System.Diagnostics.Debug.WriteLine("=== End Accepting Order ===");
                
                // Print receipt after successful acceptance (pass selected table name for dine-in table orders so receipt shows POS table)
                await PrintOrderReceiptAsync(orderId, orderJsonData, selectedTableName);

                // Remove specific order from incoming banner
                HideIncomingOrderBannerUI(displayOrderId);
                
                // Close OrderDetailsDialog if it's open for this order
                CloseOrderDetailsDialogIfOpenForOrder(displayOrderId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error accepting order: {ex.Message}");
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show($"Error accepting order: {ex.Message}", 
                        "Accept Order Error", 
                        System.Windows.MessageBoxButton.OK, 
                        System.Windows.MessageBoxImage.Error);
                });
            }
        }

        private async Task RejectOrderAsync(string orderId, string orderJsonData = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Rejecting Order ===");
                System.Diagnostics.Debug.WriteLine($"Order ID: {orderId}");
                
                // Extract display order ID from JSON for banner removal
                string displayOrderId = null;
                if (!string.IsNullOrEmpty(orderJsonData))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(orderJsonData);
                        var order = doc.RootElement;
                        displayOrderId = order.GetProperty("display_order_id").GetString();
                    }
                    catch { /* ignore JSON parsing errors */ }
                }
                
                // Get Laravel bearer token
                var laravelService = new LaravelPassportService();
                var bearerToken = await laravelService.GetAccessTokenAsync();
                
                if (string.IsNullOrEmpty(bearerToken))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to get Laravel bearer token for order rejection");
                    return;
                }

                // Get tenant code from settings
                var (tenantCode, _, _) = _settingsService.LoadSettings();
                
                // Build the API URL for rejecting the order
                var apiUrl = $"/api/v1/deny-order/{orderId}";
                
                // Create request body for reject order
                var requestBody = new { reason = new[] { new { explanation = "Cannot serve" } } };
                
                // Call the Laravel API to reject the order
                var apiService = new ApiService();
                var response = await apiService.CallLaravelApiAsync(apiUrl, bearerToken, tenantCode, System.Net.Http.HttpMethod.Post, requestBody);
                
                System.Diagnostics.Debug.WriteLine($"Reject Order Response: {response}");
                System.Diagnostics.Debug.WriteLine("=== End Rejecting Order ===");

                // Remove specific order from incoming banner
                HideIncomingOrderBannerUI(displayOrderId);
                
                // Close OrderDetailsDialog if it's open for this order
                CloseOrderDetailsDialogIfOpenForOrder(displayOrderId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error rejecting order: {ex.Message}");
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show($"Error rejecting order: {ex.Message}", 
                        "Reject Order Error", 
                        System.Windows.MessageBoxButton.OK, 
                        System.Windows.MessageBoxImage.Error);
                });
            }
        }

        // Additional order status methods for future use
        private async Task ReadyOrderAsync(string orderId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Setting Order Ready ===");
                System.Diagnostics.Debug.WriteLine($"Order ID: {orderId}");
                
                // Get Laravel bearer token
                var laravelService = new LaravelPassportService();
                var bearerToken = await laravelService.GetAccessTokenAsync();
                
                if (string.IsNullOrEmpty(bearerToken))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to get Laravel bearer token for order ready status");
                    return;
                }

                // Get tenant code from settings
                var (tenantCode, _, _) = _settingsService.LoadSettings();
                
                // Build the API URL for ready order
                var apiUrl = $"/api/v1/ready-to-pickup/{orderId}";
                
                // Create request body for ready order
                var requestBody = new { reason = new[] { "ready" } };
                
                // Call the Laravel API
                var apiService = new ApiService();
                var response = await apiService.CallLaravelApiAsync(apiUrl, bearerToken, tenantCode, System.Net.Http.HttpMethod.Post, requestBody);
                
                System.Diagnostics.Debug.WriteLine($"Ready Order Response: {response}");
                System.Diagnostics.Debug.WriteLine("=== End Setting Order Ready ===");

                // Hide incoming banner on cashier page after ready
                HideIncomingOrderBannerUI();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting order ready: {ex.Message}");
            }
        }

        private async Task CompleteOrderAsync(string orderId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Completing Order ===");
                System.Diagnostics.Debug.WriteLine($"Order ID: {orderId}");
                
                // Get Laravel bearer token
                var laravelService = new LaravelPassportService();
                var bearerToken = await laravelService.GetAccessTokenAsync();
                
                if (string.IsNullOrEmpty(bearerToken))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to get Laravel bearer token for order completion");
                    return;
                }

                // Get tenant code from settings
                var (tenantCode, _, _) = _settingsService.LoadSettings();
                
                // Build the API URL for complete order
                var apiUrl = $"/api/v1/complete-order/{orderId}";
                
                // Create request body for complete order
                var requestBody = new { reason = new[] { "complete" } };
                
                // Call the Laravel API
                var apiService = new ApiService();
                var response = await apiService.CallLaravelApiAsync(apiUrl, bearerToken, tenantCode, System.Net.Http.HttpMethod.Post, requestBody);
                
                System.Diagnostics.Debug.WriteLine($"Complete Order Response: {response}");
                System.Diagnostics.Debug.WriteLine("=== End Completing Order ===");

                // Hide incoming banner on cashier page after complete
                HideIncomingOrderBannerUI();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error completing order: {ex.Message}");
            }
        }

        private async Task CancelOrderAsync(string orderId, string[] reasons)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Cancelling Order ===");
                System.Diagnostics.Debug.WriteLine($"Order ID: {orderId}");
                
                // Get Laravel bearer token
                var laravelService = new LaravelPassportService();
                var bearerToken = await laravelService.GetAccessTokenAsync();
                
                if (string.IsNullOrEmpty(bearerToken))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to get Laravel bearer token for order cancellation");
                    return;
                }

                // Get tenant code from settings
                var (tenantCode, _, _) = _settingsService.LoadSettings();
                
                // Build the API URL for cancel order
                var apiUrl = $"/api/v1/cancel-order/{orderId}";
                
                // Create request body for cancel order
                var requestBody = new { reason = reasons };
                
                // Call the Laravel API
                var apiService = new ApiService();
                var response = await apiService.CallLaravelApiAsync(apiUrl, bearerToken, tenantCode, System.Net.Http.HttpMethod.Post, requestBody);
                
                System.Diagnostics.Debug.WriteLine($"Cancel Order Response: {response}");
                System.Diagnostics.Debug.WriteLine("=== End Cancelling Order ===");

                // Hide incoming banner on cashier page after cancel
                HideIncomingOrderBannerUI();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cancelling order: {ex.Message}");
            }
        }

        private void HideIncomingOrderBannerUI(string displayOrderId = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"HideIncomingOrderBannerUI called with displayOrderId: {displayOrderId ?? "null"}");
                
                if (!string.IsNullOrEmpty(displayOrderId))
                {
                    // Remove from persistent storage
                    RemoveIncomingOrderFromPersistentBanner(displayOrderId);
                }
                else
                {
                    // Clear all banners (fallback behavior)
                    System.Diagnostics.Debug.WriteLine("Clearing all incoming order banners");
                    _persistentIncomingOrderBanners.Clear();
                    _currentNewOrderAlertDisplayOrderId = null;
                    // Update the global incoming count after clearing all
                    UpdateIncomingOrdersCount(0);
                }
                
                // Update UI if on Cashier page
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (System.Windows.Application.Current.MainWindow is MainWindow mainWin && mainWin.MainFrame.Content is CashierHomePage cashierPage)
                    {
                        if (cashierPage.DataContext is ViewModels.CashierHomeViewModel vm)
                        {
                            vm.RefreshIncomingOrdersBannerFromPersistentStorage();
                        }
                    }
                });
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"Error in HideIncomingOrderBannerUI: {ex.Message}");
            }
        }

        // Close OrderDetailsDialog if it's open for a specific order
        private void CloseOrderDetailsDialogIfOpenForOrder(string displayOrderId)
        {
            try
            {
                if (string.IsNullOrEmpty(displayOrderId)) return;
                
                // Check if the tracked dialog order ID matches
                if (string.Equals(_currentOrderDetailsDialogDisplayOrderId, displayOrderId, StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"Closing OrderDetailsDialog for order {displayOrderId} (order was accepted/rejected)");
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            // Close the dialog
                            if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("AddItemDialogHost"))
                            {
                                MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", null);
                            }
                            // Clear tracking
                            _currentOrderDetailsDialogDisplayOrderId = null;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error closing OrderDetailsDialog: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CloseOrderDetailsDialogIfOpenForOrder: {ex.Message}");
            }
        }

        // Close OrderDetailsDialog if the order is no longer in CREATED status
        private void CloseOrderDetailsDialogIfOrderNotCreated(System.Collections.Generic.HashSet<string> latestDisplayOrderIds)
        {
            try
            {
                // latestDisplayOrderIds can be empty (no CREATED orders) but should not be null
                if (latestDisplayOrderIds == null) 
                {
                    System.Diagnostics.Debug.WriteLine("CloseOrderDetailsDialogIfOrderNotCreated: latestDisplayOrderIds is null, skipping");
                    return;
                }
                
                // Start with tracked ID as primary source
                string dialogOrderId = _currentOrderDetailsDialogDisplayOrderId;
                
                // Verify dialog is actually open and get order ID from dialog if possible
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // Check if dialog is actually open
                        if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("AddItemDialogHost"))
                        {
                            // Try to get the dialog session and extract the order ID
                            var dialogSession = MaterialDesignThemes.Wpf.DialogHost.GetDialogSession("AddItemDialogHost");
                            if (dialogSession?.Content is View.OrderDetailsDialog orderDialog && 
                                orderDialog.DataContext is ViewModels.OrderDetailsDialogViewModel vm)
                            {
                                dialogOrderId = vm.OrderNumber;
                                System.Diagnostics.Debug.WriteLine($"Found OrderDetailsDialog open with order ID: {dialogOrderId}");
                            }
                            // If we couldn't get from dialog but have tracked ID, use it
                            else if (string.IsNullOrEmpty(dialogOrderId) && !string.IsNullOrEmpty(_currentOrderDetailsDialogDisplayOrderId))
                            {
                                dialogOrderId = _currentOrderDetailsDialogDisplayOrderId;
                                System.Diagnostics.Debug.WriteLine($"Using tracked OrderDetailsDialog order ID: {dialogOrderId}");
                            }
                        }
                        else
                        {
                            // Dialog is not open, clear the ID
                            dialogOrderId = null;
                            // Also clear tracking if dialog is closed
                            if (!string.IsNullOrEmpty(_currentOrderDetailsDialogDisplayOrderId))
                            {
                                System.Diagnostics.Debug.WriteLine("OrderDetailsDialog is not open but tracking exists - clearing tracking");
                                _currentOrderDetailsDialogDisplayOrderId = null;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error getting dialog order ID: {ex.Message}");
                    }
                });
                
                // If we have a dialog order ID, check if it's still in CREATED
                if (!string.IsNullOrEmpty(dialogOrderId))
                {
                    // Use case-insensitive comparison (HashSet was created with StringComparer.OrdinalIgnoreCase)
                    var isOrderStillCreated = latestDisplayOrderIds.Contains(dialogOrderId);
                    
                    System.Diagnostics.Debug.WriteLine($"Checking OrderDetailsDialog order {dialogOrderId} - IsStillCreated: {isOrderStillCreated}, LatestIds count: {latestDisplayOrderIds.Count}");
                    if (latestDisplayOrderIds.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Latest order IDs: {string.Join(", ", latestDisplayOrderIds)}");
                    }
                    
                    if (!isOrderStillCreated)
                    {
                        System.Diagnostics.Debug.WriteLine($"OrderDetailsDialog order {dialogOrderId} is no longer in CREATED - closing dialog");
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                // Close the dialog
                                if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("AddItemDialogHost"))
                                {
                                    System.Diagnostics.Debug.WriteLine($"Closing OrderDetailsDialog for order {dialogOrderId}");
                                    MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", null);
                                }
                                // Clear tracking
                                _currentOrderDetailsDialogDisplayOrderId = null;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error closing OrderDetailsDialog: {ex.Message}");
                            }
                        });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"OrderDetailsDialog order {dialogOrderId} is still in CREATED - keeping dialog open");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No OrderDetailsDialog is currently open");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CloseOrderDetailsDialogIfOrderNotCreated: {ex.Message}");
            }
        }

        // Safely hide the bound New Order Alert popup and clear tracking state
        private void CloseNewOrderAlertPopup()
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // Hide via the Cashier VM binding; then clear tracking and show next queued alert if any
                        if (System.Windows.Application.Current.MainWindow is MainWindow mainWin && mainWin.MainFrame.Content is CashierHomePage cashierPage)
                        {
                            if (cashierPage.DataContext is ViewModels.CashierHomeViewModel vm)
                            {
                                vm.IsOrderAlertVisible = false;
                                _currentNewOrderAlertDisplayOrderId = null;
                                vm.TryShowNextNewOrderAlertFromQueue();
                                RecalculateCashierIncomingOrdersBadgeCount();
                            }
                        }
                    }
                    catch { }
                });
            }
            catch
            {
                _currentNewOrderAlertDisplayOrderId = null;
                RecalculateCashierIncomingOrdersBadgeCount();
            }
        }

        // Extract display_order_id from order JSON and set the current popup id
        private void TrySetCurrentPopupIdFromOrderJson(string orderJson)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(orderJson)) return;
                using var doc = System.Text.Json.JsonDocument.Parse(orderJson);
                if (doc.RootElement.TryGetProperty("display_order_id", out var disp) && disp.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    _currentNewOrderAlertDisplayOrderId = disp.GetString();
                    RecalculateCashierIncomingOrdersBadgeCount();
                }
            }
            catch { }
        }

        // Queue management and alert helpers
        /*public void EnqueueIncomingOrder(string orderJson)
        {
            if (string.IsNullOrWhiteSpace(orderJson)) return;
            lock (_pendingIncomingOrdersJson)
            {
                // keep latest up to 10
                _pendingIncomingOrdersJson.Insert(0, orderJson);
                while (_pendingIncomingOrdersJson.Count > 10)
                    _pendingIncomingOrdersJson.RemoveAt(_pendingIncomingOrdersJson.Count - 1);
            }
        }

        public System.Collections.Generic.List<string> DequeueAllIncomingOrders()
        {
            lock (_pendingIncomingOrdersJson)
            {
                var list = new System.Collections.Generic.List<string>(_pendingIncomingOrdersJson);
                _pendingIncomingOrdersJson.Clear();
                return list;
            }
        } */
        /*private void ShowTransientNewOrderAlert()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try { TransientNewOrderAlertRequested?.Invoke(); } catch { }
                });
            }
            catch {  }
        }*/

        // Persistent incoming order banner management
        public void AddIncomingOrderToPersistentBanner(string orderJson)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"AddIncomingOrderToPersistentBanner called with: {orderJson?.Substring(0, Math.Min(100, orderJson?.Length ?? 0))}...");
                
                if (string.IsNullOrEmpty(orderJson)) 
                {
                    System.Diagnostics.Debug.WriteLine("AddIncomingOrderToPersistentBanner: orderJson is null or empty");
                    return;
                }
                
                using var doc = System.Text.Json.JsonDocument.Parse(orderJson);
                var order = doc.RootElement;
                var displayId = order.GetProperty("display_order_id").GetString();
                if (string.IsNullOrWhiteSpace(displayId)) 
                {
                    System.Diagnostics.Debug.WriteLine("AddIncomingOrderToPersistentBanner: display_order_id is null or empty");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"AddIncomingOrderToPersistentBanner: Processing order {displayId}");

                // Extract platform information
                string platformName = "Unknown";
                string platformLogo = "";
                
                if (order.TryGetProperty("platform", out var platformProp))
                {
                    platformName = platformProp.GetString() ?? "Unknown";
                }
                
                // Prefer platform_logo if available; fallback to delivery_platform_logo
                if (order.TryGetProperty("platform_logo", out var pltLogo) && pltLogo.ValueKind != System.Text.Json.JsonValueKind.Null)
                {
                    platformLogo = pltLogo.GetString() ?? "";
                }

                // Prevent duplicates; update and move existing if already present
                var existing = _persistentIncomingOrderBanners.FirstOrDefault(i => i.DisplayOrderId == displayId);
                if (existing != null)
                {
                    existing.OrderJson = orderJson;
                    existing.PlatformName = platformName;
                    existing.PlatformLogo = platformLogo;
                    _persistentIncomingOrderBanners.Remove(existing);
                    _persistentIncomingOrderBanners.Insert(0, existing);
                }
                else
                {
                    // Insert newest at the start - store all orders, not just 3
                    _persistentIncomingOrderBanners.Insert(0, new IncomingOrderBannerItem
                    {
                        DisplayOrderId = displayId,
                        OrderJson = orderJson,
                        PlatformName = platformName,
                        PlatformLogo = platformLogo
                    });
                }

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    // Dismissed popup → same order is now in persistent; avoid double-counting the badge
                    if (string.Equals(_currentNewOrderAlertDisplayOrderId, displayId, StringComparison.OrdinalIgnoreCase))
                        _currentNewOrderAlertDisplayOrderId = null;

                    if (IsUserOnCashierPage())
                        RecalculateCashierIncomingOrdersBadgeCount();
                    else
                        UpdateIncomingOrdersCount(_persistentIncomingOrderBanners.Count);
                });
                System.Diagnostics.Debug.WriteLine($"AddIncomingOrderToPersistentBanner: Updated count to {_incomingOrdersCount}");
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"AddIncomingOrderToPersistentBanner: Exception caught: {ex.Message}");
                /* ignore parse errors */ 
            }
        }

        public void RemoveIncomingOrderFromPersistentBanner(string displayOrderId)
        {
            try
            {
                if (string.IsNullOrEmpty(displayOrderId)) return;

                var itemToRemove = _persistentIncomingOrderBanners.FirstOrDefault(i => i.DisplayOrderId == displayOrderId);
                if (itemToRemove != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Removing order from persistent banner: {displayOrderId}");
                    _persistentIncomingOrderBanners.Remove(itemToRemove);

                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        if (IsUserOnCashierPage())
                            RecalculateCashierIncomingOrdersBadgeCount();
                        else
                            UpdateIncomingOrdersCount(_persistentIncomingOrderBanners.Count);
                    });
                }
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"Error removing from persistent banner: {ex.Message}");
            }
        }

        public System.Collections.Generic.List<IncomingOrderBannerItem> GetPersistentIncomingOrderBanners()
        {
            return new System.Collections.Generic.List<IncomingOrderBannerItem>(_persistentIncomingOrderBanners);
        }

        public bool HasPersistentIncomingOrderBanners()
        {
            return _persistentIncomingOrderBanners.Count > 0;
        }

        public async Task<bool> LoadDataAfterLoginAsync()
        {
            try
            {
                // Initialize local order storage (create POS-Orders folder on desktop)
                try
                {
                    var localOrderStorage = LocalOrderStorageService.Instance;
                    var folderCreated = await localOrderStorage.EnsureOrdersFolderExistsAsync();
                    if (folderCreated)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] POS-Orders folder initialized successfully at: {localOrderStorage.OrdersFolderPath}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[LocalOrderStorage] Failed to initialize POS-Orders folder");
                    }
                }
                catch (Exception exStorage)
                {
                    System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Error initializing order storage: {exStorage.Message}");
                    // Don't fail the entire login for storage issues
                }

                // Ensure API headers reflect latest settings before first calls
                _apiService.RefreshHeadersFromSettings();

                // Load current user details (show alert on failure)
                try
                {
                    var currentUser = await _apiService.GetCurrentUserAsync();
                    if (currentUser != null)
                    {
                        CurrentUser = currentUser;
                        _localStorageService.SaveCurrentUser(currentUser);
                    }
                }
                catch (Exception exUser)
                {
                    System.Diagnostics.Debug.WriteLine($"[Error] GetCurrentUserAsync failed: {exUser.Message}");
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show($"Failed to load current user.\n{exUser.Message}", "API Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    });
                    return false;
                }

                // Load shop details
                var (tenantCode, outletCode, brandId) = _settingsService.LoadSettings();
                System.Diagnostics.Debug.WriteLine($"[Shop] Settings: OutletCode='{outletCode}', BrandId='{brandId}'");
                if (!string.IsNullOrEmpty(outletCode) && !string.IsNullOrEmpty(brandId))
                {
                    var shopDetails = await _apiService.GetShopDetailsAsync(outletCode, brandId);
                    if (shopDetails != null)
                    {
                        ShopDetails = shopDetails;
                        _localStorageService.SaveShopDetails(shopDetails);
                        // If delivery platform/menu is not configured, block login flow
                        if (ShopDetails.DeliveryPlatform == null || ShopDetails.DeliveryPlatform.SelectedMenu <= 0)
                        {
                            System.Diagnostics.Debug.WriteLine("[Shop] Missing delivery platform or selected menu.");
                            return false;
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[Shop] Missing outletCode/brandId in settings; cannot fetch shop.");
                }

                // Load order config and set Live Orders vs Orders page visibility for sidebar
                try
                {
                    if (ShopDetails != null && ShopDetails.Id > 0 && !string.IsNullOrEmpty(brandId) && int.TryParse(brandId, out int brandIdInt))
                    {
                        var orderConfigJson = await _apiService.GetOrderConfigAsync(ShopDetails.Id, brandIdInt);
                        if (!string.IsNullOrEmpty(orderConfigJson))
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(orderConfigJson);
                            var root = doc.RootElement;
                            var config = root;
                            OrderModel ongoingOrderModel = null;

                            if (root.TryGetProperty("data", out var dataEl))
                            {
                                config = dataEl;
                                if (dataEl.TryGetProperty("config", out var configEl))
                                    config = configEl;
                            }
                            if (config.TryGetProperty("is_live_orders_page", out var isLiveEl))
                            {
                                bool isLive = isLiveEl.ValueKind == System.Text.Json.JsonValueKind.True
                                    || (isLiveEl.ValueKind == System.Text.Json.JsonValueKind.String && string.Equals(isLiveEl.GetString(), "true", StringComparison.OrdinalIgnoreCase))
                                    || (isLiveEl.ValueKind == System.Text.Json.JsonValueKind.Number && isLiveEl.GetInt32() != 0);
                                UseLiveOrdersPage = isLive;
                                System.Diagnostics.Debug.WriteLine($"[GlobalData] Order config loaded: is_live_orders_page={isLive}");
                            }
                            // Auto-complete timers for platform 9 (POS) paid orders
                            if (config.TryGetProperty("is_takeaway", out var isTakeawayEl))
                                IsTakeawayAutoCompleteEnabled = isTakeawayEl.ValueKind == System.Text.Json.JsonValueKind.True
                                    || (isTakeawayEl.ValueKind == System.Text.Json.JsonValueKind.String && string.Equals(isTakeawayEl.GetString(), "true", StringComparison.OrdinalIgnoreCase))
                                    || (isTakeawayEl.ValueKind == System.Text.Json.JsonValueKind.Number && isTakeawayEl.GetInt32() != 0);
                            if (config.TryGetProperty("takeaway_timer_mins", out var takeawayMinsEl) && takeawayMinsEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                                TakeawayAutoCompleteTimerMins = Math.Max(0, takeawayMinsEl.GetInt32());
                            if (config.TryGetProperty("is_dinein", out var isDineInEl))
                                IsDineInAutoCompleteEnabled = isDineInEl.ValueKind == System.Text.Json.JsonValueKind.True
                                    || (isDineInEl.ValueKind == System.Text.Json.JsonValueKind.String && string.Equals(isDineInEl.GetString(), "true", StringComparison.OrdinalIgnoreCase))
                                    || (isDineInEl.ValueKind == System.Text.Json.JsonValueKind.Number && isDineInEl.GetInt32() != 0);
                            if (config.TryGetProperty("dinein_timer_mins", out var dineInMinsEl) && dineInMinsEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                                DineInAutoCompleteTimerMins = Math.Max(0, dineInMinsEl.GetInt32());
                            if (config.TryGetProperty("is_delivery", out var isDeliveryEl))
                                IsDeliveryAutoCompleteEnabled = isDeliveryEl.ValueKind == System.Text.Json.JsonValueKind.True
                                    || (isDeliveryEl.ValueKind == System.Text.Json.JsonValueKind.String && string.Equals(isDeliveryEl.GetString(), "true", StringComparison.OrdinalIgnoreCase))
                                    || (isDeliveryEl.ValueKind == System.Text.Json.JsonValueKind.Number && isDeliveryEl.GetInt32() != 0);
                            if (config.TryGetProperty("delivery_timer_mins", out var deliveryMinsEl) && deliveryMinsEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                                DeliveryAutoCompleteTimerMins = Math.Max(0, deliveryMinsEl.GetInt32());
                            if (config.TryGetProperty("idle_logout_minutes", out var idleMinsEl) && idleMinsEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                                IdleLogoutMinutes = Math.Max(1, Math.Min(120, idleMinsEl.GetInt32()));

                            // Restore ongoing order into cart on login if present in order config
                            try
                            {
                                if (config.TryGetProperty("ongoing_order", out var ongoingEl) &&
                                    ongoingEl.ValueKind == System.Text.Json.JsonValueKind.Array &&
                                    ongoingEl.GetArrayLength() > 0)
                                {
                                    var orderPayload = ongoingEl[0];
                                    ongoingOrderModel = BuildOrderModelFromOngoingPayload(config, orderPayload);
                                }
                            }
                            catch (Exception exOngoing)
                            {
                                System.Diagnostics.Debug.WriteLine($"[GlobalData] Failed to parse ongoing_order from order config: {exOngoing.Message}");
                            }

                            if (ongoingOrderModel != null)
                            {
                                // Make the ongoing order available for Cashier to load on first navigation
                                CurrentOrderForEdit = ongoingOrderModel;
                                HasOngoingOrderFromConfig = true;
                                System.Diagnostics.Debug.WriteLine($"[GlobalData] Ongoing order restored for display_order_id={ongoingOrderModel.DisplayOrderId}");
                            }
                        }
                    }
                }
                catch (Exception exOrderConfig)
                {
                    System.Diagnostics.Debug.WriteLine($"[GlobalData] Order config load failed (using default): {exOrderConfig.Message}");
                }

                // Start auto-complete order checker for platform 9 paid orders (timer-based)
                try { AutoCompleteOrderService.Instance.Start(); } catch (Exception exAc) { System.Diagnostics.Debug.WriteLine($"[GlobalData] AutoCompleteOrderService start failed: {exAc.Message}"); }

                // Load menu data (categories, products, menu tabs) + floor plans into cache (parallel; floor plan failures are non-fatal).
                var floorPlansCacheTask = LoadFloorPlansIntoCacheAsync();
                try
                {
                    System.Diagnostics.Debug.WriteLine("[GlobalData] Loading menu data into cache...");
                    await LoadMenuDataIntoCacheAsync();
                    System.Diagnostics.Debug.WriteLine("[GlobalData] Menu data loaded successfully");
                }
                catch (Exception exMenu)
                {
                    System.Diagnostics.Debug.WriteLine($"[GlobalData] Error loading menu data: {exMenu.Message}");
                    // Don't fail login if menu loading fails - can be retried later
                }

                try
                {
                    await floorPlansCacheTask;
                }
                catch (Exception exFp)
                {
                    System.Diagnostics.Debug.WriteLine($"[GlobalData] Error awaiting floor plan cache load: {exFp.Message}");
                }

                // Start Firebase listener for the entire system
                try
                {
                    await _firebaseService.StartListeningToCollectionAsync();
                }
                catch (Exception ex)
                {
                    // Silent error
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Error] LoadDataAfterLoginAsync: {ex.Message}");
                // Let caller handle presenting errors on the login screen
                return false;
            }
        }

        /// <summary>
        /// Builds an <see cref="OrderModel"/> instance from the ongoing_order payload stored in terminal order config.
        /// </summary>
        /// <param name="config">The resolved order config JSON element (config object, not the outer data wrapper).</param>
        /// <param name="orderPayload">The first element of the ongoing_order array (created via OrderModel.ToApiRequest()).</param>
        /// <returns>An <see cref="OrderModel"/> ready to be loaded into the Cashier cart, or null if parsing fails.</returns>
        private static OrderModel BuildOrderModelFromOngoingPayload(System.Text.Json.JsonElement config, System.Text.Json.JsonElement orderPayload)
        {
            try
            {
                if (orderPayload.ValueKind != System.Text.Json.JsonValueKind.Object)
                    return null;

                var order = new OrderModel();

                // Display/order id
                string displayOrderId = null;
                if (config.TryGetProperty("display_order_id", out var displayIdEl) && displayIdEl.ValueKind == System.Text.Json.JsonValueKind.String)
                    displayOrderId = displayIdEl.GetString();
                if (string.IsNullOrWhiteSpace(displayOrderId) &&
                    orderPayload.TryGetProperty("display_order_id", out var payloadDisplayIdEl) &&
                    payloadDisplayIdEl.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    displayOrderId = payloadDisplayIdEl.GetString();
                }

                order.DisplayOrderId = (displayOrderId ?? string.Empty).Trim();
                order.OrderNumber = order.DisplayOrderId;

                // Basic customer/order level fields
                if (orderPayload.TryGetProperty("customer_id", out var customerIdEl) && customerIdEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                    order.CustomerId = customerIdEl.GetInt32();
                if (orderPayload.TryGetProperty("customer_name", out var customerNameEl) && customerNameEl.ValueKind == System.Text.Json.JsonValueKind.String)
                    order.CustomerName = customerNameEl.GetString();
                if (orderPayload.TryGetProperty("order_note", out var noteEl) && noteEl.ValueKind == System.Text.Json.JsonValueKind.String)
                    order.OrderNotes = noteEl.GetString();

                // Monetary/timer fields
                if (orderPayload.TryGetProperty("discount", out var discountEl) && discountEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                    order.DiscountAmount = discountEl.GetDecimal();
                if (orderPayload.TryGetProperty("discount_percentage", out var discountPctEl) && discountPctEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                    order.DiscountPercentage = discountPctEl.GetDecimal();
                if (orderPayload.TryGetProperty("discount_mode_applied", out var discountModeEl) && discountModeEl.ValueKind == System.Text.Json.JsonValueKind.String)
                    order.DiscountModeApplied = discountModeEl.GetString();
                if (orderPayload.TryGetProperty("total_amount", out var totalEl) && totalEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                    order.ApiTotal = totalEl.GetDecimal();
                if (orderPayload.TryGetProperty("sub_total", out var subTotalEl) && subTotalEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                    order.ApiSubTotal = subTotalEl.GetDecimal();
                if (orderPayload.TryGetProperty("shipping_total", out var shipTotalEl) && shipTotalEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                    order.DeliveryCharge = shipTotalEl.GetDecimal();
                if (orderPayload.TryGetProperty("total_tax", out var totalTaxEl) && totalTaxEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                    order.TotalTaxAmount = totalTaxEl.GetDecimal();

                // Shipping / order type
                string shippingMethod = null;
                if (orderPayload.TryGetProperty("shipping_method", out var shipMethodEl) && shipMethodEl.ValueKind == System.Text.Json.JsonValueKind.String)
                    shippingMethod = shipMethodEl.GetString();
                order.ShippingMethod = shippingMethod;
                if (!string.IsNullOrWhiteSpace(shippingMethod))
                {
                    var upper = shippingMethod.Trim().ToUpperInvariant();
                    order.OrderType = upper switch
                    {
                        "DINE-IN" => OrderType.DineIn,
                        "TAKEAWAY" => OrderType.TakeAway,
                        "DELIVERY" => OrderType.Delivery,
                        "COLLECTION" => OrderType.Collection,
                        _ => OrderType.TakeAway
                    };
                }
                else
                {
                    order.OrderType = OrderType.TakeAway;
                }

                // Core items (order_items from ToApiRequest)
                var items = new System.Collections.Generic.List<OrderItem>();
                if (orderPayload.TryGetProperty("order_items", out var itemsEl) &&
                    itemsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var itemEl in itemsEl.EnumerateArray())
                    {
                        if (itemEl.ValueKind != System.Text.Json.JsonValueKind.Object)
                            continue;

                        var item = new OrderItem();

                        if (itemEl.TryGetProperty("item_name", out var itemNameEl) && itemNameEl.ValueKind == System.Text.Json.JsonValueKind.String)
                            item.Name = itemNameEl.GetString();

                        if (itemEl.TryGetProperty("quantity", out var qtyEl) && qtyEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                            item.Quantity = qtyEl.GetInt32();

                        if (itemEl.TryGetProperty("price_per_item", out var priceEl) && priceEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                            item.Price = priceEl.GetDecimal();

                        if (itemEl.TryGetProperty("note", out var itemNoteEl) && itemNoteEl.ValueKind == System.Text.Json.JsonValueKind.String)
                            item.Note = itemNoteEl.GetString();

                        if (itemEl.TryGetProperty("item_id", out var itemIdEl) && itemIdEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                            item.ApiItemId = itemIdEl.GetInt32();

                        if (itemEl.TryGetProperty("discount_amount", out var itemDiscEl) && itemDiscEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            var perUnitDiscount = itemDiscEl.GetDecimal();
                            var lineDiscount = perUnitDiscount * (item.Quantity > 0 ? item.Quantity : 1);
                            item.ApiDiscountAmount = lineDiscount;
                            item.VisibleDiscountAmount = lineDiscount;
                        }

                        if (itemEl.TryGetProperty("tax", out var itemTaxEl) && itemTaxEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                            item.TaxAmount = itemTaxEl.GetDecimal();

                        items.Add(item);
                    }
                }

                order.Items = items;

                // Vouchers (if any)
                if (orderPayload.TryGetProperty("vouchers", out var vouchersEl) &&
                    vouchersEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var vouchers = new System.Collections.Generic.List<VoucherModel>();
                    foreach (var vEl in vouchersEl.EnumerateArray())
                    {
                        if (vEl.ValueKind != System.Text.Json.JsonValueKind.Object)
                            continue;

                        var voucher = new VoucherModel();
                        if (vEl.TryGetProperty("voucher_code", out var codeEl) && codeEl.ValueKind == System.Text.Json.JsonValueKind.String)
                            voucher.VoucherCode = codeEl.GetString();
                        if (vEl.TryGetProperty("voucher_value", out var valueEl) && valueEl.ValueKind == System.Text.Json.JsonValueKind.String)
                            voucher.VoucherValue = valueEl.GetString();
                        if (vEl.TryGetProperty("value_type", out var valueTypeEl) && valueTypeEl.ValueKind == System.Text.Json.JsonValueKind.String)
                            voucher.ValueType = valueTypeEl.GetString();
                        if (vEl.TryGetProperty("voucher_discount", out var discEl) && discEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                            voucher.VoucherDiscount = discEl.GetDecimal();

                        vouchers.Add(voucher);
                    }

                    order.Vouchers = vouchers;
                }

                return order;
            }
            catch
            {
                return null;
            }
        }

        public void LoadDataFromStorage()
        {
            try
            {
                // Load current user from storage
                var currentUser = _localStorageService.GetCurrentUser();
                if (currentUser != null)
                {
                    CurrentUser = currentUser;
                }

                // Load shop details from storage
                var shopDetails = _localStorageService.GetShopDetails();
                if (shopDetails != null)
                {
                    ShopDetails = shopDetails;
                    System.Diagnostics.Debug.WriteLine($"[Shop] Loaded from Local Storage: Id={shopDetails.Id}, Name={shopDetails.Name}, CountryCode={shopDetails.CountryCode}, Lat={shopDetails.Latitude}, Lng={shopDetails.Longitude}");
                }
            }
            catch (Exception ex)
            {
                // Silent error
            }
        }

        public async void ClearData()
        {
            try
            {
                AutoCompleteOrderService.Instance.Stop();
                // Stop Firebase listener before clearing data
                await _firebaseService.StopListeningToCollectionAsync();
                
                CurrentUser = null;
                ShopDetails = null;
                _localStorageService.ClearAllData();

                try
                {
                    _persistentIncomingOrderBanners.Clear();
                    _currentNewOrderAlertDisplayOrderId = null;
                    _currentOrderDetailsDialogDisplayOrderId = null;
                    UpdateIncomingOrdersCount(0);
                }
                catch { /* ignore */ }

                CartService.Instance.CashierSessionDisplayOrderId = null;
                
                // Clear menu cache on logout
                ClearMenuCache();
            }
            catch (Exception ex)
            {
                // Silent error
            }
        }

        public bool HasCurrentUser()
        {
            return CurrentUser != null;
        }

        public bool HasShopDetails()
        {
            return ShopDetails != null;
        }

        public async Task RefreshCurrentUserAsync()
        {
            try
            {
                var currentUser = await _apiService.GetCurrentUserAsync();
                if (currentUser != null)
                {
                    CurrentUser = currentUser;
                    _localStorageService.SaveCurrentUser(currentUser);
                }
            }
            catch (Exception ex)
            {
                // Silent error
            }
        }

        public async Task RefreshShopDetailsAsync()
        {
            try
            {
                var (tenantCode, outletCode, brandId) = _settingsService.LoadSettings();
                if (!string.IsNullOrEmpty(outletCode) && !string.IsNullOrEmpty(brandId))
                {
                    var shopDetails = await _apiService.GetShopDetailsAsync(outletCode, brandId);
                    if (shopDetails != null)
                    {
                        ShopDetails = shopDetails;
                        _localStorageService.SaveShopDetails(shopDetails);
                    }
                }
            }
            catch (Exception ex)
            {
                // Silent error
            }
        }

        private async Task PrintOrderReceiptAsync(string orderId, string orderJsonData, string selectedTableName = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== Printing Order Receipt (New Format) ===");
                System.Diagnostics.Debug.WriteLine($"Order ID: {orderId}");

                // Delegate to centralized printing service; pass selected POS table name so receipt shows it for dine-in table orders
                await ReceiptPrintingService.Instance.PrintIncomingOrderReceiptFromJsonAsync(orderJsonData, selectedTableName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error printing receipt: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes incoming orders from API when returning to cashier page for data consistency
        /// This method is called when user navigates back to cashier page to ensure no orders are missed
        /// </summary>
        public async Task RefreshIncomingOrdersFromApiAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== Refreshing Incoming Orders from API ===");
                
                // Check if we have shop details
                if (_shopDetails == null)
                {
                    System.Diagnostics.Debug.WriteLine("Shop details not available for API refresh");
                    return;
                }

                // Get Laravel bearer token
                var laravelService = new LaravelPassportService();
                var bearerToken = await laravelService.GetAccessTokenAsync();
                
                if (string.IsNullOrEmpty(bearerToken))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to get Laravel bearer token for API refresh");
                    return;
                }

                // Build the API URL with franchise and shop parameters
                var apiUrl = $"/api/v1/admin/orders/status/CREATED?franchise={_shopDetails.FranchiseId}&shop={_shopDetails.Id}&paginate=false";
                
                // Get tenant code from settings
                var (tenantCode, _, _) = _settingsService.LoadSettings();
                
                // Call the Laravel API using the existing method in ApiService
                var apiService = new ApiService();
                var response = await apiService.CallLaravelApiAsync(apiUrl, bearerToken, tenantCode, System.Net.Http.HttpMethod.Get);
                
                System.Diagnostics.Debug.WriteLine($"API Refresh Response: {response}");
                
                // Parse the JSON response and update persistent storage
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(response);
                    
                    // Clear existing persistent banners to avoid duplicates
                    _persistentIncomingOrderBanners.Clear();
                    
                    // Build latest order IDs set from API response for reconciliation
                    var latestOrderIds = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                    // Check if the response has a 'data' property (common Laravel API structure)
                    if (doc.RootElement.TryGetProperty("data", out var dataElement))
                    {
                        // Check if data has an 'orders' property (nested structure)
                        if (dataElement.TryGetProperty("orders", out var ordersElement))
                        {
                            if (ordersElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                foreach (var order in ordersElement.EnumerateArray())
                                {
                                    // Extract display_order_id for reconciliation
                                    if (order.TryGetProperty("display_order_id", out var dispEl) && dispEl.ValueKind == System.Text.Json.JsonValueKind.String)
                                    {
                                        var id = dispEl.GetString();
                                        if (!string.IsNullOrWhiteSpace(id))
                                        {
                                            latestOrderIds.Add(id);
                                        }
                                    }
                                    
                                    var orderJson = order.ToString();
                                    if (!string.IsNullOrEmpty(orderJson))
                                    {
                                        AddIncomingOrderToPersistentBanner(orderJson);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // If data is directly an array (fallback for other API structures)
                            if (dataElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                foreach (var order in dataElement.EnumerateArray())
                                {
                                    // Extract display_order_id for reconciliation
                                    if (order.TryGetProperty("display_order_id", out var dispEl) && dispEl.ValueKind == System.Text.Json.JsonValueKind.String)
                                    {
                                        var id = dispEl.GetString();
                                        if (!string.IsNullOrWhiteSpace(id))
                                        {
                                            latestOrderIds.Add(id);
                                        }
                                    }
                                    
                                    var orderJson = order.ToString();
                                    if (!string.IsNullOrEmpty(orderJson))
                                    {
                                        AddIncomingOrderToPersistentBanner(orderJson);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // If no 'data' property, check if root is an array
                        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var order in doc.RootElement.EnumerateArray())
                            {
                                // Extract display_order_id for reconciliation
                                if (order.TryGetProperty("display_order_id", out var dispEl) && dispEl.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    var id = dispEl.GetString();
                                    if (!string.IsNullOrWhiteSpace(id))
                                    {
                                        latestOrderIds.Add(id);
                                    }
                                }
                                
                                var orderJson = order.ToString();
                                if (!string.IsNullOrEmpty(orderJson))
                                {
                                    AddIncomingOrderToPersistentBanner(orderJson);
                                }
                            }
                        }
                    }
                    
                    // Reconcile banners and close dialog if order is no longer in CREATED
                    //ReconcileIncomingOrderBanners(latestOrderIds);
                    
                    // Update the incoming orders count based on actual API response
                    // This ensures count reflects real CREATED orders, not just Firebase changes
                    _currentNewOrderAlertDisplayOrderId = null;
                    var apiOrderCount = _persistentIncomingOrderBanners.Count;
                    UpdateIncomingOrdersCount(apiOrderCount);
                    System.Diagnostics.Debug.WriteLine($"API Response: Updated count to {apiOrderCount} based on actual CREATED orders");
                    
                    System.Diagnostics.Debug.WriteLine($"API Refresh completed. Found {apiOrderCount} orders");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing API refresh response: {ex.Message}");
                    // Clear count on parsing error
                    UpdateIncomingOrdersCount(0);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing incoming orders from API: {ex.Message}");
                // Clear count on API error
                UpdateIncomingOrdersCount(0);
                // Don't show error to user - this is a background refresh
            }
        }

        private async Task<Models.TableModel> ShowTableSelectionDialogAsync()
        {
            try
            {
                // Load tables from API
                var apiService = new ApiService();
                var tables = await apiService.GetTablesAsync();
                
                // Convert to ObservableCollection for the dialog
                var tablesCollection = new System.Collections.ObjectModel.ObservableCollection<Models.TableModel>(tables);
                
                // Create the table selection dialog view model; pass incoming table name and session id from current order dialog
                string incomingTableName = null;
                int? incomingOrderSessionId = null;
                try
                {
                    var currentSession = MaterialDesignThemes.Wpf.DialogHost.GetDialogSession("AddItemDialogHost");
                    if (currentSession?.Content is POS_UI.View.OrderDetailsDialog currentOrderDialog && currentOrderDialog.DataContext is ViewModels.OrderDetailsDialogViewModel currentVm)
                    {
                        incomingTableName = string.IsNullOrWhiteSpace(currentVm.SelectedTableName) ? null : currentVm.SelectedTableName;
                        incomingOrderSessionId = currentVm.OrderSessionId;
                    }
                }
                catch { }
                object result;
                if (IsFloorPlanLayoutEnabled && CachedFloorPlans is { Count: > 0 } cachedPlans)
                {
                    var planClones = cachedPlans.Select(p => p.Clone()).ToList();
                    var fpVm = new ViewModels.FloorPlanCashierTableSelectionViewModel(
                        planClones,
                        tablesCollection,
                        preselectedTable: null,
                        incomingTableName,
                        incomingOrderSessionId);
                    var fpDlg = new View.FloorPlanCashierTableSelectionDialog { DataContext = fpVm };
                    try
                    {
                        result = await DialogHost.Show(fpDlg, "NestedModifiersDialogHost");
                    }
                    finally
                    {
                        fpVm.Dispose();
                    }
                }
                else
                {
                    var dialogVm = new ViewModels.TableSelectionDialogViewModel(tablesCollection, null, incomingTableName, incomingOrderSessionId);
                    var dialog = new View.TableSelectionDialog { DataContext = dialogVm };
                    result = await DialogHost.Show(dialog, "NestedModifiersDialogHost");
                }
                
                // Return the selected table or null if cancelled
                return result as Models.TableModel;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing table selection dialog: {ex.Message}");
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    /*System.Windows.MessageBox.Show($"Error loading tables: {ex.Message}", 
                        "Table Selection Error", 
                        System.Windows.MessageBoxButton.OK, 
                        System.Windows.MessageBoxImage.Error);*/
                });
                return null;
            }
        }
        
        // ============================================
        // MENU DATA CACHING METHODS
        // ============================================

        /// <summary>
        /// Fetches floor plan config after login so Settings → Floor Plan matches menu tabs (warm cache before first visit).
        /// </summary>
        public async Task LoadFloorPlansIntoCacheAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[GlobalData] Loading floor plan config into cache...");
                _apiService.RefreshHeadersFromSettings();
                var shopDetails = _localStorageService.GetShopDetails();
                if (shopDetails == null)
                {
                    return;
                }

                var (_, _, brandIdText) = _settingsService.LoadSettings();
                if (!int.TryParse(brandIdText, out var brandId))
                {
                    return;
                }

                const string terminalId = "1";
                var json = await _apiService.GetFloorPlanConfigAsync(shopDetails.Id, brandId, terminalId);
                var parsed = FloorPlanGetConfigParser.TryParse(json);
                if (parsed != null)
                {
                    UpdateCachedFloorPlans(parsed.Plans, parsed.FloorPlanLayoutEnabled, parsed.CustomItemTypes);
                    System.Diagnostics.Debug.WriteLine($"[GlobalData] Floor plan cache primed: enabled={parsed.FloorPlanLayoutEnabled}, plans={parsed.Plans.Count}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[GlobalData] Floor plan JSON parse failed; cache left unchanged for Settings to retry");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlobalData] Floor plan cache load failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Loads menu data (categories, products, menu tabs) from API into cache
        /// Called once during login to optimize performance
        /// </summary>
        public async Task LoadMenuDataIntoCacheAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[GlobalData] ========== Loading menu data into cache ==========");
                
                // Load categories and products
                var (apiCategories, apiProducts) = await _apiService.GetProductsAndCategoriesAsync();
                _cachedCategories = new System.Collections.Generic.List<string> { "All Items" };
                _cachedCategories.AddRange(apiCategories);
                _cachedProducts = apiProducts.ToList();
                
                System.Diagnostics.Debug.WriteLine($"[GlobalData] Cached {_cachedCategories.Count} categories, {_cachedProducts.Count} products");
                
                // Load menu tabs configuration
                _cachedMenuConfig = await MenuConfigService.Instance.LoadMenuConfigAsync();
                
                System.Diagnostics.Debug.WriteLine($"[GlobalData] Cached {_cachedMenuConfig.Tabs.Count} menu tabs");
                
                // Validate and clean menu tabs against current products/categories
                ValidateAndCleanMenuTabs();
                
                System.Diagnostics.Debug.WriteLine("[GlobalData] ========== Menu data cache loaded successfully ==========");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlobalData] ========== Error loading menu data into cache: {ex.Message} ==========");
                throw;
            }
        }
        
        /// <summary>
        /// Refreshes menu data from API and updates the cache
        /// Called when user clicks refresh button in settings
        /// </summary>
        public async Task RefreshMenuDataAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[GlobalData] ========== Refreshing menu data from API ==========");
                
                // Reload color mappings from disk to get latest color changes
                Helpers.ColorPalette.ReloadColorMappings();
                System.Diagnostics.Debug.WriteLine("[GlobalData] Reloaded color mappings from disk");
                
                // Reload categories and products
                var (apiCategories, apiProducts) = await _apiService.GetProductsAndCategoriesAsync();
                _cachedCategories = new System.Collections.Generic.List<string> { "All Items" };
                _cachedCategories.AddRange(apiCategories);
                _cachedProducts = apiProducts.ToList();
                
                // Force all products to reload their colors
                RefreshProductColors();
                
                System.Diagnostics.Debug.WriteLine($"[GlobalData] Refreshed {_cachedCategories.Count} categories, {_cachedProducts.Count} products");
                
                // Reload menu tabs configuration
                _cachedMenuConfig = await MenuConfigService.Instance.LoadMenuConfigAsync();
                
                System.Diagnostics.Debug.WriteLine($"[GlobalData] Refreshed {_cachedMenuConfig.Tabs.Count} menu tabs");
                
                // Validate and clean menu tabs against current products/categories
                ValidateAndCleanMenuTabs();
                
                System.Diagnostics.Debug.WriteLine("[GlobalData] ========== Menu data refresh complete ==========");
                
                // Notify listeners that menu data has been refreshed
                MenuDataRefreshed?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlobalData] ========== Error refreshing menu data: {ex.Message} ==========");
                throw;
            }
        }
        
        /// <summary>
        /// Forces all cached products to reload their colors from ColorPalette
        /// </summary>
        private void RefreshProductColors()
        {
            if (_cachedProducts == null) return;
            
            try
            {
                System.Diagnostics.Debug.WriteLine("[GlobalData] Refreshing colors for all products...");
                
                foreach (var product in _cachedProducts)
                {
                    // Force color update by re-reading from ColorPalette
                    product.BackgroundColor = Helpers.ColorPalette.GetBackgroundColor(product.Id, product.ItemName);
                    product.TextColor = Helpers.ColorPalette.GetTextColor();
                }
                
                System.Diagnostics.Debug.WriteLine($"[GlobalData] ✓ Refreshed colors for {_cachedProducts.Count} products");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlobalData] Error refreshing product colors: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Validates menu tabs and removes items/categories that no longer exist in the API
        /// </summary>
        private void ValidateAndCleanMenuTabs()
        {
            if (_cachedMenuConfig == null || _cachedMenuConfig.Tabs == null)
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine("[GlobalData] ========== Validating menu tabs ==========");
                
                // Get valid category IDs from products
                var validCategoryIds = _cachedProducts
                    .Where(p => p.CategoryId > 0)
                    .Select(p => p.CategoryId)
                    .Distinct()
                    .ToHashSet();
                
                // Get valid product IDs
                var validProductIds = _cachedProducts
                    .Select(p => p.Id)
                    .ToHashSet();
                
                bool hasChanges = false;
                
                foreach (var tab in _cachedMenuConfig.Tabs)
                {
                    int originalCategoryCount = tab.CategoryIds?.Count ?? 0;
                    int originalItemCount = tab.ItemIds?.Count ?? 0;
                    
                    // Clean category IDs - remove categories that no longer exist
                    if (tab.CategoryIds != null && tab.CategoryIds.Count > 0)
                    {
                        var validCategoryIdsInTab = tab.CategoryIds
                            .Where(catId => validCategoryIds.Contains(catId))
                            .ToList();
                        
                        if (validCategoryIdsInTab.Count != tab.CategoryIds.Count)
                        {
                            var removedCount = tab.CategoryIds.Count - validCategoryIdsInTab.Count;
                            System.Diagnostics.Debug.WriteLine($"[GlobalData] Tab '{tab.Name}': Removed {removedCount} invalid category IDs");
                            tab.CategoryIds = validCategoryIdsInTab;
                            hasChanges = true;
                        }
                    }
                    
                    // Clean item IDs - remove items that no longer exist
                    if (tab.ItemIds != null && tab.ItemIds.Count > 0)
                    {
                        var validItemIdsInTab = tab.ItemIds
                            .Where(itemId => validProductIds.Contains(itemId))
                            .ToList();
                        
                        if (validItemIdsInTab.Count != tab.ItemIds.Count)
                        {
                            var removedCount = tab.ItemIds.Count - validItemIdsInTab.Count;
                            System.Diagnostics.Debug.WriteLine($"[GlobalData] Tab '{tab.Name}': Removed {removedCount} invalid item IDs");
                            tab.ItemIds = validItemIdsInTab;
                            hasChanges = true;
                        }
                    }
                    
                    if (hasChanges)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GlobalData] Tab '{tab.Name}': Categories {originalCategoryCount} → {tab.CategoryIds?.Count ?? 0}, Items {originalItemCount} → {tab.ItemIds?.Count ?? 0}");
                    }
                }
                
                // If changes were made, save the updated config back to API
                if (hasChanges)
                {
                    System.Diagnostics.Debug.WriteLine("[GlobalData] Menu tabs were cleaned, saving updated config to API...");
                    _ = MenuConfigService.Instance.SaveMenuConfigAsync(_cachedMenuConfig);
                    System.Diagnostics.Debug.WriteLine("[GlobalData] ✓ Updated menu config saved to API");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[GlobalData] ✓ All menu tabs are valid, no cleanup needed");
                }
                
                System.Diagnostics.Debug.WriteLine("[GlobalData] ========== Menu tab validation complete ==========");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlobalData] Error validating menu tabs: {ex.Message}");
                // Don't throw - validation is optional, cache can still be used
            }
        }
        
        /// <summary>
        /// Updates the cached menu configuration after save
        /// Called when user saves menu tab changes
        /// </summary>
        public void UpdateCachedMenuConfig(MenuConfigModel config)
        {
            _cachedMenuConfig = config;
            System.Diagnostics.Debug.WriteLine("[GlobalData] Updated cached menu config");
        }
        
        /// <summary>
        /// Notifies all listeners that menu data has been refreshed
        /// Called when colors or menu structure changes without a full API refresh
        /// </summary>
        public void NotifyMenuDataRefreshed()
        {
            System.Diagnostics.Debug.WriteLine("[GlobalData] Notifying listeners that menu data was refreshed");
            
            // Refresh product colors in cache before notifying
            RefreshProductColors();
            
            MenuDataRefreshed?.Invoke();
        }
        
        /// <summary>
        /// Clears all cached menu data
        /// Called on logout
        /// </summary>
        public void ClearMenuCache()
        {
            _cachedCategories = null;
            _cachedProducts = null;
            _cachedMenuConfig = null;
            _cachedFloorPlans = null;
            _cachedFloorPlanCustomItemTypes = null;
            _floorPlanLayoutEnabled = false;
            System.Diagnostics.Debug.WriteLine("[GlobalData] Menu + floor plan cache cleared");
        }
    }
} 