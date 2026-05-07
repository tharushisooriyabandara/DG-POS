using POS_UI.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using POS_UI.Models;
using System.Windows;
using POS_UI.Services;
using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using System.Drawing.Printing;
using System.Drawing;
using System.Runtime.InteropServices;
using System;
using System.IO;
using Newtonsoft.Json;
using System.Management;
using POS_UI.View;
using POS_UI.View.Dialogs;
using POS_UI.Helpers;
using MaterialDesignThemes.Wpf;
using System.Threading.Tasks;
using POS_UI.Models;
using System.Text.Json;
using System.Text;
namespace POS_UI.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {private PlatformModel _currentPlatformForSnooze;
        private System.Windows.Controls.Primitives.ToggleButton _currentToggleButton;
        private readonly TokenValidationService _tokenValidationService;
        private readonly SettingsService _settingsService;
        public ObservableCollection<PlatformModel> Platforms { get; set; }
        public ObservableCollection<PrinterModel> Printers => PrintersService.Instance.Printers;
        public ObservableCollection<CardMachineModel> CardMachines => CardMachineService.Instance.CardMachines;
        public ICommand SwitchTabCommand { get; }
        public ICommand StartShiftCommand { get; }
        public ICommand OpenEndShiftDialogCommand { get; }
        public ICommand OpenCashInDialogCommand { get; }
        public ICommand OpenCashOutDialogCommand { get; }
        //public ICommand PrintXReportCommand { get; }
        public decimal EndShiftCashAmount { get; set; }
        
        private string _selectedTab;
        private readonly ApiService _apiService;
        public string SelectedTab
        {
            get => _selectedTab;
            set { _selectedTab = value; OnPropertyChanged(); }
        }
        
        // Admin tab properties
        private string _selectedAdminTab = "ShiftDetails";
        public string SelectedAdminTab
        {
            get => _selectedAdminTab;
            set { _selectedAdminTab = value; OnPropertyChanged(); }
        }
        public ICommand SwitchAdminTabCommand { get; }
        
        // Printer subtab properties
        private string _selectedPrinterSubTab = "ConnectedPrinters";
        public string SelectedPrinterSubTab
        {
            get => _selectedPrinterSubTab;
            set { _selectedPrinterSubTab = value; OnPropertyChanged(); }
        }
        public ICommand SwitchPrinterSubTabCommand { get; }

        // Configure Orders subtab properties
        private string _selectedConfigureOrdersSubTab = "OrdersPage";
        public string SelectedConfigureOrdersSubTab
        {
            get => _selectedConfigureOrdersSubTab;
            set { _selectedConfigureOrdersSubTab = value; OnPropertyChanged(); }
        }
        public ICommand SwitchConfigureOrdersSubTabCommand { get; }
        public ICommand OpenIdleLogoutTimerCommand { get; }

        // Item Discount presets (up to 4 configurable quick-discount buttons)
        private string _discountPreset1 = "";
        public string DiscountPreset1
        {
            get => _discountPreset1;
            set { _discountPreset1 = value; OnPropertyChanged(); }
        }
        private string _discountPreset2 = "";
        public string DiscountPreset2
        {
            get => _discountPreset2;
            set { _discountPreset2 = value; OnPropertyChanged(); }
        }
        private string _discountPreset3 = "";
        public string DiscountPreset3
        {
            get => _discountPreset3;
            set { _discountPreset3 = value; OnPropertyChanged(); }
        }
        private string _discountPreset4 = "";
        public string DiscountPreset4
        {
            get => _discountPreset4;
            set { _discountPreset4 = value; OnPropertyChanged(); }
        }
        public ICommand SaveItemDiscountPresetsCommand { get; }

        private bool _isSavingItemDiscounts;
        public bool IsSavingItemDiscounts
        {
            get => _isSavingItemDiscounts;
            set { _isSavingItemDiscounts = value; OnPropertyChanged(); }
        }

        private void LoadItemDiscountPresets()
        {
            var service = new POS_UI.Services.ItemDiscountService();
            var presets = service.LoadPresets();
            _discountPreset1 = presets.Count > 0 ? presets[0].ToString("G29") : "";
            _discountPreset2 = presets.Count > 1 ? presets[1].ToString("G29") : "";
            _discountPreset3 = presets.Count > 2 ? presets[2].ToString("G29") : "";
            _discountPreset4 = presets.Count > 3 ? presets[3].ToString("G29") : "";
            OnPropertyChanged(nameof(DiscountPreset1));
            OnPropertyChanged(nameof(DiscountPreset2));
            OnPropertyChanged(nameof(DiscountPreset3));
            OnPropertyChanged(nameof(DiscountPreset4));
        }

        private void SaveItemDiscountPresets()
        {
            try
            {
                var presets = new System.Collections.Generic.List<decimal>();
                if (decimal.TryParse(_discountPreset1, out decimal v1) && v1 > 0 && v1 <= 100) presets.Add(v1);
                if (decimal.TryParse(_discountPreset2, out decimal v2) && v2 > 0 && v2 <= 100) presets.Add(v2);
                if (decimal.TryParse(_discountPreset3, out decimal v3) && v3 > 0 && v3 <= 100) presets.Add(v3);
                if (decimal.TryParse(_discountPreset4, out decimal v4) && v4 > 0 && v4 <= 100) presets.Add(v4);

                var service = new POS_UI.Services.ItemDiscountService();
                service.SavePresets(presets);
                POS_UI.Services.GlobalDataService.Instance.ItemDiscountPresets = presets.Count > 0 ? presets : new System.Collections.Generic.List<decimal> { 10, 20 };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] SaveItemDiscountPresets error: {ex.Message}");
            }
        }

        // Configure Orders: which page to use (false = Orders Page, true = Live Orders Page); synced to GlobalDataService for sidebar
        private bool _useLiveOrdersPage;
        private int _idleLogoutMinutes = 10;
        public int IdleLogoutMinutes
        {
            get => _idleLogoutMinutes;
            set
            {
                var clamped = Math.Max(1, Math.Min(120, value));
                if (_idleLogoutMinutes == clamped) return;
                _idleLogoutMinutes = clamped;
                OnPropertyChanged();
                try { GlobalDataService.Instance.IdleLogoutMinutes = clamped; } catch { }
                _ = SaveOrderConfigWithIdleAsync();
            }
        }
        public bool UseLiveOrdersPage
        {
            get => _useLiveOrdersPage;
            set
            {
                if (_useLiveOrdersPage == value) return;
                _useLiveOrdersPage = value;
                OnPropertyChanged();
                try { GlobalDataService.Instance.UseLiveOrdersPage = value; } catch { }
                _ = SaveOrderConfigAsync(value);
            }
        }

        private bool _isSavingOrderConfig;
        public bool IsSavingOrderConfig
        {
            get => _isSavingOrderConfig;
            set { _isSavingOrderConfig = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsAutoCompleteOrderConfigBusy)); }
        }

        private bool _isLoadingAutoCompleteOrderConfig;
        public bool IsLoadingAutoCompleteOrderConfig
        {
            get => _isLoadingAutoCompleteOrderConfig;
            set { _isLoadingAutoCompleteOrderConfig = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsAutoCompleteOrderConfigBusy)); }
        }

        private bool _isLoadingOrderConfigForGeneral;
        public bool IsLoadingOrderConfigForGeneral
        {
            get => _isLoadingOrderConfigForGeneral;
            set { _isLoadingOrderConfigForGeneral = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsGeneralOrderConfigBusy)); }
        }

        /// <summary>True when loading or saving auto-complete order config (used to show loader and disable toggles).</summary>
        public bool IsAutoCompleteOrderConfigBusy => IsSavingOrderConfig || IsLoadingAutoCompleteOrderConfig;

        /// <summary>True when loading or saving General tab order config (used to show loader and disable controls).</summary>
        public bool IsGeneralOrderConfigBusy => IsSavingOrderConfig || IsLoadingOrderConfigForGeneral;

        // Auto complete Orders: include takeaway / dine in / delivery
        private bool _includeTakeaway = false;
        public bool IncludeTakeaway
        {
            get => _includeTakeaway;
            set
            {
                if (_includeTakeaway == value) return;
                _includeTakeaway = value;
                OnPropertyChanged();
                if (value)
                    _ = ShowTimerSelectionWhenEnabledAsync("Takeaway", m => { _takeawayTimerMinutes = m; OnPropertyChanged(nameof(TakeawayTimerMinutes)); });
                else
                {
                    _takeawayTimerMinutes = 0;
                    OnPropertyChanged(nameof(TakeawayTimerMinutes));
                    _ = SaveAutoCompleteOrderConfigAsync(null, null);
                }
            }
        }

        private bool _includeDineIn = false;
        public bool IncludeDineIn
        {
            get => _includeDineIn;
            set
            {
                if (_includeDineIn == value) return;
                _includeDineIn = value;
                OnPropertyChanged();
                if (value)
                    _ = ShowTimerSelectionWhenEnabledAsync("Dine in", m => { _dineInTimerMinutes = m; OnPropertyChanged(nameof(DineInTimerMinutes)); });
                else
                {
                    _dineInTimerMinutes = 0;
                    OnPropertyChanged(nameof(DineInTimerMinutes));
                    _ = SaveAutoCompleteOrderConfigAsync(null, null);
                }
            }
        }

        private bool _includeDelivery = false;
        public bool IncludeDelivery
        {
            get => _includeDelivery;
            set
            {
                if (_includeDelivery == value) return;
                _includeDelivery = value;
                OnPropertyChanged();
                if (value)
                    _ = ShowTimerSelectionWhenEnabledAsync("Delivery", m => { _deliveryTimerMinutes = m; OnPropertyChanged(nameof(DeliveryTimerMinutes)); });
                else
                {
                    _deliveryTimerMinutes = 0;
                    OnPropertyChanged(nameof(DeliveryTimerMinutes));
                    _ = SaveAutoCompleteOrderConfigAsync(null, null);
                }
            }
        }

        private int _takeawayTimerMinutes = 0;
        public int TakeawayTimerMinutes { get => _takeawayTimerMinutes; set { if (_takeawayTimerMinutes == value) return; _takeawayTimerMinutes = value; OnPropertyChanged(); } }

        private int _dineInTimerMinutes = 0;
        public int DineInTimerMinutes { get => _dineInTimerMinutes; set { if (_dineInTimerMinutes == value) return; _dineInTimerMinutes = value; OnPropertyChanged(); } }

        private int _deliveryTimerMinutes = 0;
        public int DeliveryTimerMinutes { get => _deliveryTimerMinutes; set { if (_deliveryTimerMinutes == value) return; _deliveryTimerMinutes = value; OnPropertyChanged(); } }

        private async Task ShowTimerSelectionWhenEnabledAsync(string orderTypeName, Action<int> setTimerMinutes)
        {
            int initialMinutes = orderTypeName == "Takeaway" ? _takeawayTimerMinutes : orderTypeName == "Dine in" ? _dineInTimerMinutes : _deliveryTimerMinutes;
            var vm = new TimerSelectionDialogViewModel(initialMinutes, orderTypeName, "RootDialog");
            var dialog = new TimerSelectionDialog { DataContext = vm };
            var result = await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog");
            if (result is int minutes)
            {
                setTimerMinutes(minutes);
                // Save current state of all three order types (Takeaway stays 10 mins when user then sets Dine in to 20 mins).
                await SaveAutoCompleteOrderConfigAsync(orderTypeName, minutes);
            }
        }

        /// <summary>Current cashier display order id for terminal order config</summary>
        private static string GetOrderConfigDisplayOrderId()
        {
            var cart = CartService.Instance;
            var id = cart?.CashierSessionDisplayOrderId ?? cart?.DisplayOrderId;
            return string.IsNullOrWhiteSpace(id) ? "" : id.Trim();
        }

        private async Task SaveOrderConfigAsync(bool useLiveOrdersPage)
        {
            IsSavingOrderConfig = true;
            try
            {
                var (_, outletCode, brandIdStr) = _settingsService.LoadSettings();
                if (string.IsNullOrWhiteSpace(outletCode) || string.IsNullOrWhiteSpace(brandIdStr) || !int.TryParse(brandIdStr, out int brandId))
                    return;
                var shopDetails = await _apiService.GetShopDetailsAsync(outletCode, brandIdStr);
                if (shopDetails == null || shopDetails.Id <= 0) return;
                // Send full config so auto-complete settings are not overwritten; only page_name and is_live_orders_page change.
                var gds = GlobalDataService.Instance;
                var orderConfig = new
                {
                    page_name = useLiveOrdersPage ? "live_orders" : "orders",
                    is_live_orders_page = useLiveOrdersPage,
                    is_takeaway = gds?.IsTakeawayAutoCompleteEnabled ?? _includeTakeaway,
                    takeaway_timer_mins = gds != null ? gds.TakeawayAutoCompleteTimerMins : (_includeTakeaway ? _takeawayTimerMinutes : 0),
                    is_dinein = gds?.IsDineInAutoCompleteEnabled ?? _includeDineIn,
                    dinein_timer_mins = gds != null ? gds.DineInAutoCompleteTimerMins : (_includeDineIn ? _dineInTimerMinutes : 0),
                    is_delivery = gds?.IsDeliveryAutoCompleteEnabled ?? _includeDelivery,
                    delivery_timer_mins = gds != null ? gds.DeliveryAutoCompleteTimerMins : (_includeDelivery ? _deliveryTimerMinutes : 0),
                    idle_logout_minutes = _idleLogoutMinutes,
                    display_order_id = GetOrderConfigDisplayOrderId(),
                    ongoing_order = System.Array.Empty<object>()
                };
                var orderConfigJson = JsonConvert.SerializeObject(orderConfig);
                var success = await _apiService.SaveOrderConfigAsync(shopDetails.Id, brandId, orderConfigJson);
                if (success)
                {
                    var successVm = StatusDialogViewModel.CreateSuccess(
                        useLiveOrdersPage ? "Live Orders Page Enabled" : "Orders Page Enabled",
                        useLiveOrdersPage ? "The Live Orders page has been enabled successfully." : "The Orders page has been set successfully.");
                    var successDlg = new StatusDialog { DataContext = successVm };
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        MaterialDesignThemes.Wpf.DialogHost.Show(successDlg, "RootDialog"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] SaveOrderConfigAsync error: {ex.Message}");
            }
            finally
            {
                IsSavingOrderConfig = false;
            }
        }

        /// <summary>Saves order config with current idle logout minutes (no dialog). Called when user changes IdleLogoutMinutes.</summary>
        private async Task SaveOrderConfigWithIdleAsync()
        {
            IsSavingOrderConfig = true;
            try
            {
                var (_, outletCode, brandIdStr) = _settingsService.LoadSettings();
                if (string.IsNullOrWhiteSpace(outletCode) || string.IsNullOrWhiteSpace(brandIdStr) || !int.TryParse(brandIdStr, out int brandId))
                    return;
                var shopDetails = await _apiService.GetShopDetailsAsync(outletCode, brandIdStr);
                if (shopDetails == null || shopDetails.Id <= 0) return;
                var gds = GlobalDataService.Instance;
                var orderConfig = new
                {
                    page_name = _useLiveOrdersPage ? "live_orders" : "orders",
                    is_live_orders_page = _useLiveOrdersPage,
                    is_takeaway = gds?.IsTakeawayAutoCompleteEnabled ?? _includeTakeaway,
                    takeaway_timer_mins = gds != null ? gds.TakeawayAutoCompleteTimerMins : (_includeTakeaway ? _takeawayTimerMinutes : 0),
                    is_dinein = gds?.IsDineInAutoCompleteEnabled ?? _includeDineIn,
                    dinein_timer_mins = gds != null ? gds.DineInAutoCompleteTimerMins : (_includeDineIn ? _dineInTimerMinutes : 0),
                    is_delivery = gds?.IsDeliveryAutoCompleteEnabled ?? _includeDelivery,
                    delivery_timer_mins = gds != null ? gds.DeliveryAutoCompleteTimerMins : (_includeDelivery ? _deliveryTimerMinutes : 0),
                    idle_logout_minutes = _idleLogoutMinutes,
                    display_order_id = GetOrderConfigDisplayOrderId(),
                    ongoing_order = System.Array.Empty<object>()
                };
                var orderConfigJson = JsonConvert.SerializeObject(orderConfig);
                await _apiService.SaveOrderConfigAsync(shopDetails.Id, brandId, orderConfigJson);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] SaveOrderConfigWithIdleAsync error: {ex.Message}");
            }
            finally
            {
                IsSavingOrderConfig = false;
            }
        }

        /// <summary>
        /// Saves auto-complete order config using current state of all three order types.
        /// Request body includes page_name, is_live_orders_page, is_takeaway, takeaway_timer_mins, is_dinein, dinein_timer_mins, is_delivery, delivery_timer_mins.
        /// When justConfiguredOrderTypeName/justConfiguredTimerMins are null (e.g. user unselected a toggle), shows generic success message.
        /// </summary>
        private async Task SaveAutoCompleteOrderConfigAsync(string justConfiguredOrderTypeName, int? justConfiguredTimerMins)
        {
            var orderConfig = new
            {
                page_name = "live_orders",
                is_live_orders_page = true,
                is_takeaway = _includeTakeaway,
                takeaway_timer_mins = _includeTakeaway ? _takeawayTimerMinutes : 0,
                is_dinein = _includeDineIn,
                dinein_timer_mins = _includeDineIn ? _dineInTimerMinutes : 0,
                is_delivery = _includeDelivery,
                delivery_timer_mins = _includeDelivery ? _deliveryTimerMinutes : 0,
                idle_logout_minutes = _idleLogoutMinutes,
                display_order_id = GetOrderConfigDisplayOrderId(),
                ongoing_order = System.Array.Empty<object>()
            };
            var orderConfigJson = JsonConvert.SerializeObject(orderConfig);
            IsSavingOrderConfig = true;
            try
            {
                var (_, outletCode, brandIdStr) = _settingsService.LoadSettings();
                if (string.IsNullOrWhiteSpace(outletCode) || string.IsNullOrWhiteSpace(brandIdStr) || !int.TryParse(brandIdStr, out int brandId))
                    return;
                var shopDetails = await _apiService.GetShopDetailsAsync(outletCode, brandIdStr);
                if (shopDetails == null || shopDetails.Id <= 0) return;
                var success = await _apiService.SaveOrderConfigAsync(shopDetails.Id, brandId, orderConfigJson);
                if (success)
                {
                    GlobalDataService.Instance.UpdateAutoCompleteOrderConfig(
                        _includeTakeaway, _takeawayTimerMinutes,
                        _includeDineIn, _dineInTimerMinutes,
                        _includeDelivery, _deliveryTimerMinutes);
                    string title = justConfiguredOrderTypeName != null && justConfiguredTimerMins.HasValue
                        ? "Auto-Complete Enabled"
                        : "Auto-Complete Disabled";
                    string message = justConfiguredOrderTypeName != null && justConfiguredTimerMins.HasValue
                        ? $"{justConfiguredOrderTypeName} orders will be automatically completed after {justConfiguredTimerMins} minute(s)."
                        : "Orders require manual completion.";
                    var successVm = StatusDialogViewModel.CreateSuccess(title, message);
                    var successDlg = new StatusDialog { DataContext = successVm };
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        MaterialDesignThemes.Wpf.DialogHost.Show(successDlg, "RootDialog"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] SaveAutoCompleteOrderConfigAsync error: {ex.Message}");
            }
            finally
            {
                IsSavingOrderConfig = false;
            }
        }

        /// <summary>
        /// Loads auto-complete order config from GetOrderConfigAsync and updates toggle state and timer values for the Auto Complete Orders tab.
        /// </summary>
        public async Task LoadAutoCompleteOrderConfigFromApiAsync()
        {
            IsLoadingAutoCompleteOrderConfig = true;
            try
            {
                var (_, outletCode, brandIdStr) = _settingsService.LoadSettings();
                if (string.IsNullOrWhiteSpace(outletCode) || string.IsNullOrWhiteSpace(brandIdStr) || !int.TryParse(brandIdStr, out int brandIdInt))
                    return;
                var shop = GlobalDataService.Instance?.ShopDetails;
                if (shop == null || shop.Id <= 0)
                {
                    shop = await _apiService.GetShopDetailsAsync(outletCode, brandIdStr);
                    if (shop == null || shop.Id <= 0) return;
                }
                var orderConfigJson = await _apiService.GetOrderConfigAsync(shop.Id, brandIdInt);
                if (string.IsNullOrEmpty(orderConfigJson)) return;

                using var doc = JsonDocument.Parse(orderConfigJson);
                var root = doc.RootElement;
                var config = root;
                if (root.TryGetProperty("data", out var dataEl))
                {
                    config = dataEl;
                    if (dataEl.TryGetProperty("config", out var configEl))
                        config = configEl;
                }

                bool isTakeaway = false, isDineIn = false, isDelivery = false;
                int takeawayMins = 0, dineInMins = 0, deliveryMins = 0;

                if (config.TryGetProperty("is_takeaway", out var isTakeawayEl))
                    isTakeaway = isTakeawayEl.ValueKind == JsonValueKind.True
                        || (isTakeawayEl.ValueKind == JsonValueKind.String && string.Equals(isTakeawayEl.GetString(), "true", StringComparison.OrdinalIgnoreCase))
                        || (isTakeawayEl.ValueKind == JsonValueKind.Number && isTakeawayEl.GetInt32() != 0);
                if (config.TryGetProperty("takeaway_timer_mins", out var takeawayMinsEl) && takeawayMinsEl.ValueKind == JsonValueKind.Number)
                    takeawayMins = Math.Max(0, takeawayMinsEl.GetInt32());
                if (config.TryGetProperty("is_dinein", out var isDineInEl))
                    isDineIn = isDineInEl.ValueKind == JsonValueKind.True
                        || (isDineInEl.ValueKind == JsonValueKind.String && string.Equals(isDineInEl.GetString(), "true", StringComparison.OrdinalIgnoreCase))
                        || (isDineInEl.ValueKind == JsonValueKind.Number && isDineInEl.GetInt32() != 0);
                if (config.TryGetProperty("dinein_timer_mins", out var dineInMinsEl) && dineInMinsEl.ValueKind == JsonValueKind.Number)
                    dineInMins = Math.Max(0, dineInMinsEl.GetInt32());
                if (config.TryGetProperty("is_delivery", out var isDeliveryEl))
                    isDelivery = isDeliveryEl.ValueKind == JsonValueKind.True
                        || (isDeliveryEl.ValueKind == JsonValueKind.String && string.Equals(isDeliveryEl.GetString(), "true", StringComparison.OrdinalIgnoreCase))
                        || (isDeliveryEl.ValueKind == JsonValueKind.Number && isDeliveryEl.GetInt32() != 0);
                if (config.TryGetProperty("delivery_timer_mins", out var deliveryMinsEl) && deliveryMinsEl.ValueKind == JsonValueKind.Number)
                    deliveryMins = Math.Max(0, deliveryMinsEl.GetInt32());

                if (config.TryGetProperty("idle_logout_minutes", out var idleMinsEl) && idleMinsEl.ValueKind == JsonValueKind.Number)
                {
                    var idleMins = Math.Max(1, Math.Min(120, idleMinsEl.GetInt32()));
                    _idleLogoutMinutes = idleMins;
                    OnPropertyChanged(nameof(IdleLogoutMinutes));
                    try { GlobalDataService.Instance.IdleLogoutMinutes = idleMins; } catch { }
                }
                if (config.TryGetProperty("is_live_orders_page", out var isLiveEl))
                {
                    var isLive = isLiveEl.ValueKind == JsonValueKind.True
                        || (isLiveEl.ValueKind == JsonValueKind.String && string.Equals(isLiveEl.GetString(), "true", StringComparison.OrdinalIgnoreCase))
                        || (isLiveEl.ValueKind == JsonValueKind.Number && isLiveEl.GetInt32() != 0);
                    _useLiveOrdersPage = isLive;
                    OnPropertyChanged(nameof(UseLiveOrdersPage));
                    try { GlobalDataService.Instance.UseLiveOrdersPage = isLive; } catch { }
                }

                _includeTakeaway = isTakeaway;
                _takeawayTimerMinutes = takeawayMins;
                _includeDineIn = isDineIn;
                _dineInTimerMinutes = dineInMins;
                _includeDelivery = isDelivery;
                _deliveryTimerMinutes = deliveryMins;
                OnPropertyChanged(nameof(IncludeTakeaway));
                OnPropertyChanged(nameof(TakeawayTimerMinutes));
                OnPropertyChanged(nameof(IncludeDineIn));
                OnPropertyChanged(nameof(DineInTimerMinutes));
                OnPropertyChanged(nameof(IncludeDelivery));
                OnPropertyChanged(nameof(DeliveryTimerMinutes));

                try { GlobalDataService.Instance?.UpdateAutoCompleteOrderConfig(isTakeaway, takeawayMins, isDineIn, dineInMins, isDelivery, deliveryMins); } catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] LoadAutoCompleteOrderConfigFromApiAsync error: {ex.Message}");
            }
            finally
            {
                IsLoadingAutoCompleteOrderConfig = false;
            }
        }

        /// <summary>Opens IdleLogoutTimerSelectionDialog; on Save, sets IdleLogoutMinutes (which triggers SaveOrderConfig).</summary>
        private async Task OpenIdleLogoutTimerAsync()
        {
            var vm = new IdleLogoutTimerSelectionDialogViewModel(_idleLogoutMinutes, "RootDialog");
            var dialog = new POS_UI.View.IdleLogoutTimerSelectionDialog { DataContext = vm };
            var result = await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog");
            if (result is int minutes)
            {
                IdleLogoutMinutes = minutes;
            }
        }

        /// <summary>Loads order config when opening General sub-tab and updates UseLiveOrdersPage and IdleLogoutMinutes.</summary>
        public async Task LoadOrderConfigForGeneralAsync()
        {
            IsLoadingOrderConfigForGeneral = true;
            try
            {
                var (_, outletCode, brandIdStr) = _settingsService.LoadSettings();
                if (string.IsNullOrWhiteSpace(outletCode) || string.IsNullOrWhiteSpace(brandIdStr) || !int.TryParse(brandIdStr, out int brandIdInt))
                    return;
                var shop = GlobalDataService.Instance?.ShopDetails;
                if (shop == null || shop.Id <= 0)
                {
                    shop = await _apiService.GetShopDetailsAsync(outletCode, brandIdStr);
                    if (shop == null || shop.Id <= 0) return;
                }
                var orderConfigJson = await _apiService.GetOrderConfigAsync(shop.Id, brandIdInt);
                if (string.IsNullOrEmpty(orderConfigJson)) return;

                using var doc = JsonDocument.Parse(orderConfigJson);
                var root = doc.RootElement;
                var config = root;
                if (root.TryGetProperty("data", out var dataEl))
                {
                    config = dataEl;
                    if (dataEl.TryGetProperty("config", out var configEl))
                        config = configEl;
                }

                if (config.TryGetProperty("is_live_orders_page", out var isLiveEl))
                {
                    var isLive = isLiveEl.ValueKind == JsonValueKind.True
                        || (isLiveEl.ValueKind == JsonValueKind.String && string.Equals(isLiveEl.GetString(), "true", StringComparison.OrdinalIgnoreCase))
                        || (isLiveEl.ValueKind == JsonValueKind.Number && isLiveEl.GetInt32() != 0);
                    _useLiveOrdersPage = isLive;
                    OnPropertyChanged(nameof(UseLiveOrdersPage));
                    try { GlobalDataService.Instance.UseLiveOrdersPage = isLive; } catch { }
                }
                if (config.TryGetProperty("idle_logout_minutes", out var idleMinsEl) && idleMinsEl.ValueKind == JsonValueKind.Number)
                {
                    var idleMins = Math.Max(1, Math.Min(120, idleMinsEl.GetInt32()));
                    _idleLogoutMinutes = idleMins;
                    OnPropertyChanged(nameof(IdleLogoutMinutes));
                    try { GlobalDataService.Instance.IdleLogoutMinutes = idleMins; } catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] LoadOrderConfigForGeneralAsync error: {ex.Message}");
            }
            finally
            {
                IsLoadingOrderConfigForGeneral = false;
            }
        }

        // Printer Groups from Shop Details
        private ObservableCollection<PrinterGroupModel> _printerGroups = new ObservableCollection<PrinterGroupModel>();
        public ObservableCollection<PrinterGroupModel> PrinterGroups
        {
            get => _printerGroups;
            set { _printerGroups = value; OnPropertyChanged(); }
        }
        
        public UserModel LoggedInUser { get; set; }
        public string ShiftTimer { get; set; }
        public string OutletName { get; set; }
        public string CurrentPage { get; set; }
        public string Note { get; set; }
        public ICommand RequestPinChangeCommand { get; }
        public ICommand RefreshPlatformsCommand { get; }
        private bool _isPinDialogOpen;
        public bool IsPinDialogOpen
        {
            get => _isPinDialogOpen;
            set { _isPinDialogOpen = value; OnPropertyChanged(); }
        }
        private bool _isLoadingPlatforms;
        public bool IsLoadingPlatforms
        {
            get => _isLoadingPlatforms;
            set { _isLoadingPlatforms = value; OnPropertyChanged(); }
        }
        
        private bool _isPrintingZReport;
        public bool IsPrintingZReport
        {
            get => _isPrintingZReport;
            set { _isPrintingZReport = value; OnPropertyChanged(); }
        }
        
        // Menu Configuration properties
        private ObservableCollection<MenuTabModel> _menuTabs = new ObservableCollection<MenuTabModel>();
        public ObservableCollection<MenuTabModel> MenuTabs
        {
            get => _menuTabs;
            set { _menuTabs = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanAddMoreTabs)); }
        }
        
        private bool _isLoadingMenuConfig;
        public bool IsLoadingMenuConfig
        {
            get => _isLoadingMenuConfig;
            set { _isLoadingMenuConfig = value; OnPropertyChanged(); }
        }
        
        private bool _hasUnsavedChanges;
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set { _hasUnsavedChanges = value; OnPropertyChanged(); }
        }
        
        public bool CanAddMoreTabs => MenuTabs.Count < 5;
        
        // Cached menu data for tab editor
        private ObservableCollection<string> _availableCategories = new ObservableCollection<string>();
        private ObservableCollection<POS_UI.Models.ProductItemModel> _availableProducts = new ObservableCollection<POS_UI.Models.ProductItemModel>();
        
        public ICommand AddMenuTabCommand { get; }
        public ICommand EditMenuTabCommand { get; }
        public ICommand DeleteMenuTabCommand { get; }
        public ICommand MoveTabUpCommand { get; }
        public ICommand MoveTabDownCommand { get; }
        public ICommand SaveMenuConfigCommand { get; }
        public ICommand LoadMenuConfigCommand { get; }

        // Floor plan configuration
        private ObservableCollection<FloorPlanModel> _floorPlans = new ObservableCollection<FloorPlanModel>();
        public ObservableCollection<FloorPlanModel> FloorPlans
        {
            get => _floorPlans;
            set { _floorPlans = value; OnPropertyChanged(); }
        }

        private FloorPlanModel? _selectedFloorPlan;
        public FloorPlanModel? SelectedFloorPlan
        {
            get => _selectedFloorPlan;
            set { _selectedFloorPlan = value; OnPropertyChanged(); }
        }

        private FloorPlanModel? _editingFloorPlan;
        public FloorPlanModel? EditingFloorPlan
        {
            get => _editingFloorPlan;
            set { _editingFloorPlan = value; OnPropertyChanged(); }
        }

        private FloorPlanModel? _editingFloorPlanDraft;
        public FloorPlanModel? EditingFloorPlanDraft
        {
            get => _editingFloorPlanDraft;
            set { _editingFloorPlanDraft = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEditingFloorPlan)); }
        }

        public bool IsEditingFloorPlan => EditingFloorPlanDraft != null;

        private ObservableCollection<TableModel> _availableFloorPlanTables = new ObservableCollection<TableModel>();
        public ObservableCollection<TableModel> AvailableFloorPlanTables
        {
            get => _availableFloorPlanTables;
            set { _availableFloorPlanTables = value; OnPropertyChanged(); }
        }

        private TableModel? _selectedAvailableFloorPlanTable;
        public TableModel? SelectedAvailableFloorPlanTable
        {
            get => _selectedAvailableFloorPlanTable;
            set { _selectedAvailableFloorPlanTable = value; OnPropertyChanged(); }
        }

        private FloorPlanTablePlacementModel? _selectedPlacedFloorPlanTable;
        public FloorPlanTablePlacementModel? SelectedPlacedFloorPlanTable
        {
            get => _selectedPlacedFloorPlanTable;
            set
            {
                if (value == null && EditingFloorPlanDraft != null)
                {
                    foreach (var t in EditingFloorPlanDraft.Tables)
                    {
                        t.IsSelectedOnCanvas = false;
                    }
                }

                _selectedPlacedFloorPlanTable = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedFloorPlanTable));
            }
        }

        public bool HasSelectedFloorPlanTable => SelectedPlacedFloorPlanTable != null;

        public ObservableCollection<FloorPlanShapeType> FloorPlanShapeOptions { get; } = new ObservableCollection<FloorPlanShapeType>
        {
            FloorPlanShapeType.Rectangle,
            FloorPlanShapeType.Square,
            FloorPlanShapeType.Circle,
            FloorPlanShapeType.Oval,
            FloorPlanShapeType.Pill,
            FloorPlanShapeType.Rounded,
            FloorPlanShapeType.Parallelogram,
            FloorPlanShapeType.Diamond
        };

        public List<string> FloorPlanColorPalette { get; } = ColorPalette.GetAllColors();

        /// <summary>Catalog entries for non-table floor elements (merged from API + built-in defaults).</summary>
        public ObservableCollection<FloorPlanCustomItemTypeModel> FloorPlanCustomItemTypes { get; } = new ObservableCollection<FloorPlanCustomItemTypeModel>();

        private bool _isFloorPlanColorPickerOpen;
        public bool IsFloorPlanColorPickerOpen
        {
            get => _isFloorPlanColorPickerOpen;
            set { _isFloorPlanColorPickerOpen = value; OnPropertyChanged(); }
        }

        public ICommand ToggleFloorPlanColorPickerCommand { get; }

        private bool _isLoadingFloorPlanTables;
        public bool IsLoadingFloorPlanTables
        {
            get => _isLoadingFloorPlanTables;
            set { _isLoadingFloorPlanTables = value; OnPropertyChanged(); }
        }

        private bool _isSavingFloorPlan;
        public bool IsSavingFloorPlan
        {
            get => _isSavingFloorPlan;
            set
            {
                _isSavingFloorPlan = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanEditFloorPlanSettings));
            }
        }

        private bool _isFloorPlanLayoutToggleBusy;
        public bool IsFloorPlanLayoutToggleBusy
        {
            get => _isFloorPlanLayoutToggleBusy;
            set
            {
                _isFloorPlanLayoutToggleBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanEditFloorPlanSettings));
            }
        }

        private bool _suppressFloorPlanLayoutPersist;
        private bool _isFloorPlanLayoutEnabled;
        /// <summary>When true, Cashier dine-in uses floor plan layouts for table selection. Persisted in floor plan config JSON.</summary>
        public bool IsFloorPlanLayoutEnabled
        {
            get => _isFloorPlanLayoutEnabled;
            set
            {
                if (_isFloorPlanLayoutEnabled == value)
                {
                    return;
                }

                if (_suppressFloorPlanLayoutPersist)
                {
                    _isFloorPlanLayoutEnabled = value;
                    OnPropertyChanged();
                    return;
                }

                var previous = _isFloorPlanLayoutEnabled;
                _isFloorPlanLayoutEnabled = value;
                OnPropertyChanged();
                _ = PersistFloorPlanLayoutToggleAsync(previous, value);
            }
        }

        public bool CanEditFloorPlanSettings => !IsSavingFloorPlan && !IsFloorPlanLayoutToggleBusy;

        public ICommand LoadFloorPlanDataCommand { get; }
        public ICommand AddFloorPlanCommand { get; }
        public ICommand EditFloorPlanCommand { get; }
        public ICommand DeleteFloorPlanCommand { get; }
        public ICommand AddTableToFloorPlanCommand { get; }
        public ICommand AddCustomItemToFloorPlanCommand { get; }
        public ICommand RemoveTableFromFloorPlanCommand { get; }
        public ICommand ClearFloorPlanCommand { get; }
        public ICommand SaveFloorPlanCommand { get; }
        public ICommand CancelFloorPlanEditCommand { get; }
        public ICommand SetSelectedPlacedFloorPlanTableCommand { get; }
        public ICommand IncreaseFloorPlanTableWidthCommand { get; }
        public ICommand DecreaseFloorPlanTableWidthCommand { get; }
        public ICommand IncreaseFloorPlanTableHeightCommand { get; }
        public ICommand DecreaseFloorPlanTableHeightCommand { get; }
        
        // Custom full-screen dialog properties
        private bool _isMenuDialogOpen;
        public bool IsMenuDialogOpen
        {
            get => _isMenuDialogOpen;
            set { _isMenuDialogOpen = value; OnPropertyChanged(); }
        }
        
        private object _menuDialogContent;
        public object MenuDialogContent
        {
            get => _menuDialogContent;
            set { _menuDialogContent = value; OnPropertyChanged(); }
        }

        private bool _isFloorPlanDialogOpen;
        public bool IsFloorPlanDialogOpen
        {
            get => _isFloorPlanDialogOpen;
            set { _isFloorPlanDialogOpen = value; OnPropertyChanged(); }
        }

        private object? _floorPlanDialogContent;
        public object? FloorPlanDialogContent
        {
            get => _floorPlanDialogContent;
            set { _floorPlanDialogContent = value; OnPropertyChanged(); }
        }

        private bool _isPinChangeRequestedDialogOpen;
        public bool IsPinChangeRequestedDialogOpen
        {
            get => _isPinChangeRequestedDialogOpen;
            set { _isPinChangeRequestedDialogOpen = value; OnPropertyChanged(); }
        }
        private string _newPin = "";
        public string NewPin 
        { 
            get => _newPin; 
            set 
            { 
                _newPin = value; 
                OnPropertyChanged(); 
            } 
        }
        
        private string _confirmPin = "";
        public string ConfirmPin 
        { 
            get => _confirmPin; 
            set 
            { 
                _confirmPin = value; 
                OnPropertyChanged(); 
            } 
        }
        
        // Individual PIN digits for the UI
        private string _newPinDigit1 = "";
        public string NewPinDigit1 
        { 
            get => _newPinDigit1; 
            set 
            { 
                _newPinDigit1 = value; 
                UpdateNewPinFromDigits();
                OnPropertyChanged(); 
            } 
        }
        
        private string _newPinDigit2 = "";
        public string NewPinDigit2 
        { 
            get => _newPinDigit2; 
            set 
            { 
                _newPinDigit2 = value; 
                UpdateNewPinFromDigits();
                OnPropertyChanged(); 
            } 
        }
        
        private string _newPinDigit3 = "";
        public string NewPinDigit3 
        { 
            get => _newPinDigit3; 
            set 
            { 
                _newPinDigit3 = value; 
                UpdateNewPinFromDigits();
                OnPropertyChanged(); 
            } 
        }
        
        private string _newPinDigit4 = "";
        public string NewPinDigit4 
        { 
            get => _newPinDigit4; 
            set 
            { 
                _newPinDigit4 = value; 
                UpdateNewPinFromDigits();
                OnPropertyChanged(); 
            } 
        }
        
        private string _newPinDigit5 = "";
        public string NewPinDigit5 
        { 
            get => _newPinDigit5; 
            set 
            { 
                _newPinDigit5 = value; 
                UpdateNewPinFromDigits();
                OnPropertyChanged(); 
            } 
        }
        
        private string _newPinDigit6 = "";
        public string NewPinDigit6 
        { 
            get => _newPinDigit6; 
            set 
            { 
                _newPinDigit6 = value; 
                UpdateNewPinFromDigits();
                OnPropertyChanged(); 
            } 
        }
        
        // Confirm PIN digits
        private string _confirmPinDigit1 = "";
        public string ConfirmPinDigit1 
        { 
            get => _confirmPinDigit1; 
            set 
            { 
                _confirmPinDigit1 = value; 
                UpdateConfirmPinFromDigits();
                OnPropertyChanged(); 
            } 
        }
        
        private string _confirmPinDigit2 = "";
        public string ConfirmPinDigit2 
        { 
            get => _confirmPinDigit2; 
            set 
            { 
                _confirmPinDigit2 = value; 
                UpdateConfirmPinFromDigits();
                OnPropertyChanged(); 
            } 
        }
        
        private string _confirmPinDigit3 = "";
        public string ConfirmPinDigit3 
        { 
            get => _confirmPinDigit3; 
            set 
            { 
                _confirmPinDigit3 = value; 
                UpdateConfirmPinFromDigits();
                OnPropertyChanged(); 
            } 
        }
        
        private string _confirmPinDigit4 = "";
        public string ConfirmPinDigit4 
        { 
            get => _confirmPinDigit4; 
            set 
            { 
                _confirmPinDigit4 = value; 
                UpdateConfirmPinFromDigits();
                OnPropertyChanged(); 
            } 
        }
        
        private string _confirmPinDigit5 = "";
        public string ConfirmPinDigit5 
        { 
            get => _confirmPinDigit5; 
            set 
            { 
                _confirmPinDigit5 = value; 
                UpdateConfirmPinFromDigits();
                OnPropertyChanged(); 
            } 
        }
        
        private string _confirmPinDigit6 = "";
        public string ConfirmPinDigit6 
        { 
            get => _confirmPinDigit6; 
            set 
            { 
                _confirmPinDigit6 = value; 
                UpdateConfirmPinFromDigits();
                OnPropertyChanged(); 
            } 
        }
        public ICommand SetNewPinCommand { get; }
        
        private void UpdateNewPinFromDigits()
        {
            var digits = new[] { NewPinDigit1, NewPinDigit2, NewPinDigit3, NewPinDigit4, NewPinDigit5, NewPinDigit6 };
            NewPin = string.Join("", digits.Where(d => !string.IsNullOrEmpty(d)));
        }
        
        private void UpdateConfirmPinFromDigits()
        {
            var digits = new[] { ConfirmPinDigit1, ConfirmPinDigit2, ConfirmPinDigit3, ConfirmPinDigit4, ConfirmPinDigit5, ConfirmPinDigit6 };
            ConfirmPin = string.Join("", digits.Where(d => !string.IsNullOrEmpty(d)));
        }
        
        private void ClearPinDigits()
        {
            NewPinDigit1 = NewPinDigit2 = NewPinDigit3 = NewPinDigit4 = NewPinDigit5 = NewPinDigit6 = "";
            ConfirmPinDigit1 = ConfirmPinDigit2 = ConfirmPinDigit3 = ConfirmPinDigit4 = ConfirmPinDigit5 = ConfirmPinDigit6 = "";
            NewPin = ConfirmPin = "";
        }
        // Selected user for PIN reset (from Users list)
        public UserModel SelectedUserForPinReset { get; set; }
        // Whether the selected user (target of reset) is an Outlet Admin → show 6-digit PIN
        public bool IsPinTargetOutletAdmin => SelectedUserForPinReset != null &&
            !string.IsNullOrEmpty(SelectedUserForPinReset.Role) &&
            (SelectedUserForPinReset.Role.Replace(" ", "", System.StringComparison.OrdinalIgnoreCase)
                .Equals("OutletAdmin", System.StringComparison.OrdinalIgnoreCase));

        // User details dialog state
        private bool _isUserDetailsDialogOpen;
        public bool IsUserDetailsDialogOpen
        {
            get => _isUserDetailsDialogOpen;
            set { _isUserDetailsDialogOpen = value; OnPropertyChanged(); }
        }
        private UserModel _selectedUserDetails;
        public UserModel SelectedUserDetails
        {
            get => _selectedUserDetails;
            set { _selectedUserDetails = value; OnPropertyChanged(); }
        }
        public ICommand ViewUserDetailsCommand { get; }
        public bool IsAdmin => LoggedInUser != null &&
            !string.IsNullOrEmpty(LoggedInUser.Role) &&
            (LoggedInUser.Role.Replace(" ", "", System.StringComparison.OrdinalIgnoreCase)
                .Equals("OutletAdmin", System.StringComparison.OrdinalIgnoreCase));
        public ObservableCollection<SecuritySettingModel> SecuritySettings { get; set; }
        
        // Cash drawer active session
        private CashDrawerActiveSessionModel _activeCashDrawerSession;
        public CashDrawerActiveSessionModel ActiveCashDrawerSession
        {
            get => _activeCashDrawerSession;
            set { _activeCashDrawerSession = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasActiveCashDrawerSession)); OnPropertyChanged(nameof(CanCashIn)); OnPropertyChanged(nameof(CanCashOut)); }
        }
        public bool HasActiveCashDrawerSession => ActiveCashDrawerSession != null;
        
        // Properties to control cash in/out button states
        public bool CanCashIn => HasActiveCashDrawerSession;
        public bool CanCashOut => HasActiveCashDrawerSession;

        // Cash Session properties
        private List<CashDrawerSessionModel> _allCashSessions = new List<CashDrawerSessionModel>();
        private ObservableCollection<CashDrawerSessionModel> _cashDrawerSessions;
        public ObservableCollection<CashDrawerSessionModel> CashDrawerSessions
        {
            get => _cashDrawerSessions;
            set { _cashDrawerSessions = value; OnPropertyChanged(); }
        }

        private DateTime? _fromDate;
        public DateTime? FromDate
        {
            get => _fromDate;
            set { _fromDate = value; OnPropertyChanged(); }
        }

        private DateTime? _toDate;
        public DateTime? ToDate
        {
            get => _toDate;
            set { _toDate = value; OnPropertyChanged(); }
        }

        private bool _isLoadingCashSessions;
        public bool IsLoadingCashSessions
        {
            get => _isLoadingCashSessions;
            set { _isLoadingCashSessions = value; OnPropertyChanged(); }
        }

        private bool _hasNoCashSessions;
        public bool HasNoCashSessions
        {
            get => _hasNoCashSessions;
            set { _hasNoCashSessions = value; OnPropertyChanged(); }
        }

        // Pagination properties for cash sessions
        private int _cashSessionPageSize = 7;
        private int _cashSessionTotalPages = 1;
        private int _cashSessionCurrentPage = 1;

        public int CashSessionPageSize
        {
            get => _cashSessionPageSize;
            set
            {
                if (value <= 0) return;
                if (_cashSessionPageSize == value) return;
                _cashSessionPageSize = value;
                OnPropertyChanged();
                RecalculateCashSessionPaging(resetToFirstPage: true);
            }
        }

        public int CashSessionCurrentPage
        {
            get => _cashSessionCurrentPage;
            private set
            {
                if (_cashSessionCurrentPage == value) return;
                _cashSessionCurrentPage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CashSessionPageInfoText));
                RefreshCashSessionPaginationCommands();
            }
        }

        public int CashSessionTotalPages
        {
            get => _cashSessionTotalPages;
            private set
            {
                if (_cashSessionTotalPages == value) return;
                _cashSessionTotalPages = Math.Max(1, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(CashSessionPageInfoText));
                RefreshCashSessionPaginationCommands();
            }
        }

        public string CashSessionPageInfoText => $"Page {CashSessionCurrentPage} of {CashSessionTotalPages}";

        public ICommand LoadCashSessionsCommand { get; }
        public ICommand ClearCashSessionDatesCommand { get; }
        public ICommand CashSessionNextPageCommand { get; }
        public ICommand CashSessionPrevPageCommand { get; }
        public ICommand CashSessionFirstPageCommand { get; }
        public ICommand CashSessionLastPageCommand { get; }

        // Cash Drawer Transaction properties
        private List<CashDrawerTransactionModel> _allCashTransactions = new List<CashDrawerTransactionModel>();
        private ObservableCollection<CashDrawerTransactionModel> _cashDrawerTransactions;
        public ObservableCollection<CashDrawerTransactionModel> CashDrawerTransactions
        {
            get => _cashDrawerTransactions;
            set { _cashDrawerTransactions = value; OnPropertyChanged(); }
        }

        private DateTime? _transactionFromDate;
        public DateTime? TransactionFromDate
        {
            get => _transactionFromDate;
            set { _transactionFromDate = value; OnPropertyChanged(); }
        }

        private DateTime? _transactionToDate;
        public DateTime? TransactionToDate
        {
            get => _transactionToDate;
            set { _transactionToDate = value; OnPropertyChanged(); }
        }

        private bool _isLoadingCashTransactions;
        public bool IsLoadingCashTransactions
        {
            get => _isLoadingCashTransactions;
            set { _isLoadingCashTransactions = value; OnPropertyChanged(); }
        }

        private bool _hasNoCashTransactions;
        public bool HasNoCashTransactions
        {
            get => _hasNoCashTransactions;
            set { _hasNoCashTransactions = value; OnPropertyChanged(); }
        }

        // Pagination properties for cash transactions
        private int _cashTransactionPageSize = 8;
        private int _cashTransactionTotalPages = 1;
        private int _cashTransactionCurrentPage = 1;

        public int CashTransactionPageSize
        {
            get => _cashTransactionPageSize;
            set
            {
                if (value <= 0) return;
                if (_cashTransactionPageSize == value) return;
                _cashTransactionPageSize = value;
                OnPropertyChanged();
                RecalculateCashTransactionPaging(resetToFirstPage: true);
            }
        }

        public int CashTransactionCurrentPage
        {
            get => _cashTransactionCurrentPage;
            private set
            {
                if (_cashTransactionCurrentPage == value) return;
                _cashTransactionCurrentPage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CashTransactionPageInfoText));
                RefreshCashTransactionPaginationCommands();
            }
        }

        public int CashTransactionTotalPages
        {
            get => _cashTransactionTotalPages;
            private set
            {
                if (_cashTransactionTotalPages == value) return;
                _cashTransactionTotalPages = Math.Max(1, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(CashTransactionPageInfoText));
                RefreshCashTransactionPaginationCommands();
            }
        }

        public string CashTransactionPageInfoText => $"Page {CashTransactionCurrentPage} of {CashTransactionTotalPages}";

        public ICommand LoadCashTransactionsCommand { get; }
        public ICommand ClearCashTransactionDatesCommand { get; }
        public ICommand CashTransactionNextPageCommand { get; }
        public ICommand CashTransactionPrevPageCommand { get; }
        public ICommand CashTransactionFirstPageCommand { get; }
        public ICommand CashTransactionLastPageCommand { get; }

        private bool _isStartShiftEnabled = true;
        public bool IsStartShiftEnabled
        {
            get => _isStartShiftEnabled;
            set { _isStartShiftEnabled = value; OnPropertyChanged(); }
        }

        // Shift details properties
        private ShiftModel _currentShiftData;
        public ShiftModel CurrentShiftData 
        { 
            get => _currentShiftData; 
            set { _currentShiftData = value; OnPropertyChanged(); UpdateShiftPagination(resetPage:true); } 
        }
        private DateTime _selectedFromDate = DateTime.Today;
        public DateTime SelectedFromDate 
        { 
            get => _selectedFromDate; 
            set { _selectedFromDate = value; OnPropertyChanged(); } 
        }
        
        private DateTime? _selectedToDate = DateTime.Today;
        public DateTime? SelectedToDate 
        { 
            get => _selectedToDate; 
            set { _selectedToDate = value; OnPropertyChanged(); } 
        }
        public ICommand LoadShiftDataCommand { get; }
        public ICommand ShowShiftDataCommand { get; }
        public ICommand ClearToDateCommand { get; }
        // Pagination for shift details
        public ObservableCollection<ShiftDetailModel> PagedShiftDetails { get; } = new ObservableCollection<ShiftDetailModel>();
        private int _shiftPageSize = 7;
        public int ShiftPageSize { get => _shiftPageSize; set { _shiftPageSize = value; OnPropertyChanged(); UpdateShiftPagination(); } }
        private int _currentShiftPage = 1;
        public int CurrentShiftPage { get => _currentShiftPage; set { _currentShiftPage = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShiftPageInfoText)); RefreshShiftPaginationCommands(); } }
        private int _totalShiftPages = 1;
        public int TotalShiftPages { get => _totalShiftPages; private set { _totalShiftPages = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShiftPageInfoText)); RefreshShiftPaginationCommands(); } }
        private readonly RelayCommand _nextShiftPageCommand;
        private readonly RelayCommand _prevShiftPageCommand;
        private readonly RelayCommand _firstShiftPageCommand;
        private readonly RelayCommand _lastShiftPageCommand;
        public ICommand NextShiftPageCommand => _nextShiftPageCommand;
        public ICommand PrevShiftPageCommand => _prevShiftPageCommand;
        public ICommand FirstShiftPageCommand => _firstShiftPageCommand;
        public ICommand LastShiftPageCommand => _lastShiftPageCommand;
        public string ShiftPageInfoText => $"Page {CurrentShiftPage} of {TotalShiftPages}";
        private bool _isLoadingShiftData;
        public bool IsLoadingShiftData
        {
            get => _isLoadingShiftData;
            set { _isLoadingShiftData = value; OnPropertyChanged(); }
        }

        // Users list and dialog for viewing shift details
        public ObservableCollection<UserModel> OutletUsers { get; set; } = new ObservableCollection<UserModel>();
        public ObservableCollection<UserModel> FilteredOutletUsers { get; set; } = new ObservableCollection<UserModel>();
        private string _userSearchText;
        public string UserSearchText
        {
            get => _userSearchText;
            set { _userSearchText = value; OnPropertyChanged(); UpdateFilteredUsers(); }
        }
        private void ViewUser(UserModel user)
        {
            if (user == null) return;
            SelectedShiftUser = user;
            _ = OpenSelectedUserShiftDialogAsync();
        }
        private bool _isShiftDialogOpen;
        public bool IsShiftDialogOpen
        {
            get => _isShiftDialogOpen;
            set { _isShiftDialogOpen = value; OnPropertyChanged(); }
        }
        public UserModel SelectedShiftUser { get; set; }
        public ICommand LoadUsersCommand { get; }
        public ICommand ViewUserShiftDetailsCommand { get; }

        // Outlet Admin Shop Shift Info properties
        private ShopShiftInfoModel _currentShopShiftData;
        public ShopShiftInfoModel CurrentShopShiftData 
        { 
            get => _currentShopShiftData; 
            set { _currentShopShiftData = value; OnPropertyChanged(); } 
        }
        private bool _isLoadingShopShiftData;
        public bool IsLoadingShopShiftData
        {
            get => _isLoadingShopShiftData;
            set { _isLoadingShopShiftData = value; OnPropertyChanged(); }
        }
        public ICommand LoadShopShiftDataCommand { get; }
        public ICommand ViewUserShiftDetailsFromShopCommand { get; }
        public ICommand ShowShopShiftDataCommand { get; }
        public ICommand ClearShopToDateCommand { get; }
        private DateTime _selectedShopFromDate = DateTime.Today;
        public DateTime SelectedShopFromDate { get => _selectedShopFromDate; set { _selectedShopFromDate = value; OnPropertyChanged(); } }
        private DateTime? _selectedShopToDate = DateTime.Today;
        public DateTime? SelectedShopToDate { get => _selectedShopToDate; set { _selectedShopToDate = value; OnPropertyChanged(); } }
        
        
      // Snooze settings properties
//////////////////////////////////////


// Snooze options collection
public ObservableCollection<string> SnoozeOptions { get; set; }

// Selected snooze option
private string _selectedSnoozeOption;
public string SelectedSnoozeOption
{
    get => _selectedSnoozeOption;
    set { _selectedSnoozeOption = value; OnPropertyChanged(); }
}

// Command to confirm snooze selection
public ICommand ConfirmSnoozeCommand { get; }
public ICommand CancelSnoozeCommand { get; }

private async Task ConfirmSnoozeAsync()
{
    if (_currentPlatformForSnooze == null)
    {
        // Close the specific DialogHost by identifier to ensure it closes
        DialogHost.Close("RootDialog");
        return;
    }

    var platform = _currentPlatformForSnooze;
    // Persist the selection into the platform model for UI display
    platform.SelectedSnoozeOption = SelectedSnoozeOption;

    // Set platform updating state to prevent UI changes
    platform.IsUpdating = true;
    // Show the main loading spinner immediately after pressing OK
    IsLoadingPlatforms = true;

    // Close the dialog immediately on OK click
    DialogHost.Close("RootDialog");

    try
    {
        // Backend "snoozed" values – confirm with backend if names differ
        // Backend expects "0"/"1" string flags
        var autoAccepting = platform.AutoAccepting ? "1" : "0";
        var storeStatus = "0";
        string availableFrom = MapSnoozeLabelToApiValue(SelectedSnoozeOption);

        // If your PlatformModel has PlatformId (backend id), use it:
        var result = await _apiService.UpdateDeliveryPlatformOrderActionAsync(
            platform.Id,
            autoAccepting,
            storeStatus,
            availableFrom
        );

        var ok = result.IsSuccess;
        var error = result.ErrorMessage;

        if (!ok)
        {
            System.Windows.MessageBox.Show($"Failed to snooze: {error}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            // Only update UI after successful backend response
            platform.IsActive = false;
            // Refresh platforms to get backend state
            FetchPlatformsFromApi();
            // Notify View to update toggle state
            ToggleStateChanged?.Invoke(platform, false);
        }
    }
    finally
    {
        // Always reset platform updating state
        platform.IsUpdating = false;
        // Hide the main loading spinner when done
        IsLoadingPlatforms = false;
    }
}

private void CancelSnooze()
{
    // Revert toggle back to ON (since user cancelled snooze)
    if (_currentPlatformForSnooze != null)
    {
        // Ensure the platform remains active (toggle stays ON)
        _currentPlatformForSnooze.IsActive = true;
        _currentPlatformForSnooze.IsUpdating = false;
        
        // Force property change notification to update the UI
        _currentPlatformForSnooze.OnPropertyChanged("IsActive");
        _currentPlatformForSnooze.OnPropertyChanged("Status");
        _currentPlatformForSnooze.OnPropertyChanged("StatusColor");
    }
    DialogHost.Close("RootDialog");
}




private static string MapSnoozeLabelToApiValue(string label)
{
    
    
    var result = label switch
     {
        "1 Hour"    => "1_hour",
        "3 Hours"   => "3_hours", 
        "Next day"  => "1_day",
        "1 Week"    => "1_week",
        "forever"   => "forever",
        _ => null
    };
    
    return result;
}
   
public async Task ResumePlatformAsync(PlatformModel platform)
{
    if (platform == null) return;

    // Set platform updating state to prevent UI changes
    platform.IsUpdating = true;
    // Show the main loading spinner immediately for resume flow
    IsLoadingPlatforms = true;

    try
    {
        var autoAccepting = platform.AutoAccepting ? "1" : "0";
        var storeStatus = "1"; // ON per your spec
        string availableFrom = null; // no available_from when turning ON

        var result = await _apiService.UpdateDeliveryPlatformOrderActionAsync(
            platform.Id, autoAccepting, storeStatus, availableFrom);

        var ok = result.IsSuccess;
        var error = result.ErrorMessage;

        if (!ok)
        {
            System.Windows.MessageBox.Show($"Failed to resume: {error}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Only update UI after successful backend response
        platform.IsActive = true;
        // Refresh list on success
        FetchPlatformsFromApi();
        // Update the toggle state after successful API call
        UpdateToggleState(platform, true);
    }
    finally
    {
        // Always reset platform updating state
        platform.IsUpdating = false;
        // Hide the main loading spinner after resume flow completes
        IsLoadingPlatforms = false;
    }
}

private void UpdateToggleState(PlatformModel platform, bool newState)
{
    // This method will be called from the View to update the toggle state
    // after successful API calls
    // The View will need to implement this to find and update the toggle button
}

public void NotifyToggleStateChanged(PlatformModel platform, bool newState)
{
    // This method will be called from the View after successful API calls
    // to notify that the toggle state should be updated
    // We'll use an event or callback to communicate with the View
}

// Event to notify when toggle state should be updated
public event Action<PlatformModel, bool> ToggleStateChanged;

public ICommand OpenDialogCommand => new RelayCommand<PlatformModel>(ShowDialog);

private void ShowDialog(PlatformModel platform = null)
{
    _currentPlatformForSnooze = platform;

     // Set default selection if none selected
    if (string.IsNullOrEmpty(SelectedSnoozeOption) && SnoozeOptions?.Count > 0)
    {
        SelectedSnoozeOption = SnoozeOptions[0];
    }
    var dialog = new SnoozeDialog();
    DialogHost.Show(dialog, "RootDialog");
}


        ////////////////////////////////////////////////

        
        // Test method to fetch users with outlet code
        public async Task<List<UserModel>> TestFetchUsersWithOutletCode(string outletCode = null)
        {
            try
            {
                var apiService = new ApiService();
                var users = await apiService.GetUsersAsync(outletCode);
                return users;
            }
            catch (Exception ex)
            {
                // Check if it's a network connectivity issue
                var networkService = POS_UI.Services.NetworkConnectivityService.Instance;
                if (!networkService.IsConnected)
                {
                    // Don't show error message if there's no internet connection
                    // The internet connection dialog will handle this
                    return new List<UserModel>();
                }
                
                MessageBox.Show($"Failed to fetch users: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<UserModel>();
            }
        }
        
        public SettingsViewModel()
        {

            
            try
            {
                // Initialize snooze options
/////////////////////////////////////////
                _apiService = new ApiService();
SnoozeOptions = new ObservableCollection<string>
{
    "1 Hour",
    "3 Hours", 
    "Next Day",
    "1 Week",
    "Forever"
};
///////////////////////////////////////
// Initialize commands
ConfirmSnoozeCommand = new AsyncRelayCommand(ConfirmSnoozeAsync);
                //  OpenDialogCommand = new RelayCommand(ShowDialog);
            
                _tokenValidationService = new TokenValidationService();
                _settingsService = new SettingsService();
                Platforms = new ObservableCollection<PlatformModel>();
                _useLiveOrdersPage = GlobalDataService.Instance.UseLiveOrdersPage;
                _suppressFloorPlanLayoutPersist = true;
                _isFloorPlanLayoutEnabled = GlobalDataService.Instance.IsFloorPlanLayoutEnabled;
                _suppressFloorPlanLayoutPersist = false;
                // CardMachines is now managed by CardMachineService
                SelectedTab = "Platform Settings";
                CurrentPage = "Settings";
                SwitchTabCommand = new RelayCommand<string>(tab => 
                {
                    SelectedTab = tab;
                    if (tab == "Menu")
                    {
                        // Load menu configuration from cache when Menu tab is selected
                        _ = InitializeMenuTabAsync();
                    }
                    if (tab == "Configure Orders")
                    {
                        // Load General tab (OrdersPage) when entering Configure Orders tab
                        _ = LoadOrderConfigForGeneralAsync();
                    }
                    if (tab == "Floor Plan")
                    {
                        _ = InitializeFloorPlanTabAsync();
                    }
                    if (tab == "User")
                    {
                        // Load shift data when User tab is selected
                        _ = LoadShiftDataAsync();
                        if (IsAdmin)
                        {
                            _ = LoadUsersAsync();
                            // Load shop shift data for outlet admin
                            _ = LoadShopShiftDataAsync();
                        }
                    }
                    if (tab == "Cash Drawer")
                    {
                        _ = LoadActiveCashDrawerSessionAsync();

                        // Auto-load Cash Sessions for today when entering Cash Drawer tab
                        FromDate = DateTime.Today;
                        ToDate = DateTime.Today;
                        _ = LoadCashSessionsAsync();
                        // Auto-load Cash In/Out transactions for today when entering Cash Drawer tab
                        TransactionFromDate = DateTime.Today;
                        // Keep ToDate optional; comment next line if only from-date is desired
                        // TransactionToDate = DateTime.Today;
                        _ = LoadCashTransactionsAsync();
                    }
                });

                
                
                // Printer subtab command
                SwitchPrinterSubTabCommand = new RelayCommand<string>(subTab => 
                {
                    SelectedPrinterSubTab = subTab;
                });

                // Configure Orders subtab command
                SwitchConfigureOrdersSubTabCommand = new RelayCommand<string>(subTab =>
                {
                    SelectedConfigureOrdersSubTab = subTab;
                    if (string.Equals(subTab, "AutoCompleteOrders", StringComparison.OrdinalIgnoreCase))
                        _ = LoadAutoCompleteOrderConfigFromApiAsync();
                    else if (string.Equals(subTab, "OrdersPage", StringComparison.OrdinalIgnoreCase))
                        _ = LoadOrderConfigForGeneralAsync();
                    else if (string.Equals(subTab, "ItemDiscounts", StringComparison.OrdinalIgnoreCase))
                        LoadItemDiscountPresets();
                });
                SaveItemDiscountPresetsCommand = new RelayCommand(SaveItemDiscountPresets);
                OpenIdleLogoutTimerCommand = new RelayCommand(async () => await OpenIdleLogoutTimerAsync());

                // Commands
                CancelSnoozeCommand = new RelayCommand(CancelSnooze);

                // Get logged in user information from token
                var currentUser = _tokenValidationService.GetCurrentUser();
                string userId = null;
                if (currentUser != null)
                {
                    // Show claims for debugging
                    var claims = string.Join("\n", currentUser.Claims.Select(c => $"{c.Type}: {c.Value}"));
                    //System.Windows.MessageBox.Show("Claims:\n" + claims);
                    userId = currentUser.FindFirst("sub")?.Value;
                    //System.Windows.MessageBox.Show($"userId from token: {userId}");
                }
                else
                {
                    System.Windows.MessageBox.Show("No current user found in token.");
                }
                FetchLoggedInUserFromApi(userId);
                FetchPlatformsFromApi();
                ShiftTimer = "2h:48m:72s";
                OutletName = ((POS_UI.Services.GlobalDataService.Instance.ShopDetails?.Name) ?? OutletName ?? "Outlet") + " - POS";
                
                // Load Printer Groups from Shop Details
                LoadPrinterGroups();
                
                RequestPinChangeCommand = new RelayCommand<object>(RequestPinChange);
                SetNewPinCommand = new RelayCommand(SetNewPin);
                RefreshPlatformsCommand = new RelayCommand(FetchPlatformsFromApi);
                
                // Initialize shift-related commands
                LoadShiftDataCommand = new AsyncRelayCommand(async () => await LoadShiftDataAsync());
                ShowShiftDataCommand = new AsyncRelayCommand(async () => await ShowShiftDataAsync());
                ClearToDateCommand = new RelayCommand(() => SelectedToDate = null);
                LoadUsersCommand = new AsyncRelayCommand(async () => await LoadUsersAsync());
                ViewUserShiftDetailsCommand = new AsyncRelayCommand(async () => await OpenSelectedUserShiftDialogAsync());
                StartShiftCommand = new AsyncRelayCommand(async () => await StartShiftAsync());
                OpenEndShiftDialogCommand = new AsyncRelayCommand(async () => await OpenEndShiftDialogAsync());
                OpenCashInDialogCommand = new AsyncRelayCommand(async () => await OpenCashInDialogAsync());
                OpenCashOutDialogCommand = new AsyncRelayCommand(async () => await OpenCashOutDialogAsync());
                //PrintXReportCommand = new AsyncRelayCommand(async () => await PrintXReportAsync());
                
                // Initialize cash session commands and collections
                LoadCashSessionsCommand = new AsyncRelayCommand(async () => await LoadCashSessionsAsync());
                ClearCashSessionDatesCommand = new RelayCommand(ClearCashSessionDates);
                CashDrawerSessions = new ObservableCollection<CashDrawerSessionModel>();
                HasNoCashSessions = true; // Initially no sessions loaded
                
                // Initialize cash session pagination commands
                CashSessionNextPageCommand = new RelayCommand(CashSessionNextPage, CanGoCashSessionNextPage);
                CashSessionPrevPageCommand = new RelayCommand(CashSessionPrevPage, CanGoCashSessionPrevPage);
                CashSessionFirstPageCommand = new RelayCommand(CashSessionFirstPage, CanGoCashSessionPrevPage);
                CashSessionLastPageCommand = new RelayCommand(CashSessionLastPage, CanGoCashSessionNextPage);
                
                // Initialize cash transaction commands and collections
                LoadCashTransactionsCommand = new AsyncRelayCommand(async () => await LoadCashTransactionsAsync());
                ClearCashTransactionDatesCommand = new RelayCommand(ClearCashTransactionDates);
                CashDrawerTransactions = new ObservableCollection<CashDrawerTransactionModel>();
                HasNoCashTransactions = true; // Initially no transactions loaded
                
                // Initialize cash transaction pagination commands
                CashTransactionNextPageCommand = new RelayCommand(CashTransactionNextPage, CanGoCashTransactionNextPage);
                CashTransactionPrevPageCommand = new RelayCommand(CashTransactionPrevPage, CanGoCashTransactionPrevPage);
                CashTransactionFirstPageCommand = new RelayCommand(CashTransactionFirstPage, CanGoCashTransactionPrevPage);
                CashTransactionLastPageCommand = new RelayCommand(CashTransactionLastPage, CanGoCashTransactionNextPage);
                
                // Initialize date range to today
                var today = DateTime.Today;
                FromDate = today;
                ToDate = today;
                TransactionFromDate = today;
                TransactionToDate = today;
                _nextShiftPageCommand = new RelayCommand(NextShiftPage, CanGoNextShiftPage);
                _prevShiftPageCommand = new RelayCommand(PrevShiftPage, CanGoPrevShiftPage);
                _firstShiftPageCommand = new RelayCommand(FirstShiftPage, CanGoPrevShiftPage);
                _lastShiftPageCommand = new RelayCommand(LastShiftPage, CanGoNextShiftPage);
                
                // Initialize outlet admin shop shift commands
                LoadShopShiftDataCommand = new AsyncRelayCommand(async () => await LoadShopShiftDataAsync());
                ViewUserShiftDetailsFromShopCommand = new AsyncRelayCommand<object>(async (parameter) => await OpenUserShiftDetailsFromShopAsync(parameter));
                ShowShopShiftDataCommand = new AsyncRelayCommand(async () => await ShowShopShiftDataAsync());
                ClearShopToDateCommand = new RelayCommand(() => SelectedShopToDate = null);
                
                // Initialize admin tab commands
                SwitchAdminTabCommand = new RelayCommand<string>(tab => 
                {
                    SelectedAdminTab = tab;
                    if (tab == "ShiftDetails" && IsAdmin)
                    {
                        // Load shop shift data when ShiftDetails tab is selected
                        _ = LoadShopShiftDataAsync();
                    }
                    else if (tab == "CashSession" && IsAdmin)
                    {
                        // Load cash sessions when Cash Session tab is selected
                        _ = LoadCashSessionsAsync();
                    }
                });

                // User details
                ViewUserDetailsCommand = new AsyncRelayCommand<object>(async param => await OpenUserDetailsDialogAsync(param as UserModel));
                
                // Menu configuration commands
                LoadMenuConfigCommand = new AsyncRelayCommand(async () => await LoadMenuConfigAsync());
                AddMenuTabCommand = new AsyncRelayCommand(async () => await AddMenuTabAsync());
                EditMenuTabCommand = new AsyncRelayCommand<MenuTabModel>(async (tab) => await EditMenuTabAsync(tab));
                DeleteMenuTabCommand = new AsyncRelayCommand<MenuTabModel>(async (tab) => await DeleteMenuTabAsync(tab));
                MoveTabUpCommand = new AsyncRelayCommand<MenuTabModel>(async (tab) => await MoveTabUpAsync(tab));
                MoveTabDownCommand = new AsyncRelayCommand<MenuTabModel>(async (tab) => await MoveTabDownAsync(tab));
                SaveMenuConfigCommand = new AsyncRelayCommand(async () => await SaveMenuConfigAsync(showSuccessMessage: true));

                // Floor plan commands
                LoadFloorPlanDataCommand = new AsyncRelayCommand(async () => await InitializeFloorPlanTabAsync(forceReloadTables: true));
                AddFloorPlanCommand = new RelayCommand(AddFloorPlan);
                EditFloorPlanCommand = new RelayCommand<FloorPlanModel>(StartEditFloorPlan);
                DeleteFloorPlanCommand = new RelayCommand<FloorPlanModel>(DeleteFloorPlan);
                AddTableToFloorPlanCommand = new AsyncRelayCommand(OpenSelectTableForFloorPlanAsync);
                AddCustomItemToFloorPlanCommand = new RelayCommand(() => _ = OpenSelectCustomItemForFloorPlanAsync());
                ToggleFloorPlanColorPickerCommand = new RelayCommand(() =>
                {
                    if (!HasSelectedFloorPlanTable) return;
                    IsFloorPlanColorPickerOpen = !IsFloorPlanColorPickerOpen;
                });
                RemoveTableFromFloorPlanCommand = new RelayCommand(RemoveSelectedPlacedTableFromEditingFloorPlan);
                ClearFloorPlanCommand = new RelayCommand(ClearEditingFloorPlanTables);
                SaveFloorPlanCommand = new AsyncRelayCommand(SaveEditingFloorPlanAsync);
                CancelFloorPlanEditCommand = new RelayCommand(CancelFloorPlanEdit);
                SetSelectedPlacedFloorPlanTableCommand = new RelayCommand<FloorPlanTablePlacementModel>(SelectPlacedFloorPlanTable);
                IncreaseFloorPlanTableWidthCommand = new RelayCommand(() => AdjustSelectedFloorPlanTableSize(widthDelta: 10, heightDelta: 0));
                DecreaseFloorPlanTableWidthCommand = new RelayCommand(() => AdjustSelectedFloorPlanTableSize(widthDelta: -10, heightDelta: 0));
                IncreaseFloorPlanTableHeightCommand = new RelayCommand(() => AdjustSelectedFloorPlanTableSize(widthDelta: 0, heightDelta: 10));
                DecreaseFloorPlanTableHeightCommand = new RelayCommand(() => AdjustSelectedFloorPlanTableSize(widthDelta: 0, heightDelta: -10));
            } catch (Exception ex) { System.Windows.MessageBox.Show("Exception: " + ex.Message); }
        }

        public async Task LoadActiveCashDrawerSessionAsync()
        {
            try
            {
                var session = await _apiService.GetActiveCashDrawerSessionAsync();
                ActiveCashDrawerSession = session;
                IsStartShiftEnabled = session == null; // enable start if no active
            }
            catch (Exception ex)
            {
                // Keep Start Shift enabled on error so user can attempt
                IsStartShiftEnabled = true;
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to load session", ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
        }

        public async Task LoadCashSessionsAsync()
        {
            try
            {
                if (FromDate == null)
                {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("From Date is required", "From Date is required. Please select a date.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                    return;
                }
                IsLoadingCashSessions = true;
                var sessions = await _apiService.GetCashDrawerSessionsAsync(FromDate, ToDate);
                
                _allCashSessions = sessions.ToList();
                HasNoCashSessions = _allCashSessions.Count == 0;
                RecalculateCashSessionPaging(resetToFirstPage: true);
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to load cash sessions", ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                HasNoCashSessions = true; // Show no data message on error
            }
            finally
            {
                IsLoadingCashSessions = false;
            }
        }

        private async Task OpenCashInDialogAsync()
        {
            try
            {
                // Trigger cash drawer on Cash In
                TriggerCashDrawer();
                var dialog = new POS_UI.View.CashInOutDialog(POS_UI.View.CashInOutDialog.CashFlowType.CashIn);
                var result = await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog");
                if (result is decimal amount)
                {
                    // TODO: optionally call an API for cash in here
                    await LoadActiveCashDrawerSessionAsync();
                }
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to open dialog", ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
        }

        private async Task OpenCashOutDialogAsync()
        {
            try
            {
                // Trigger cash drawer on Cash Out
                TriggerCashDrawer();
                
                // Convert ActiveCashDrawerSession to CashDrawerSessionModel for the dialog
                CashDrawerSessionModel sessionForDialog = null;
                if (ActiveCashDrawerSession != null)
                {
                    sessionForDialog = new CashDrawerSessionModel
                    {
                        Id = ActiveCashDrawerSession.Id,
                        CashDrawerId = ActiveCashDrawerSession.CashDrawerId,
                        SessionStartedUserId = ActiveCashDrawerSession.SessionStartedUserId,
                        SessionStartedUser = ActiveCashDrawerSession.SessionStartedUser,
                        OpenedAt = ActiveCashDrawerSession.OpenedAt,
                        OpeningBalance = ActiveCashDrawerSession.OpeningBalance,
                        ClosingBalanceExpected = ActiveCashDrawerSession.ClosingBalanceExpected,
                        TotalInAmount = ActiveCashDrawerSession.TotalInAmount,
                        TotalOutAmount = ActiveCashDrawerSession.TotalOutAmount,
                        TotalSalesAmount = ActiveCashDrawerSession.TotalSalesAmount,
                        TotalRefundAmount = ActiveCashDrawerSession.TotalRefundAmount,
                        TotalCashSaleCashRefundAmount = ActiveCashDrawerSession.TotalCashSaleCashRefundAmount,
                        TotalCardSaleCashRefundAmount = ActiveCashDrawerSession.TotalCardSaleCashRefundAmount,
                        TotalOtherCashSaleCashRefundAmount = ActiveCashDrawerSession.TotalOtherCashSaleCashRefundAmount,
                        OtherSalesAmount = ActiveCashDrawerSession.OtherSalesAmount,
                        Status = ActiveCashDrawerSession.Status,
                        CreatedAt = ActiveCashDrawerSession.CreatedAt,
                        UpdatedAt = ActiveCashDrawerSession.UpdatedAt
                    };
                }
                
                var dialog = new POS_UI.View.CashInOutDialog(POS_UI.View.CashInOutDialog.CashFlowType.CashOut, sessionForDialog);
                var result = await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog");
                if (result is decimal amount)
                {
                    // Trigger cash drawer on Cash Out
                    TriggerCashDrawer();
                    await LoadActiveCashDrawerSessionAsync();
                }
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to open dialog", ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
        }

       /* private async Task PrintXReportAsync()
        {
            try
            {
                if (ActiveCashDrawerSession == null)
                {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("No Active Session", "There is no active cash drawer session to print.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    await MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                    return;
                }

                // Use active session's opened date and current UTC time for both cash drawer summary and Z-Report stats
                var fromDate = ActiveCashDrawerSession.OpenedAt;
                var toDate = DateTime.UtcNow; // Current UTC time

                // Fetch cash drawer session for cash drawer summary (only ONE session)
                // Use active session's opened date and current UTC time
                List<CashDrawerSessionModel> cashSessions = null;
                try
                {
                    var cashSessionsFromApi = await _apiService.GetCashDrawerSessionsAsync(fromDate, toDate);
                    // Only take the first session (the active/current session) for cash drawer summary
                    if (cashSessionsFromApi != null && cashSessionsFromApi.Count > 0)
                {
                        // Get the session that matches the active session ID, or the first one
                        var activeSession = cashSessionsFromApi.FirstOrDefault(s => s.Id == ActiveCashDrawerSession.Id) 
                                          ?? cashSessionsFromApi.FirstOrDefault();
                        cashSessions = activeSession != null ? new List<CashDrawerSessionModel> { activeSession } : new List<CashDrawerSessionModel>();
                    }
                    else
                    {
                        cashSessions = new List<CashDrawerSessionModel>();
                    }
                }
                catch (Exception sessionsEx)
                {
                    // Log but don't block printing - we can still print the report without sessions
                    System.Diagnostics.Debug.WriteLine($"[PrintXReport] Failed to fetch cash drawer sessions: {sessionsEx.Message}");
                    cashSessions = new List<CashDrawerSessionModel>();
                }

                // Convert ActiveCashDrawerSession to CashDrawerSessionModel (for backward compatibility)
                var session = new CashDrawerSessionModel
                {
                    Id = ActiveCashDrawerSession.Id,
                    CashDrawerId = ActiveCashDrawerSession.CashDrawerId,
                    SessionStartedUserId = ActiveCashDrawerSession.SessionStartedUserId,
                    SessionStartedUser = ActiveCashDrawerSession.SessionStartedUser,
                    OpenedAt = ActiveCashDrawerSession.OpenedAt,
                    OpeningBalance = ActiveCashDrawerSession.OpeningBalance,
                    ClosingBalanceExpected = ActiveCashDrawerSession.ClosingBalanceExpected,
                    TotalInAmount = ActiveCashDrawerSession.TotalInAmount,
                    TotalOutAmount = ActiveCashDrawerSession.TotalOutAmount,
                    TotalSalesAmount = ActiveCashDrawerSession.TotalSalesAmount,
                    TotalRefundAmount = ActiveCashDrawerSession.TotalRefundAmount,
                    OtherSalesAmount = ActiveCashDrawerSession.OtherSalesAmount,
                    Status = ActiveCashDrawerSession.Status,
                    CreatedAt = ActiveCashDrawerSession.CreatedAt,
                    UpdatedAt = ActiveCashDrawerSession.UpdatedAt,
                    // Active session doesn't have these, so set to null
                    ClosedAt = null,
                    SessionEndedUser = null,
                    SessionEndedUserId = null,
                    ClosingBalanceCounted = null,
                    Difference = 0
                };

                // Fetch Z-Report stats from API - required for printing
                // Use same dates as cash drawer summary (active session's opened date and current UTC time)
                POS_UI.Models.ZReportStatsModel zReportStats = null;
                try
                {
                    zReportStats = await _apiService.GetZReportStatsAsync(fromDate, toDate);
                    
                    // Ensure CalculateOrderCounts is called to populate order count properties
                    if (zReportStats != null)
                    {
                        zReportStats.CalculateOrderCounts();
                    }
                }
                catch (Exception apiEx)
                {
                    var errorVm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("API Error", $"Failed to fetch Z-Report stats: {apiEx.Message}\nCannot print report without data.");
                    var errorDlg = new POS_UI.View.StatusDialog { DataContext = errorVm };
                    await MaterialDesignThemes.Wpf.DialogHost.Show(errorDlg, "RootDialog");
                    return; // Don't proceed if we can't get the data
                }

                // Ensure we have zReportStats before proceeding
                if (zReportStats == null)
                {
                    var errorVm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("No Data", "Unable to fetch Z-Report stats. Cannot print report.");
                    var errorDlg = new POS_UI.View.StatusDialog { DataContext = errorVm };
                    await MaterialDesignThemes.Wpf.DialogHost.Show(errorDlg, "RootDialog");
                    return;
                }

                // Print the report with ZReportStats and cash drawer sessions
                // Tax data is now included in zReportStats.TaxSummary from GetZReportStatsAsync
                await ReceiptPrintingService.Instance.PrintReportReceiptAsync(session, zReportStats, cashSessions, null);
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to print X-Report", ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                await MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
        }*/

        private async void TriggerCashDrawer()
        {
            try
            {
                // Send pulse to all active printers; if none active, send to all discovered printers
                var printersService = PrintersService.Instance;
                var targetPrinters = printersService.Printers.Where(p => p.IsActive).Select(p => p.DeviceName).ToList();
                if (targetPrinters.Count == 0)
                {
                    targetPrinters = printersService.Printers.Select(p => p.DeviceName).ToList();
                }
                if (targetPrinters.Count == 0)
                {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Active Cash Drawer Not Found", "No active cash drawer is configured to perform this action.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                    return;
                }

                // ESC/POS pulse command to open cash drawer
                byte[] openDrawerCommand = new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA };
                bool anySuccess = false;
                foreach (var pn in targetPrinters.Distinct())
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(pn)) continue;
                        var ok = RawPrinterHelper.SendBytesToPrinter(pn, openDrawerCommand);
                        if (ok) anySuccess = true;
                    }
                    catch { }
                }
                if (!anySuccess)
                {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed", "Failed to trigger cash drawer.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                    return;
                }

                // Log user activity after successful drawer opening
                try
                {
                    var currentUser = new LocalStorageService().GetCurrentUser();
                    var currentUserId = currentUser?.Id;
                    await _apiService.LogUserActivityAsync("open", "cash_drawer", 1, currentUserId, "Opened cash drawer");
                }
                catch
                {
                    // Silently fail logging - don't block drawer opening if logging fails
                }
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Opening Cash Drawer", $"Error opening cash drawer: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
        }
        private async Task StartShiftAsync()
        {
            try
            {
                // Show opening balance dialog - it will handle the API call internally
                var openingBalanceDialog = new OpeningBalanceDialog { DialogHostIdentifier = "RootDialog" };
                await MaterialDesignThemes.Wpf.DialogHost.Show(openingBalanceDialog, "RootDialog");
                
                // Refresh the active session after dialog closes (success or cancel)
                await LoadActiveCashDrawerSessionAsync();
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to start shift", ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
        }

        private async Task OpenEndShiftDialogAsync()
        {
            try
            {
                var incompleteCount = ActiveCashDrawerSession?.IncompleteOrders?.Count ?? 0;
                if (incompleteCount > 0)
                {
                    var orders = ActiveCashDrawerSession.IncompleteOrders?.Orders;
                    var blockVm = POS_UI.ViewModels.StatusDialogViewModel.CreateCannotEndShiftIncompleteOrders(incompleteCount, orders);
                    var blockDlg = new POS_UI.View.StatusDialog { DataContext = blockVm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(blockDlg, "RootDialog");
                    return;
                }

                // Open cash drawer immediately when End Shift button is clicked
               // TriggerCashDrawer();
                
                //await Task.Delay(1000);
                // Store the session ID, start date/time, and active session data before closing
                int? sessionId = ActiveCashDrawerSession?.Id;
                DateTime? sessionStartDate = ActiveCashDrawerSession?.OpenedAt.AddMinutes(-1);
                //var activeSessionData = ActiveCashDrawerSession; // Store the active session data
                
                var dialog = new POS_UI.View.EndShiftDialog();
                var result = await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog");
                if (result is decimal amount)
                {
                    EndShiftCashAmount = amount;
                    OnPropertyChanged(nameof(EndShiftCashAmount));
                    try
                    {
                        CashDrawerSessionModel closedSession = null;
                        if (sessionStartDate.HasValue && sessionId.HasValue)
                        {
                            closedSession = await _apiService.GetCashDrawerSessionByIdAsync(sessionId.Value);
                            if (closedSession == null)
                            {
                                var errorMessage = $"Failed to retrieve closed session by ID.\nSession ID: {sessionId.Value}";
                                MessageBox.Show(errorMessage, "Z-Report", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }

                        // Show success dialog and wait for user to close it
                        var currency = GlobalDataService.Instance.ShopDetails?.Currency ?? "";
                        var successVm = POS_UI.ViewModels.StatusDialogViewModel.CreateSuccess("Shift Ended", $"Cash amount recorded: {currency}{amount:0.00}");
                        var successDlg = new POS_UI.View.StatusDialog { DataContext = successVm };
                        await MaterialDesignThemes.Wpf.DialogHost.Show(successDlg, "RootDialog");

                        // After success dialog closes, open Z-Report dialog with session content (user can print from there)
                        if (closedSession != null)
                        {
                            var zReportDialog = new POS_UI.View.ZReportDialog(closedSession);
                            await MaterialDesignThemes.Wpf.DialogHost.Show(zReportDialog, "RootDialog");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[OpenEndShiftDialog] Error: {ex.Message}");
                    }
                    finally
                    {
                        await LoadActiveCashDrawerSessionAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to open dialog", ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
        }

        private async Task PrintXReportForSessionAsync(CashDrawerSessionModel session, DateTime fromDate, DateTime toDate)
        {
            try
            {
                // Use the provided date range for both cash drawer summary and Z-Report stats
                var cashDrawerFromDate = fromDate; // Session opened date
                var cashDrawerToDate = toDate; // Session closed date (or provided end date)
                
                // OPTIMIZATION: Run both API calls in PARALLEL to reduce wait time
                var cashSessionsTask = _apiService.GetCashDrawerSessionsAsync(cashDrawerFromDate, cashDrawerToDate);
                var zReportStatsTask = _apiService.GetZReportStatsAsync(cashDrawerFromDate.ToUniversalTime(), cashDrawerToDate.AddDays(1).ToUniversalTime());
                
                // Wait for both tasks to complete simultaneously
                await Task.WhenAll(cashSessionsTask, zReportStatsTask);
                
                // Process cash drawer sessions result
                List<CashDrawerSessionModel> cashSessions = null;
                try
                {
                    var cashSessionsFromApi = await cashSessionsTask;
                    // Only take the first session (the active/current session) for cash drawer summary
                    if (cashSessionsFromApi != null && cashSessionsFromApi.Count > 0)
                    {
                        // Get the session that matches the closed session ID, or the first one
                        var targetSession = cashSessionsFromApi.FirstOrDefault(s => s.Id == session.Id) 
                                          ?? cashSessionsFromApi.FirstOrDefault();
                        cashSessions = targetSession != null ? new List<CashDrawerSessionModel> { targetSession } : new List<CashDrawerSessionModel> { session };
                    }
                    else
                    {
                        cashSessions = new List<CashDrawerSessionModel> { session };
                    }
                }
                catch (Exception sessionsEx)
                {
                    // Log but don't block printing - use the session we have
                    System.Diagnostics.Debug.WriteLine($"[PrintXReportForSession] Failed to fetch cash drawer sessions: {sessionsEx.Message}");
                    MessageBox.Show($"[PrintXReportForSession] Failed to fetch cash drawer sessions: {sessionsEx.Message}");
                    cashSessions = new List<CashDrawerSessionModel> { session };
                }

                // Process Z-Report stats result
                // Note: CalculateOrderCounts() is already called inside GetZReportStatsAsync
                POS_UI.Models.ZReportStatsModel zReportStats = null;
                try
                {
                    zReportStats = await zReportStatsTask;
                }
                catch (Exception apiEx)
                {
                    MessageBox.Show($"[PrintXReportForSession] Failed to fetch Z-Report stats: {apiEx.Message}");
                    return;
                }

                if (zReportStats == null)
                {
                    MessageBox.Show("[PrintXReportForSession] Unable to fetch Z-Report stats. Cannot print report.");
                    return;
                }

                // Print the report - tax data is included in zReportStats.TaxSummary
                await ReceiptPrintingService.Instance.PrintReportReceiptAsync(session, zReportStats, cashSessions, null);
            }
            catch (Exception ex)
            {
                // Log error but don't show dialog - this is automatic printing
                MessageBox.Show($"[PrintXReportForSession] Failed to print X-Report: {ex.Message}");
            }
        }

        private async void FetchLoggedInUserFromApi(string userId)
        {
            //System.Windows.MessageBox.Show("FetchLoggedInUserFromApi called");
            if (string.IsNullOrEmpty(userId))
                {
                LoggedInUser = new UserModel { FirstName = "Unknown", LastName = "User", Role = "Cashier" };
                SecuritySettings = new ObservableCollection<SecuritySettingModel>();
                OnPropertyChanged(nameof(LoggedInUser));
                OnPropertyChanged(nameof(IsAdmin));
                //OnPropertyChanged(nameof(SecuritySettings));
                return;
            }
            try
            {
                var apiService = new ApiService();
                var user = await apiService.GetUserByIdAsync(userId);
                //System.Windows.MessageBox.Show(JsonConvert.SerializeObject(user));
                if (user != null)
                {
                    //System.Windows.MessageBox.Show($"API returned user: {user.FirstName} {user.LastName}");
                    LoggedInUser = user;
                }
                else
                {
                    //System.Windows.MessageBox.Show("API returned null user.");
                    LoggedInUser = new UserModel { FirstName = "gggg", LastName = "", Role = "Cashier" };
                }
                //System.Windows.MessageBox.Show($"Role: {LoggedInUser.Role}");

            if (IsAdmin)
            {
                SecuritySettings = new ObservableCollection<SecuritySettingModel>
                {
                    new SecuritySettingModel { UserName = "Cash 1", Status = "Checked IN", LastActive = DateTime.Now, IsActive = true, PinChangeRequested = true },
                    new SecuritySettingModel { UserName = "Cash 2", Status = "Checked OUT", LastActive = DateTime.Now.AddMinutes(-30), IsActive = false, PinChangeRequested = false }
                };
                // Auto-load users list for outlet admins so Users tab shows immediately
                _ = LoadUsersAsync();
            }
            else
            {
                SecuritySettings = new ObservableCollection<SecuritySettingModel>();
            }
                OnPropertyChanged(nameof(LoggedInUser));
                OnPropertyChanged(nameof(IsAdmin));
                OnPropertyChanged(nameof(SecuritySettings));
            }
            catch(Exception ex)
            {
                //System.Windows.MessageBox.Show("Exception in FetchLoggedInUserFromApi: " + ex.Message);
                LoggedInUser = new UserModel { FirstName = "ffffff", LastName = "", Role = "Cashier" };
                SecuritySettings = new ObservableCollection<SecuritySettingModel>();
                OnPropertyChanged(nameof(LoggedInUser));
                OnPropertyChanged(nameof(IsAdmin));
                OnPropertyChanged(nameof(SecuritySettings));
            }
        }

        private async void FetchPlatformsFromApi()
        {
            IsLoadingPlatforms = true;
            try
            {
                var apiService = new ApiService();
                var platforms = await apiService.GetPlatformsAsync();
                
                Platforms.Clear();
                foreach (var platform in platforms)
                {
                    // Initialize display state to match backend state
                    platform.DisplayIsActive = platform.IsActive;
                    Platforms.Add(platform);
                }
                
                OnPropertyChanged(nameof(Platforms));
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
                
                //System.Windows.MessageBox.Show($"Failed to fetch platforms: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                // Add some default platforms in case of error
                Platforms.Clear();
                Platforms.Add(new PlatformModel { PlatformName = "Uber Eats", Branch = "Subway Galle", IsActive = true });
                Platforms.Add(new PlatformModel { PlatformName = "Uber Eats", Branch = "Subway Matara", IsActive = false });
                OnPropertyChanged(nameof(Platforms));
            }
            finally
            {
                IsLoadingPlatforms = false;
            }
        }

        private void LoadPrinterGroups()
        {
            try
            {
                var shopDetails = POS_UI.Services.GlobalDataService.Instance.ShopDetails;
                if (shopDetails?.PrinterGroups != null && shopDetails.PrinterGroups.Count > 0)
                {
                    PrinterGroups = new ObservableCollection<PrinterGroupModel>(shopDetails.PrinterGroups);
                }
                else
                {
                    PrinterGroups = new ObservableCollection<PrinterGroupModel>();
                }
                OnPropertyChanged(nameof(PrinterGroups));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load printer groups: {ex.Message}");
                PrinterGroups = new ObservableCollection<PrinterGroupModel>();
            }
        }

        private void RequestPinChange(object parameter)
        {
            try
            {
                if (IsAdmin)
                {
                    // Admin users: Reset other users' PINs
                    // parameter is expected to be the selected user from the Users list (CommandParameter="{Binding}")
                    if (parameter is UserModel selected)
                    {
                        SelectedUserForPinReset = selected;
                        OnPropertyChanged(nameof(SelectedUserForPinReset));
                        OnPropertyChanged(nameof(IsPinTargetOutletAdmin));
                    }
                    else
                    {
                        MessageBox.Show("Please select a user to reset their PIN.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    // Non-admin users: Change their own PIN
                    // Use the logged-in user as the target
                    SelectedUserForPinReset = LoggedInUser;
                    OnPropertyChanged(nameof(SelectedUserForPinReset));
                    OnPropertyChanged(nameof(IsPinTargetOutletAdmin));
                }
                
                ClearPinDigits(); // Clear any previous PIN input
                IsPinDialogOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening PIN dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void SetNewPin()
        {
            if (string.IsNullOrEmpty(NewPin) || NewPin != ConfirmPin)
            {
                //MessageBox.Show("PINs do not match or are empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Warning","PINs do not match or are empty.");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                return;
            }

            if (SelectedUserForPinReset == null)
            {
                MessageBox.Show("No user selected for PIN reset.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Call the API to update the user's PIN
                bool success = await _apiService.UpdateUserPinAsync(SelectedUserForPinReset.ApiId, NewPin);
                
                if (success)
                {
                    IsPinDialogOpen = false;
                    // Determine message based on the role of the user whose PIN was changed
                    if(IsAdmin)
                   {
                        var targetRole = SelectedUserForPinReset?.Role?.Trim() ?? string.Empty;
                        bool isCashier = targetRole.Equals("Cashier", StringComparison.OrdinalIgnoreCase) || targetRole.Equals("Outlet Cashier", StringComparison.OrdinalIgnoreCase);
                        string message = isCashier
                            ? "The cashier's PIN has been reset successfully. Please provide the new PIN to the cashier."
                            : "PIN reset successfully.";
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateSuccess("Success", message);
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                   }
                   else
                   {
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateSuccess("Success", "PIN reset successfully.");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                   }

                    // Clear the PIN fields
                    ClearPinDigits();
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Failed to update PIN: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to update PIN", $"Failed to update PIN: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
        }

        private async Task LoadShiftDataAsync()
        {
            try
            {
                IsLoadingShiftData = true;
                
                // Get current user ID from token
                var currentUser = _tokenValidationService.GetCurrentUser();
                if (currentUser == null)
                {
                    // Create empty shift data instead of showing error
                    CurrentShiftData = new ShiftModel
                    {
                        UserId = 0,
                        FromDate = DateTime.Today,
                        ToDate = DateTime.Today.AddDays(1).AddSeconds(-1),
                        OrderCount = 0,
                        TotalOrderAmount = 0
                    };
                    OnPropertyChanged(nameof(CurrentShiftData));
                    return;
                }

                var userId = currentUser.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    // Create empty shift data instead of showing error
                    CurrentShiftData = new ShiftModel
                    {
                        UserId = 0,
                        FromDate = DateTime.Today,
                        ToDate = DateTime.Today.AddDays(1).AddSeconds(-1),
                        OrderCount = 0,
                        TotalOrderAmount = 0
                    };
                    OnPropertyChanged(nameof(CurrentShiftData));
                    return;
                }

                // Load shift data for current date (no 'to' by default)
                CurrentShiftData = await _apiService.GetUserShiftsAsync(userIdInt, DateTime.Today, null);
                OnPropertyChanged(nameof(CurrentShiftData));
            }
            catch (Exception ex)
            {
                // Create empty shift data on error instead of showing error message
                CurrentShiftData = new ShiftModel
                {
                    UserId = 0,
                    FromDate = DateTime.Today,
                    ToDate = DateTime.Today.AddDays(1).AddSeconds(-1),
                    OrderCount = 0,
                    TotalOrderAmount = 0
                };
                OnPropertyChanged(nameof(CurrentShiftData));
                Console.WriteLine($"Failed to load shift data: {ex.Message}");
            }
            finally
            {
                IsLoadingShiftData = false;
            }
        }

        private async Task ShowShiftDataAsync()
        {
            try
            {
                IsLoadingShiftData = true;
                
                // Prefer the user selected for the dialog; fallback to current token user
                int targetUserApiId = 0;
                if (SelectedShiftUser != null && SelectedShiftUser.ApiId > 0)
                {
                    targetUserApiId = SelectedShiftUser.ApiId;
                }
                else
                {
                    var currentUser = _tokenValidationService.GetCurrentUser();
                    if (currentUser != null)
                    {
                        var idStr = currentUser.FindFirst("sub")?.Value;
                        int.TryParse(idStr, out targetUserApiId);
                    }
                }

                if (targetUserApiId <= 0)
                {
                    // Create empty shift data instead of showing error
                    CurrentShiftData = new ShiftModel
                    {
                        UserId = 0,
                        FromDate = SelectedFromDate,
                        ToDate = (SelectedToDate ?? SelectedFromDate.AddDays(1).AddSeconds(-1)),
                        OrderCount = 0,
                        TotalOrderAmount = 0
                    };
                    OnPropertyChanged(nameof(CurrentShiftData));
                    return;
                }

                // Load shift data for selected date range (omit 'to' when not chosen)
                CurrentShiftData = await _apiService.GetUserShiftsAsync(targetUserApiId, SelectedFromDate, SelectedToDate);
                OnPropertyChanged(nameof(CurrentShiftData));
                UpdateShiftPagination(resetPage:true);
            }
            catch (Exception ex)
            {
                // Create empty shift data on error instead of showing error message
                CurrentShiftData = new ShiftModel
                {
                    UserId = 0,
                    FromDate = SelectedFromDate,
                    ToDate = (SelectedToDate ?? SelectedFromDate.AddDays(1).AddSeconds(-1)),
                    OrderCount = 0,
                    TotalOrderAmount = 0
                };
                OnPropertyChanged(nameof(CurrentShiftData));
                Console.WriteLine($"Failed to load shift data: {ex.Message}");
            }
            finally
            {
                IsLoadingShiftData = false;
            }
        }

        private void UpdateShiftPagination(bool resetPage = false)
        {
            var list = CurrentShiftData?.ShiftDetails ?? new List<ShiftDetailModel>();
            TotalShiftPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)ShiftPageSize));
            if (resetPage) CurrentShiftPage = 1;
            else if (CurrentShiftPage > TotalShiftPages)
            {
                CurrentShiftPage = TotalShiftPages;
            }
            UpdatePagedShiftDetails();
        }

        private void UpdatePagedShiftDetails()
        {
            PagedShiftDetails.Clear();
            var list = CurrentShiftData?.ShiftDetails ?? new List<ShiftDetailModel>();
            if (list.Count == 0) return;

            int skip = (CurrentShiftPage - 1) * ShiftPageSize;
            foreach (var item in list.Skip(skip).Take(ShiftPageSize))
            {
                PagedShiftDetails.Add(item);
            }
        }

        private void RefreshShiftPaginationCommands()
        {
            _nextShiftPageCommand.RaiseCanExecuteChanged();
            _prevShiftPageCommand.RaiseCanExecuteChanged();
            _firstShiftPageCommand.RaiseCanExecuteChanged();
            _lastShiftPageCommand.RaiseCanExecuteChanged();
        }

        private bool CanGoNextShiftPage() => CurrentShiftPage < TotalShiftPages;
        private bool CanGoPrevShiftPage() => CurrentShiftPage > 1;
        private void NextShiftPage()
        {
            if (!CanGoNextShiftPage()) return;
            CurrentShiftPage++;
            UpdatePagedShiftDetails();
        }
        private void PrevShiftPage()
        {
            if (!CanGoPrevShiftPage()) return;
            CurrentShiftPage--;
            UpdatePagedShiftDetails();
        }
        private void FirstShiftPage()
        {
            if (!CanGoPrevShiftPage()) return;
            CurrentShiftPage = 1;
            UpdatePagedShiftDetails();
        }
        private void LastShiftPage()
        {
            if (!CanGoNextShiftPage()) return;
            CurrentShiftPage = TotalShiftPages;
            UpdatePagedShiftDetails();
        }

        private async Task LoadUsersAsync()
        {
            try
            {
                var svc = new ApiService();
                var users = await svc.GetUsersAsync();
                OutletUsers.Clear();
                foreach (var u in users)
                {
                    OutletUsers.Add(u);
                }
                OnPropertyChanged(nameof(OutletUsers));
                UpdateFilteredUsers();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load users: {ex.Message}");
                try { POS_UI.Services.LogService.Error("SettingsViewModel: Failed to load users", ex); } catch { }
            }
        }

        private void UpdateFilteredUsers()
        {
            FilteredOutletUsers.Clear();
            IEnumerable<UserModel> source = OutletUsers;
            var query = (UserSearchText ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(query))
            {
                var q = query.ToLowerInvariant();
                source = source.Where(u =>
                    (!string.IsNullOrWhiteSpace(u.FirstName) && u.FirstName.ToLowerInvariant().Contains(q)) ||
                    (!string.IsNullOrWhiteSpace(u.LastName) && u.LastName.ToLowerInvariant().Contains(q)) ||
                    (!string.IsNullOrWhiteSpace(u.Email) && u.Email.ToLowerInvariant().Contains(q)) ||
                    u.ApiId.ToString().Contains(q));
            }
            foreach (var u in source)
            {
                FilteredOutletUsers.Add(u);
            }
            OnPropertyChanged(nameof(FilteredOutletUsers));
        }

        private async Task OpenSelectedUserShiftDialogAsync()
        {
            try
            {
                // If admin, SelectedShiftUser must be set from UI. If cashier, use LoggedInUser.
                var userForShift = SelectedShiftUser;
                if (!IsAdmin)
                {
                    // current user id is in JWT; however API user id might not be present in LoggedInUser
                    // fallback to fetching current user details which includes email/name; for shift endpoint we need numeric id.
                    // If LoggedInUser.ApiId not present, try loading users and matching by email/name.
                    if (LoggedInUser != null && LoggedInUser.ApiId == 0)
                    {
                        await LoadUsersAsync();
                        var match = OutletUsers.FirstOrDefault(x => 
                            !string.IsNullOrWhiteSpace(x.Email) && x.Email.Equals(LoggedInUser.Email, StringComparison.OrdinalIgnoreCase));
                        if (match != null) LoggedInUser.ApiId = match.ApiId;
                    }
                    userForShift = new UserModel
                    {
                        ApiId = LoggedInUser?.ApiId ?? 0,
                        FirstName = LoggedInUser?.FirstName,
                        LastName = LoggedInUser?.LastName,
                        Email = LoggedInUser?.Email
                    };
                }

                if (userForShift == null || userForShift.ApiId <= 0)
                {
                    MessageBox.Show("User id not found for shift details.");
                    return;
                }

                IsLoadingShiftData = true;
                CurrentShiftData = await _apiService.GetUserShiftsAsync(userForShift.ApiId, SelectedFromDate, SelectedToDate);
                OnPropertyChanged(nameof(CurrentShiftData));

                IsShiftDialogOpen = true;
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Failed to open shift details: {ex.Message}");
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to open shift details", ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            finally
            {
                IsLoadingShiftData = false;
            }
        }

        private async Task LoadShopShiftDataAsync()
        {
            try
            {
                IsLoadingShopShiftData = true;
                
                // Get shop details to get shop ID
                var (tenantCode, outletCode, brandId) = _settingsService.LoadSettings();
                var shopDetails = await _apiService.GetShopDetailsAsync(outletCode, brandId);
                
                if (shopDetails == null || shopDetails.Id <= 0)
                {
                    //MessageBox.Show("Shop details not found.");
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Shop details not found", "Shop details not found.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                    return;
                }

                // Load shop shift data for current date
                SelectedShopFromDate = DateTime.Today;
                SelectedShopToDate = DateTime.Today;
                CurrentShopShiftData = await _apiService.GetShopShiftInfoAsync(shopDetails.Id, SelectedShopFromDate, SelectedShopToDate);
                
                // Load users to populate user names
                await LoadUsersAsync();
                
                // Populate user names in shift details
                if (CurrentShopShiftData?.ShiftDetails != null)
                {
                    foreach (var shiftDetail in CurrentShopShiftData.ShiftDetails)
                    {
                        var user = OutletUsers.FirstOrDefault(u => u.ApiId == shiftDetail.UserId);
                        shiftDetail.UserName = user?.FullName ?? $"User {shiftDetail.UserId}";
                    }
                }
                
                OnPropertyChanged(nameof(CurrentShopShiftData));
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Failed to load shop shift data: {ex.Message}");
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to load shop shift data", ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            finally
            {
                IsLoadingShopShiftData = false;
            }
        }

        private async Task ShowShopShiftDataAsync()
        {
            try
            {
                IsLoadingShopShiftData = true;
                var (tenantCode, outletCode, brandId) = _settingsService.LoadSettings();
                var shopDetails = await _apiService.GetShopDetailsAsync(outletCode, brandId);
                if (shopDetails == null || shopDetails.Id <= 0) { MessageBox.Show("Shop details not found."); return; }

                CurrentShopShiftData = await _apiService.GetShopShiftInfoAsync(shopDetails.Id, SelectedShopFromDate, SelectedShopToDate);

                // Ensure users list for name mapping
                await LoadUsersAsync();
                if (CurrentShopShiftData?.ShiftDetails != null)
                {
                    foreach (var sd in CurrentShopShiftData.ShiftDetails)
                    {
                        var user = OutletUsers.FirstOrDefault(u => u.ApiId == sd.UserId);
                        sd.UserName = user?.FullName ?? $"User {sd.UserId}";
                    }
                }

                OnPropertyChanged(nameof(CurrentShopShiftData));
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Failed to load shop shift data: {ex.Message}");
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to load shop shift data", ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            finally
            {
                IsLoadingShopShiftData = false;
            }
        }

        private async Task OpenUserShiftDetailsFromShopAsync(object parameter)
        {
            try
            {
                if (parameter is ShopShiftDetailModel shopShiftDetail)
                {
                    // Get user details for the selected user
                    var user = OutletUsers.FirstOrDefault(u => u.ApiId == shopShiftDetail.UserId);
                    if (user == null)
                    {
                        MessageBox.Show("User details not found.");
                        return;
                    }

                    // Set the selected user and open shift details dialog
                    SelectedShiftUser = user;
                    await OpenSelectedUserShiftDialogAsync();
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Failed to open user shift details: {ex.Message}");
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to open user shift details", ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        

        private RelayCommand getConnectedPrinterCommand;
        public ICommand GetConnectedPrinterCommand => getConnectedPrinterCommand ??= new RelayCommand(RefreshPrinters);

        private RelayCommand<string> testPrintCommand;
        public ICommand TestPrintCommand => testPrintCommand ??= new RelayCommand<string>(TestPrint);

        private RelayCommand<PrinterModel> openPrinterSettingsCommand;
        public ICommand OpenPrinterSettingsCommand => openPrinterSettingsCommand ??= new RelayCommand<PrinterModel>(OpenPrinterSettings);

        private RelayCommand<PrinterGroupModel> openConnectedPrintersDialogCommand;
        public ICommand OpenConnectedPrintersDialogCommand => openConnectedPrintersDialogCommand ??= new RelayCommand<PrinterGroupModel>(OpenConnectedPrintersDialog);



        private void RefreshPrinters()
        {
            PrintersService.Instance.GetConnectedPrinters();
        }

        

        private void TestPrint(string printerName)
        {
            try
            {
                if (string.IsNullOrEmpty(printerName))
                {
                    MessageBox.Show("Printer name is required for test printing.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Create a simple test print content
                string testContent = $@"
==========================================
            TEST PRINT
==========================================
Printer: {printerName}
Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
==========================================
This is a test print to verify that the
printer '{printerName}' is working correctly.
==========================================
";

                // Convert the test content to bytes (assuming ESC/POS format)
                byte[] printData = System.Text.Encoding.ASCII.GetBytes(testContent);

                // Add cut command (ESC/POS cut command: GS V m)
                // 0x1D = GS (Group Separator)
                // 0x56 = V (Cut command)
                // 0x00 = Full cut (0x00 = full cut, 0x01 = partial cut)
                byte[] cutCommand = new byte[] { 0x1D, 0x56, 0x00 };

                // Combine print data with cut command
                byte[] combinedData = new byte[printData.Length + cutCommand.Length];
                Array.Copy(printData, 0, combinedData, 0, printData.Length);
                Array.Copy(cutCommand, 0, combinedData, printData.Length, cutCommand.Length);

                // Send to printer using the existing RawPrinterHelper
                bool success = RawPrinterHelper.SendBytesToPrinter(printerName, combinedData);

                if (success)
                {
                    MessageBox.Show($"Test print sent successfully to {printerName} with paper cut", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Failed to send test print to {printerName}. Please check if the printer is connected and accessible.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during test print: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenPrinterSettings(PrinterModel printer)
        {
            try
            {
                var dialog = new View.PrinterSettingsDialog(printer);
                dialog.Owner = Application.Current.MainWindow;
                
                bool? result = dialog.ShowDialog();
                
                if (result == true)
                {
                    // Trigger property change notification to update the UI
                    OnPropertyChanged(nameof(Printers));
                    
                    MessageBox.Show($"Printer '{printer.DeviceName}' settings updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening printer settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OpenConnectedPrintersDialog(PrinterGroupModel printerGroup)
        {
            try
            {
                if (printerGroup == null)
                {
                    return;
                }

                var dialog = new View.ConnectedPrinterGroupsDialog(printerGroup, TestPrintCommand);
                await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog");
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error opening connected printers dialog", ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
        }



        private RelayCommand addCardMachineCommand;
        public ICommand AddCardMachineCommand => addCardMachineCommand ??= new RelayCommand(AddCardMachine);

        private RelayCommand<CardMachineModel> editCardMachineCommand;
        public ICommand EditCardMachineCommand => editCardMachineCommand ??= new RelayCommand<CardMachineModel>(EditCardMachine);

        private RelayCommand<CardMachineModel> deleteCardMachineCommand;
        public ICommand DeleteCardMachineCommand => deleteCardMachineCommand ??= new RelayCommand<CardMachineModel>(DeleteCardMachine);

        private RelayCommand<CardMachineModel> activateCardMachineCommand;
        public ICommand ActivateCardMachineCommand => activateCardMachineCommand ??= new RelayCommand<CardMachineModel>(ActivateCardMachine);

        private RelayCommand<CardMachineModel> deactivateCardMachineCommand;
        public ICommand DeactivateCardMachineCommand => deactivateCardMachineCommand ??= new RelayCommand<CardMachineModel>(DeactivateCardMachine);

        private RelayCommand<CardMachineModel> pairCardMachineCommand;
        public ICommand PairCardMachineCommand => pairCardMachineCommand ??= new RelayCommand<CardMachineModel>(PairCardMachine);
        private RelayCommand<CardMachineModel> printZReportCommand;
        public ICommand PrintZReportCommand => printZReportCommand ??= new RelayCommand<CardMachineModel>(PrintZReportOnTerminal);

        private RelayCommand<CardMachineModel> manageCardMachineUsersCommand;
        public ICommand ManageCardMachineUsersCommand => manageCardMachineUsersCommand ??= new RelayCommand<CardMachineModel>(ManageCardMachineUsers);

        private void AddCardMachine()
        {
            try
            {
    
                var dialog = new View.AddCardMachineDialog();
                dialog.Owner = Application.Current.MainWindow;
                

                bool? result = dialog.ShowDialog();

                
                if (result == true)
                {
                    var newCardMachine = dialog.GetCardMachine();
                    CardMachineService.Instance.AddCardMachine(newCardMachine);
                    OnPropertyChanged(nameof(CardMachines));
                    
                    MessageBox.Show($"New card machine '{newCardMachine.DeviceName}' added and paired successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {

                MessageBox.Show($"Error adding card machine: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditCardMachine(CardMachineModel cardMachine)
        {
            if (cardMachine == null) return;

            try
            {
                var dialog = new View.AddCardMachineDialog(cardMachine);
                dialog.Owner = Application.Current.MainWindow;
                
                bool? result = dialog.ShowDialog();
                
                if (result == true)
                {
                    var updatedCardMachine = dialog.GetCardMachine();
                    
                    // Update the existing card machine with new values
                    cardMachine.DeviceName = updatedCardMachine.DeviceName;
                    cardMachine.DeviceType = updatedCardMachine.DeviceType;
                    cardMachine.IPAddress = updatedCardMachine.IPAddress;
                    cardMachine.Port = updatedCardMachine.Port;
                    cardMachine.DeviceId = updatedCardMachine.DeviceId;
                    cardMachine.ParingCode = updatedCardMachine.ParingCode;
                    cardMachine.ParingCodeTime = updatedCardMachine.ParingCodeTime;
                    cardMachine.IsActive = updatedCardMachine.IsActive;
                    cardMachine.APIEndpoint = updatedCardMachine.APIEndpoint;
                    cardMachine.AuthToken = updatedCardMachine.AuthToken;
                    
                    // Save to file using service
                    CardMachineService.Instance.UpdateCardMachine(cardMachine);
                    OnPropertyChanged(nameof(CardMachines));
                    
                    MessageBox.Show($"Card machine '{cardMachine.DeviceName}' updated and paired successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error editing card machine: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteCardMachine(CardMachineModel cardMachine)
        {
            if (cardMachine == null) return;

            try
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete '{cardMachine.DeviceName}'?", 
                    "Confirm Delete", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    CardMachineService.Instance.DeleteCardMachine(cardMachine);
                    OnPropertyChanged(nameof(CardMachines));
                    MessageBox.Show($"Card machine '{cardMachine.DeviceName}' deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting card machine: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ActivateCardMachine(CardMachineModel cardMachine)
        {
            if (cardMachine == null) return;

            try
            {
                cardMachine.IsActive = true;
                CardMachineService.Instance.UpdateCardMachine(cardMachine);
                OnPropertyChanged(nameof(CardMachines));
                
                MessageBox.Show($"Card machine '{cardMachine.DeviceName}' activated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error activating card machine: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeactivateCardMachine(CardMachineModel cardMachine)
        {
            if (cardMachine == null) return;

            try
            {
                cardMachine.IsActive = false;
                CardMachineService.Instance.UpdateCardMachine(cardMachine);
                OnPropertyChanged(nameof(CardMachines));
                
                MessageBox.Show($"Card machine '{cardMachine.DeviceName}' deactivated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deactivating card machine: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PairCardMachine(CardMachineModel cardMachine)
        {
            if (cardMachine == null) return;

            try
            {
                var dialog = new View.PairCardMachineDialog(cardMachine);
                dialog.Owner = Application.Current.MainWindow;
                
                bool? result = dialog.ShowDialog();
                
                if (result == true && dialog.PairingSuccessful)
                {
                    // Update the card machine with new auth token and pairing info
                    cardMachine.AuthToken = dialog.AuthToken;
                    cardMachine.ParingCode = int.TryParse(dialog.PairingCode, out int code) ? code : 0;
                    cardMachine.ParingCodeTime = DateTime.Now;
                    
                    CardMachineService.Instance.UpdateCardMachine(cardMachine);
                    OnPropertyChanged(nameof(CardMachines));
                    
                    MessageBox.Show($"Card machine '{cardMachine.DeviceName}' paired successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error pairing card machine: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void PrintZReportOnTerminal(CardMachineModel cardMachine)
        {
            try
            {
                if (cardMachine == null)
                {
                    System.Windows.MessageBox.Show("No card machine selected.", "Card Machine", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                if (!cardMachine.IsActive || string.IsNullOrWhiteSpace(cardMachine.AuthToken))
                {
                    System.Windows.MessageBox.Show("Card machine is not active or not paired.", "Card Machine", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                var api = new POS_UI.Services.CardMachineApiService();
                var ok = await api.PrintZReportAsync(cardMachine);
                if (!ok)
                {
                    System.Windows.MessageBox.Show("Failed to print Z Report from terminal.", "Card Machine", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
                else
                {
                    System.Windows.MessageBox.Show("Z Report requested on terminal.", "Card Machine", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"Error printing Z Report: {ex.Message}", "Card Machine", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ManageCardMachineUsers(CardMachineModel cardMachine)
        {
            if (cardMachine == null) return;

            try
            {
                var viewModel = new CardMachineUsersDialogViewModel(cardMachine);
                var dialog = new CardMachineUsersDialog(viewModel);
                
                dialog.Owner = Application.Current.MainWindow;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error managing card machine users: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OpenUserDetailsDialogAsync(UserModel user)
        {
            try
            {
                if (user == null)
                {
                    MessageBox.Show("No user selected.");
                    return;
                }
                var api = new ApiService();
                // Api expects string id
                var details = await api.GetUserByIdAsync(user.ApiId.ToString());
                SelectedUserDetails = details ?? user;
                IsUserDetailsDialogOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load user details: {ex.Message}");
            }
        }

        // Cash Session Pagination Methods
        private void RecalculateCashSessionPaging(bool resetToFirstPage = false)
        {
            if (_allCashSessions == null || !_allCashSessions.Any())
            {
                CashDrawerSessions.Clear();
                CashSessionTotalPages = 1;
                CashSessionCurrentPage = 1;
                HasNoCashSessions = true;
                return;
            }

            HasNoCashSessions = false; // We have sessions
            CashSessionTotalPages = (int)Math.Ceiling((double)_allCashSessions.Count / CashSessionPageSize);
            
            if (resetToFirstPage)
            {
                CashSessionCurrentPage = 1;
            }
            else if (CashSessionCurrentPage > CashSessionTotalPages)
            {
                CashSessionCurrentPage = CashSessionTotalPages;
            }

            UpdateCashSessionPagedOrders();
        }

        private void UpdateCashSessionPagedOrders()
        {
            if (_allCashSessions == null || !_allCashSessions.Any())
            {
                CashDrawerSessions.Clear();
                return;
            }

            int skip = (CashSessionCurrentPage - 1) * CashSessionPageSize;
            var pagedSessions = _allCashSessions.Skip(skip).Take(CashSessionPageSize).ToList();

            CashDrawerSessions.Clear();
            foreach (var session in pagedSessions)
            {
                CashDrawerSessions.Add(session);
            }
        }

        private void RefreshCashSessionPaginationCommands()
        {
            ((RelayCommand)CashSessionNextPageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CashSessionPrevPageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CashSessionFirstPageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CashSessionLastPageCommand).RaiseCanExecuteChanged();
        }

        private bool CanGoCashSessionNextPage() => CashSessionCurrentPage < CashSessionTotalPages;
        private bool CanGoCashSessionPrevPage() => CashSessionCurrentPage > 1;

        private void CashSessionNextPage()
        {
            if (!CanGoCashSessionNextPage()) return;
            CashSessionCurrentPage++;
            UpdateCashSessionPagedOrders();
        }

        private void CashSessionPrevPage()
        {
            if (!CanGoCashSessionPrevPage()) return;
            CashSessionCurrentPage--;
            UpdateCashSessionPagedOrders();
        }

        private void CashSessionFirstPage()
        {
            if (!CanGoCashSessionPrevPage()) return;
            CashSessionCurrentPage = 1;
            UpdateCashSessionPagedOrders();
        }

        private void CashSessionLastPage()
        {
            if (!CanGoCashSessionNextPage()) return;
            CashSessionCurrentPage = CashSessionTotalPages;
            UpdateCashSessionPagedOrders();
        }

        private void ClearCashSessionDates()
        {
            FromDate = null;
            ToDate = null;
            // Clear sessions and show no data message when dates are cleared
            /*_allCashSessions.Clear();
            CashDrawerSessions.Clear();
            HasNoCashSessions = true;*/
        }

        public async Task LoadCashTransactionsAsync()
        {
            try
            {
                if (TransactionFromDate == null)
                {
                   var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("From Date is required", "From Date is required. Please select a date.");
                   var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                   MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                   return;
                }
                IsLoadingCashTransactions = true;
                var transactions = await _apiService.GetCashDrawerTransactionsAsync(TransactionFromDate, TransactionToDate);
                
                _allCashTransactions = transactions.ToList();
                HasNoCashTransactions = _allCashTransactions.Count == 0;
                RecalculateCashTransactionPaging(resetToFirstPage: true);
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to load cash transactions", ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                HasNoCashTransactions = true; // Show no data message on error
            }
            finally
            {
                IsLoadingCashTransactions = false;
            }
        }

        private void ClearCashTransactionDates()
        {
            TransactionFromDate = null;
            TransactionToDate = null;
            // Clear transactions and show no data message when dates are cleared
            /*_allCashTransactions.Clear();
            CashDrawerTransactions.Clear();
            HasNoCashTransactions = true;*/
        }

        private void RecalculateCashTransactionPaging(bool resetToFirstPage = false)
        {
            if (_allCashTransactions == null || !_allCashTransactions.Any())
            {
                CashTransactionTotalPages = 1;
                CashTransactionCurrentPage = 1;
                CashDrawerTransactions = new ObservableCollection<CashDrawerTransactionModel>();
                HasNoCashTransactions = true;
                return;
            }

            HasNoCashTransactions = false; // We have transactions
            CashTransactionTotalPages = (int)Math.Ceiling((double)_allCashTransactions.Count / CashTransactionPageSize);

            if (resetToFirstPage)
                CashTransactionCurrentPage = 1;
            else if (CashTransactionCurrentPage > CashTransactionTotalPages)
                CashTransactionCurrentPage = CashTransactionTotalPages;

            UpdateCashTransactionPagedOrders();
        }

        private void UpdateCashTransactionPagedOrders()
        {
            if (_allCashTransactions == null || !_allCashTransactions.Any())
            {
                CashDrawerTransactions = new ObservableCollection<CashDrawerTransactionModel>();
                return;
            }

            int skip = (CashTransactionCurrentPage - 1) * CashTransactionPageSize;
            var pagedTransactions = _allCashTransactions.Skip(skip).Take(CashTransactionPageSize).ToList();
            CashDrawerTransactions = new ObservableCollection<CashDrawerTransactionModel>(pagedTransactions);
        }

        private void RefreshCashTransactionPaginationCommands()
        {
            ((RelayCommand)CashTransactionNextPageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CashTransactionPrevPageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CashTransactionFirstPageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CashTransactionLastPageCommand).RaiseCanExecuteChanged();
        }

        private bool CanGoCashTransactionNextPage() => CashTransactionCurrentPage < CashTransactionTotalPages;
        private bool CanGoCashTransactionPrevPage() => CashTransactionCurrentPage > 1;

        private void CashTransactionNextPage()
        {
            if (CanGoCashTransactionNextPage())
            {
                CashTransactionCurrentPage++;
                UpdateCashTransactionPagedOrders();
            }
        }

        private void CashTransactionPrevPage()
        {
            if (CanGoCashTransactionPrevPage())
            {
                CashTransactionCurrentPage--;
                UpdateCashTransactionPagedOrders();
            }
        }

        private void CashTransactionFirstPage()
        {
            CashTransactionCurrentPage = 1;
            UpdateCashTransactionPagedOrders();
        }

        private void CashTransactionLastPage()
        {
            CashTransactionCurrentPage = CashTransactionTotalPages;
            UpdateCashTransactionPagedOrders();
        }

        // ============================================
        // MENU CONFIGURATION METHODS
        // ============================================

        /// <summary>
        /// Loads menu configuration from API
        /// </summary>
        /// <summary>
        /// Initializes the menu tab by loading from cache (no API calls)
        /// </summary>
        private async Task InitializeMenuTabAsync()
        {
            try
            {
                IsLoadingMenuConfig = true;
                System.Diagnostics.Debug.WriteLine("======================================");
                System.Diagnostics.Debug.WriteLine("[SettingsVM] ========== INITIALIZING MENU TAB FROM CACHE ==========");

                // Get menu config from cache
                var config = GlobalDataService.Instance.CachedMenuConfig;
                
                if (config == null)
                {
                    // If cache is not available, load from API once
                    System.Diagnostics.Debug.WriteLine("[SettingsVM] Cache not available, loading from API...");
                    config = await MenuConfigService.Instance.LoadMenuConfigAsync();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsVM] Using cached menu config");
                }
                
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Received {config.Tabs.Count} tabs");
                
                // Populate MenuTabs observable collection
                MenuTabs.Clear();
                foreach (var tab in config.Tabs.OrderBy(t => t.Order))
                {
                    MenuTabs.Add(tab);
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] Tab #{tab.Order}: '{tab.Name}' (Type={tab.ContentType}, Default={tab.IsDefault})");
                    if (tab.ContentType == "categories")
                    {
                        System.Diagnostics.Debug.WriteLine($"           → CategoryIds: [{string.Join(", ", tab.CategoryIds)}]");
                    }
                    else if (tab.ContentType == "items")
                    {
                        System.Diagnostics.Debug.WriteLine($"           → ItemIds: [{string.Join(", ", tab.ItemIds)}]");
                    }
                }
                
                HasUnsavedChanges = false;
                OnPropertyChanged(nameof(CanAddMoreTabs));
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] ✓✓✓ Loaded {MenuTabs.Count} menu tabs from cache ✓✓✓");
                System.Diagnostics.Debug.WriteLine("======================================");
                
                // Load available categories and products for the tab editor (from cache)
                await LoadMenuDataForEditorAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] ✗✗✗ Error initializing menu tab: {ex.Message} ✗✗✗");
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Failed to load menu configuration: {ex.Message}\n\nShowing default tab.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                IsLoadingMenuConfig = false;
            }
        }

        /// <summary>
        /// Refreshes menu data from API (triggered by Refresh button)
        /// </summary>
        private async Task LoadMenuConfigAsync()
        {
            try
            {
                IsLoadingMenuConfig = true;
                System.Diagnostics.Debug.WriteLine("======================================");
                System.Diagnostics.Debug.WriteLine("[SettingsVM] ========== REFRESHING MENU DATA FROM API ==========");

                // Refresh all menu data from API (categories, products, menu config)
                await GlobalDataService.Instance.RefreshMenuDataAsync();
                
                System.Diagnostics.Debug.WriteLine("[SettingsVM] Menu data refreshed from API");
                
                // Get the refreshed menu config from cache
                var config = GlobalDataService.Instance.CachedMenuConfig;
                
                if (config == null)
                {
                    // Fallback: Load directly from API if cache is not available
                    System.Diagnostics.Debug.WriteLine("[SettingsVM] Cache not available, loading directly from API...");
                    config = await MenuConfigService.Instance.LoadMenuConfigAsync();
                }
                
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Received {config.Tabs.Count} tabs");
                
                // Populate MenuTabs observable collection
                MenuTabs.Clear();
                foreach (var tab in config.Tabs.OrderBy(t => t.Order))
                {
                    MenuTabs.Add(tab);
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] Tab #{tab.Order}: '{tab.Name}' (Type={tab.ContentType}, Default={tab.IsDefault})");
                    if (tab.ContentType == "categories")
                    {
                        System.Diagnostics.Debug.WriteLine($"           → CategoryIds: [{string.Join(", ", tab.CategoryIds)}]");
                    }
                    else if (tab.ContentType == "items")
                    {
                        System.Diagnostics.Debug.WriteLine($"           → ItemIds: [{string.Join(", ", tab.ItemIds)}]");
                    }
                }
                
                HasUnsavedChanges = false;
                OnPropertyChanged(nameof(CanAddMoreTabs));
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] ✓✓✓ Loaded {MenuTabs.Count} menu tabs successfully ✓✓✓");
                System.Diagnostics.Debug.WriteLine("======================================");
                
                // Load available categories and products for the tab editor (from cache)
                await LoadMenuDataForEditorAsync();
                
                // Show nice success dialog
                var successVm = StatusDialogViewModel.CreateSuccess(
                    "Menu Refreshed Successfully", 
                    "All menu data has been refreshed from the server.\n\nCategories, products, and menu tabs are now up to date.");
                var successDlg = new StatusDialog { DataContext = successVm };
                MaterialDesignThemes.Wpf.DialogHost.Show(successDlg, "RootDialog");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] ✗✗✗ Error refreshing menu data: {ex.Message} ✗✗✗");
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Stack trace: {ex.StackTrace}");
                
                // Show nice error dialog
                var errorVm = StatusDialogViewModel.CreateError(
                    "Refresh Failed", 
                    $"Failed to refresh menu data from server.\n\n{ex.Message}");
                var errorDlg = new StatusDialog { DataContext = errorVm };
                MaterialDesignThemes.Wpf.DialogHost.Show(errorDlg, "RootDialog");
            }
            finally
            {
                IsLoadingMenuConfig = false;
            }
        }

        /// <summary>
        /// Loads available categories and products for the tab editor from cache
        /// </summary>
        private async Task LoadMenuDataForEditorAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[SettingsVM] Loading menu data for editor...");
                
                var globalData = GlobalDataService.Instance;
                
                // Try to use cached data first
                if (globalData.IsMenuDataLoaded)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsVM] Using cached menu data");
                    
                    _availableCategories.Clear();
                    _availableCategories.Add("All Items");
                    foreach (var cat in globalData.CachedCategories)
                    {
                        _availableCategories.Add(cat);
                    }
                    
                    _availableProducts.Clear();
                    foreach (var prod in globalData.CachedProducts)
                    {
                        _availableProducts.Add(prod);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] Loaded {globalData.CachedCategories.Count} categories and {globalData.CachedProducts.Count} products from cache");
                }
                else
                {
                    // Fallback: Load from API if cache is not available
                    System.Diagnostics.Debug.WriteLine("[SettingsVM] Cache not available, loading from API...");
                    
                    var (categories, products) = await _apiService.GetProductsAndCategoriesAsync();
                    
                    _availableCategories.Clear();
                    _availableCategories.Add("All Items");
                    foreach (var cat in categories)
                    {
                        _availableCategories.Add(cat);
                    }
                    
                    _availableProducts.Clear();
                    foreach (var prod in products)
                    {
                        _availableProducts.Add(prod);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] Loaded {categories.Count} categories and {products.Count} products from API");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Error loading menu data: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens dialog to add a new menu tab
        /// </summary>
        private async Task AddMenuTabAsync()
        {
            try
            {
                if (MenuTabs.Count >= 5)
                {
                    MessageBox.Show("Maximum of 5 tabs reached. Delete a tab to add a new one.", "Maximum Tabs", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Create dialog view model
                var dialogViewModel = new EditMenuTabViewModel(null, _availableCategories, _availableProducts);
                
                // Set up callbacks BEFORE showing dialog
                dialogViewModel.OnSave = async (tab) =>
                {
                    var maxId = MenuTabs.Any() ? MenuTabs.Max(t => t.Id) : 0;
                    var maxOrder = MenuTabs.Any() ? MenuTabs.Max(t => t.Order) : 0;
                    
                    tab.Id = maxId + 1;
                    tab.Order = maxOrder + 1;
                    tab.IsDefault = false;
                    
                    MenuTabs.Add(tab);
                    OnPropertyChanged(nameof(CanAddMoreTabs));
                    
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] ✓ Added {tab.ContentType.ToUpper()} tab: '{tab.Name}' with {tab.Slots?.Count ?? 0} slots ({tab.CategoryIds.Count} categories, {tab.ItemIds.Count} items)");
                    
                    IsMenuDialogOpen = false;
                    
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] Auto-saving to API...");
                    await SaveMenuConfigAsync();
                };
                
                dialogViewModel.OnCancel = () =>
                {
                    IsMenuDialogOpen = false;
                };
                
                // Create dialog view
                var dialogView = new POS_UI.View.Dialogs.EditMenuTabDialog
                {
                    DataContext = dialogViewModel
                };

                // Show custom full-screen dialog
                MenuDialogContent = dialogView;
                IsMenuDialogOpen = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Error adding menu tab: {ex.Message}");
                MessageBox.Show($"Failed to add tab: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Opens dialog to edit an existing menu tab
        /// </summary>
        private async Task EditMenuTabAsync(MenuTabModel tab)
        {
            try
            {
                if (tab == null) return;

                // Cannot edit default tab
                if (tab.IsDefault)
                {
                    MessageBox.Show("The default 'All Items' tab cannot be edited.\n\nIt will automatically load based on the menu data from the API.", 
                        "Cannot Edit Default Tab", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Create dialog view model
                var dialogViewModel = new EditMenuTabViewModel(tab, _availableCategories, _availableProducts);
                
                // Set up callbacks BEFORE showing dialog
                dialogViewModel.OnSave = async (updatedTab) =>
                {
                    tab.Name = updatedTab.Name;
                    tab.ContentType = updatedTab.ContentType;
                    tab.CategoryIds = updatedTab.CategoryIds;
                    tab.ItemIds = updatedTab.ItemIds;
                    tab.Slots = updatedTab.Slots;
                    
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] ✓ Updated {tab.ContentType.ToUpper()} tab: '{tab.Name}' with {tab.Slots?.Count ?? 0} slots ({tab.CategoryIds.Count} categories, {tab.ItemIds.Count} items)");
                    
                    IsMenuDialogOpen = false;
                    OnPropertyChanged(nameof(MenuTabs));
                    
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] Auto-saving to API...");
                    await SaveMenuConfigAsync();
                };
                
                dialogViewModel.OnCancel = () =>
                {
                    IsMenuDialogOpen = false;
                };
                
                // Create dialog view
                var dialogView = new POS_UI.View.Dialogs.EditMenuTabDialog
                {
                    DataContext = dialogViewModel
                };

                // Show custom full-screen dialog
                MenuDialogContent = dialogView;
                IsMenuDialogOpen = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Error editing menu tab: {ex.Message}");
                MessageBox.Show($"Failed to edit tab: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Deletes a menu tab (cannot delete default tab)
        /// </summary>
        private async Task DeleteMenuTabAsync(MenuTabModel tab)
        {
            try
            {
                if (tab == null) return;

                if (tab.IsDefault)
                {
                    MessageBox.Show("Cannot delete the default tab.", "Delete Tab", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Are you sure you want to delete the tab '{tab.Name}'?", 
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    MenuTabs.Remove(tab);
                    OnPropertyChanged(nameof(CanAddMoreTabs));
                    
                    // Reorder remaining tabs
                    int order = 1;
                    foreach (var t in MenuTabs.OrderBy(t => t.Order))
                    {
                        t.Order = order++;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] Deleted tab: {tab.Name}");
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] Auto-saving to API...");
                    
                    // Automatically save to API
                    await SaveMenuConfigAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Error deleting menu tab: {ex.Message}");
                MessageBox.Show($"Failed to delete tab: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Moves a tab up in the order
        /// </summary>
        private async Task MoveTabUpAsync(MenuTabModel tab)
        {
            try
            {
                if (tab == null || tab.Order <= 1) return;

                var previousTab = MenuTabs.FirstOrDefault(t => t.Order == tab.Order - 1);
                if (previousTab != null)
                {
                    // Swap orders
                    previousTab.Order++;
                    tab.Order--;
                    
                    // Re-sort the collection
                    var sortedTabs = MenuTabs.OrderBy(t => t.Order).ToList();
                    MenuTabs.Clear();
                    foreach (var t in sortedTabs)
                    {
                        MenuTabs.Add(t);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] Moved tab '{tab.Name}' up");
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] Auto-saving to API...");
                    
                    // Automatically save to API
                    await SaveMenuConfigAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Error moving tab up: {ex.Message}");
            }
        }

        /// <summary>
        /// Moves a tab down in the order
        /// </summary>
        private async Task MoveTabDownAsync(MenuTabModel tab)
        {
            try
            {
                if (tab == null || tab.Order >= MenuTabs.Count) return;

                var nextTab = MenuTabs.FirstOrDefault(t => t.Order == tab.Order + 1);
                if (nextTab != null)
                {
                    // Swap orders
                    nextTab.Order--;
                    tab.Order++;
                    
                    // Re-sort the collection
                    var sortedTabs = MenuTabs.OrderBy(t => t.Order).ToList();
                    MenuTabs.Clear();
                    foreach (var t in sortedTabs)
                    {
                        MenuTabs.Add(t);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] Moved tab '{tab.Name}' down");
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] Auto-saving to API...");
                    
                    // Automatically save to API
                    await SaveMenuConfigAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Error moving tab down: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves the current menu configuration to API
        /// </summary>
        private async Task SaveMenuConfigAsync(bool showSuccessMessage = false)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("======================================");
                System.Diagnostics.Debug.WriteLine("[SettingsVM] ========== SAVING MENU CONFIGURATION ==========");
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Total tabs to save: {MenuTabs.Count}");
                
                // Log each tab before saving
                foreach (var tab in MenuTabs)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] Tab #{tab.Order}: '{tab.Name}' (Type={tab.ContentType}, Default={tab.IsDefault})");
                    if (tab.ContentType == "categories")
                    {
                        System.Diagnostics.Debug.WriteLine($"           → CategoryIds: [{string.Join(", ", tab.CategoryIds)}]");
                    }
                    else if (tab.ContentType == "items")
                    {
                        System.Diagnostics.Debug.WriteLine($"           → ItemIds: [{string.Join(", ", tab.ItemIds)}]");
                    }
                    else if (tab.ContentType == "mixed")
                    {
                        System.Diagnostics.Debug.WriteLine($"           → CategoryIds: [{string.Join(", ", tab.CategoryIds)}]");
                        System.Diagnostics.Debug.WriteLine($"           → ItemIds: [{string.Join(", ", tab.ItemIds)}]");
                        System.Diagnostics.Debug.WriteLine($"           → Slots: {tab.Slots?.Count ?? 0} entries");
                    }
                }

                // Get current config and update it with modified tabs
                var config = MenuConfigService.Instance.GetCurrentConfig();
                if (config == null)
                {
                    // Create new config from current tabs
                    var localStorage = new LocalStorageService();
                    var shopDetails = localStorage.GetShopDetails();
                    var settingsService = new SettingsService();
                    var (_, _, brandIdStr) = settingsService.LoadSettings();
                    int.TryParse(brandIdStr, out int brandId);
                    
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] Creating new config: ShopId={shopDetails?.Id}, BrandId={brandId}");
                    
                    config = new MenuConfigModel
                    {
                        BrandId = brandId,
                        OutletId = shopDetails?.Id ?? 0,
                        TerminalId = "1",
                        Tabs = MenuTabs.ToList()
                    };
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM] Updating existing config");
                    config.Tabs = MenuTabs.ToList();
                }

                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Calling MenuConfigService.SaveMenuConfigAsync...");
                var success = await MenuConfigService.Instance.SaveMenuConfigAsync(config);
                
                if (success)
                {
                    HasUnsavedChanges = false;
                    
                    // Update the GlobalDataService cache with the new config
                    GlobalDataService.Instance.UpdateCachedMenuConfig(config);
                    System.Diagnostics.Debug.WriteLine("[SettingsVM] Updated GlobalDataService menu cache");
                    
                    // Reload colors from disk to pick up any color changes made in the dialog
                    Helpers.ColorPalette.ReloadColorMappings();
                    System.Diagnostics.Debug.WriteLine("[SettingsVM] Reloaded color mappings to pick up changes");
                    
                    // Notify cashier to refresh with new colors (triggers MenuDataRefreshed event)
                    GlobalDataService.Instance.NotifyMenuDataRefreshed();
                    System.Diagnostics.Debug.WriteLine("[SettingsVM] Notified cashier view to refresh colors");
                    
                    if (showSuccessMessage)
                    {
                        MessageBox.Show("Menu configuration saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    System.Diagnostics.Debug.WriteLine("[SettingsVM] ✓✓✓ Menu configuration SAVED SUCCESSFULLY ✓✓✓");
                }
                else
                {
                    MessageBox.Show("Failed to save menu configuration. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    System.Diagnostics.Debug.WriteLine("[SettingsVM] ✗✗✗ Failed to save menu configuration ✗✗✗");
                }
                System.Diagnostics.Debug.WriteLine("======================================");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] ✗✗✗ Error saving menu config: {ex.Message} ✗✗✗");
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Failed to save menu configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================
        // FLOOR PLAN METHODS
        // ============================================
        private async Task InitializeFloorPlanTabAsync(bool forceReloadTables = false)
        {
            // Same idea as Menu tab: show last-known plans immediately from cache, then refresh from API (tables list is editor-only — do not block the list).
            _ = LoadTablesForFloorPlanEditorAsync(forceReloadTables);

            if (FloorPlanCustomItemTypes.Count == 0)
            {
                ApplyMergedCustomItemTypes(FloorPlanCustomItemCatalog.MergeWithApi(GlobalDataService.Instance.CachedFloorPlanCustomItemTypes));
            }

            var cached = GlobalDataService.Instance.CachedFloorPlans;
            if (cached != null)
            {
                ApplyCachedFloorPlansToUi(cached);
            }

            try
            {
                _apiService.RefreshHeadersFromSettings();
                await LoadFloorPlansFromApiAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Floor plan config load from API failed: {ex.Message}");
            }
        }

        private void ApplyCachedFloorPlansToUi(IReadOnlyList<FloorPlanModel> cached)
        {
            var prevId = SelectedFloorPlan?.Id;
            FloorPlans.Clear();
            foreach (var plan in cached)
            {
                FloorPlans.Add(plan.Clone());
            }

            SelectedFloorPlan = prevId.HasValue
                ? FloorPlans.FirstOrDefault(f => f.Id == prevId.Value) ?? FloorPlans.FirstOrDefault()
                : FloorPlans.FirstOrDefault();

            _suppressFloorPlanLayoutPersist = true;
            IsFloorPlanLayoutEnabled = GlobalDataService.Instance.IsFloorPlanLayoutEnabled;
            _suppressFloorPlanLayoutPersist = false;

            ApplyMergedCustomItemTypes(FloorPlanCustomItemCatalog.MergeWithApi(GlobalDataService.Instance.CachedFloorPlanCustomItemTypes));
        }

        /// <summary>
        /// GET /api/v1/shop/{shopId}/config/floor_plan — restores <see cref="FloorPlans"/> from <c>data</c>.
        /// </summary>
        private async Task LoadFloorPlansFromApiAsync()
        {
            var localStorage = new LocalStorageService();
            var shopDetails = localStorage.GetShopDetails();
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
            if (parsed == null)
            {
                System.Diagnostics.Debug.WriteLine("[SettingsVM] Floor plan JSON parse error");
                return;
            }

            _suppressFloorPlanLayoutPersist = true;
            IsFloorPlanLayoutEnabled = parsed.FloorPlanLayoutEnabled;
            _suppressFloorPlanLayoutPersist = false;

            FloorPlans.Clear();
            foreach (var fp in parsed.Plans)
            {
                FloorPlans.Add(fp);
            }

            SelectedFloorPlan = FloorPlans.FirstOrDefault();
            ApplyMergedCustomItemTypes(parsed.CustomItemTypes);
            GlobalDataService.Instance.UpdateCachedFloorPlans(FloorPlans, IsFloorPlanLayoutEnabled, FloorPlanCustomItemTypes.ToList());
        }

        private async Task LoadTablesForFloorPlanEditorAsync(bool forceReloadTables = false)
        {
            if (!forceReloadTables && AvailableFloorPlanTables.Any())
            {
                return;
            }

            IsLoadingFloorPlanTables = true;
            try
            {
                _apiService.RefreshHeadersFromSettings();
                var tables = await _apiService.GetTablesAsync();

                AvailableFloorPlanTables.Clear();
                foreach (var table in tables.OrderBy(t => t.Name))
                {
                    AvailableFloorPlanTables.Add(table);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to load floor plan tables.\n\n{ex.Message}", "Floor Plan", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoadingFloorPlanTables = false;
            }
        }

        private void AddFloorPlan()
        {
            if (IsFloorPlanDialogOpen || IsSavingFloorPlan)
            {
                return;
            }

            var nextId = FloorPlans.Any() ? FloorPlans.Max(fp => fp.Id) + 1 : 1;
            var floorPlan = new FloorPlanModel
            {
                Id = nextId,
                Name = $"Floor Plan {nextId}"
            };

            // Not added to FloorPlans until Save in the layout editor — avoids persisting to API/cache until then.
            StartEditFloorPlan(floorPlan);
        }

        private async void StartEditFloorPlan(FloorPlanModel? floorPlan)
        {
            if (floorPlan == null)
            {
                return;
            }

            SelectedFloorPlan = floorPlan;
            EditingFloorPlan = floorPlan;
            EditingFloorPlanDraft = floorPlan.Clone();
            SelectedPlacedFloorPlanTable = null;
            SelectedAvailableFloorPlanTable = null;

            await LoadTablesForFloorPlanEditorAsync(false);
            FloorPlanDialogContent = new EditFloorPlanDialog { DataContext = this };
            IsFloorPlanDialogOpen = true;
        }

        private void CancelFloorPlanEdit()
        {
            var editing = EditingFloorPlan;
            var pendingNewNotInList = editing != null && !FloorPlans.Contains(editing);

            IsFloorPlanDialogOpen = false;
            FloorPlanDialogContent = null;
            EditingFloorPlan = null;
            EditingFloorPlanDraft = null;
            SelectedPlacedFloorPlanTable = null;
            SelectedAvailableFloorPlanTable = null;
            IsFloorPlanColorPickerOpen = false;

            if (pendingNewNotInList)
            {
                SelectedFloorPlan = FloorPlans.FirstOrDefault();
            }
        }

        /// <summary>Table IDs already placed on the floor plan being edited (draft) or on any other saved floor plan.</summary>
        private HashSet<int> GetTableIdsAllocatedOnAnyFloorPlanForPicker()
        {
            var ids = new HashSet<int>();
            if (EditingFloorPlanDraft == null)
            {
                return ids;
            }

            foreach (var p in EditingFloorPlanDraft.Tables)
            {
                if (p.Kind == FloorPlanElementKind.Table && p.TableId > 0)
                {
                    ids.Add(p.TableId);
                }
            }

            foreach (var fp in FloorPlans)
            {
                if (fp.Id == EditingFloorPlanDraft.Id)
                {
                    continue;
                }

                foreach (var p in fp.Tables)
                {
                    if (p.Kind == FloorPlanElementKind.Table && p.TableId > 0)
                    {
                        ids.Add(p.TableId);
                    }
                }
            }

            return ids;
        }

        private async Task OpenSelectTableForFloorPlanAsync()
        {
            if (EditingFloorPlanDraft == null)
            {
                return;
            }

            // Always refetch outlet tables so status (e.g. Unavailable) matches the server after changes elsewhere without leaving Settings.
            await LoadTablesForFloorPlanEditorAsync(forceReloadTables: true);
            if (AvailableFloorPlanTables.Count == 0)
            {
                MessageBox.Show("No tables are available. Check your outlet configuration and try again.", "Floor Plan", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var usedTableIds = GetTableIdsAllocatedOnAnyFloorPlanForPicker();
            var dialogVm = new FloorPlanTableSelectionDialogViewModel(AvailableFloorPlanTables, usedTableIds);
            var dialog = new FloorPlanTableSelectionDialog { DataContext = dialogVm };
            var result = await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog");
            if (result is List<TableModel> selectedList)
            {
                foreach (var t in selectedList)
                {
                    AddTableFromTableModel(t);
                }
            }
            else if (result is TableModel selected)
            {
                AddTableFromTableModel(selected);
            }
        }

        private void AddTableFromTableModel(TableModel table)
        {
            if (EditingFloorPlanDraft == null)
            {
                return;
            }

            if (GetTableIdsAllocatedOnAnyFloorPlanForPicker().Contains(table.ApiId))
            {
                return;
            }

            var count = EditingFloorPlanDraft.Tables.Count;
            var defaultColor = FloorPlanColorPalette.Count > 0 ? FloorPlanColorPalette[0] : "#1976D2";
            var placement = new FloorPlanTablePlacementModel
            {
                Kind = FloorPlanElementKind.Table,
                TableId = table.ApiId,
                TableName = table.Name,
                SeatCount = table.SeatCount,
                X = 30 + ((count % 5) * 140),
                Y = 30 + ((count / 5) * 110),
                Width = 120,
                Height = 80,
                Shape = FloorPlanShapeType.Rectangle,
                ColorHex = defaultColor
            };

            EditingFloorPlanDraft.Tables.Add(placement);
            SelectPlacedFloorPlanTable(placement);
        }

        private void RemoveSelectedPlacedTableFromEditingFloorPlan()
        {
            if (EditingFloorPlanDraft == null || SelectedPlacedFloorPlanTable == null)
            {
                return;
            }

            EditingFloorPlanDraft.Tables.Remove(SelectedPlacedFloorPlanTable);
            SelectedPlacedFloorPlanTable = null;
        }

        private void ClearEditingFloorPlanTables()
        {
            if (EditingFloorPlanDraft == null)
            {
                return;
            }

            if (EditingFloorPlanDraft.Tables.Count == 0)
            {
                return;
            }

            var result = MessageBox.Show("Clear all tables and custom items from this floor plan editor?", "Clear All", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            EditingFloorPlanDraft.Tables.Clear();
            SelectedPlacedFloorPlanTable = null;
        }

        public void SelectPlacedFloorPlanTable(FloorPlanTablePlacementModel? table)
        {
            if (EditingFloorPlanDraft == null || table == null)
            {
                return;
            }

            foreach (var t in EditingFloorPlanDraft.Tables)
            {
                t.IsSelectedOnCanvas = false;
            }

            var match = EditingFloorPlanDraft.Tables.FirstOrDefault(t => ReferenceEquals(t, table));
            if (match == null && table.Kind == FloorPlanElementKind.Table)
            {
                match = EditingFloorPlanDraft.Tables.FirstOrDefault(t =>
                    t.Kind == FloorPlanElementKind.Table && t.TableId == table.TableId);
            }

            var selected = match ?? table;
            selected.IsSelectedOnCanvas = true;
            SelectedPlacedFloorPlanTable = selected;
        }

        public void MoveSelectedFloorPlanTable(double deltaX, double deltaY)
        {
            if (SelectedPlacedFloorPlanTable == null)
            {
                return;
            }

            SelectedPlacedFloorPlanTable.X += deltaX;
            SelectedPlacedFloorPlanTable.Y += deltaY;
        }

        private void AdjustSelectedFloorPlanTableSize(double widthDelta, double heightDelta)
        {
            if (SelectedPlacedFloorPlanTable == null)
            {
                return;
            }

            if (widthDelta != 0)
            {
                SelectedPlacedFloorPlanTable.Width += widthDelta;
            }

            if (heightDelta != 0)
            {
                SelectedPlacedFloorPlanTable.Height += heightDelta;
            }
        }

        private async Task SaveEditingFloorPlanAsync()
        {
            if (EditingFloorPlanDraft == null || EditingFloorPlan == null)
            {
                return;
            }

            IsSavingFloorPlan = true;
            var pendingNewNotYetInList = !FloorPlans.Contains(EditingFloorPlan);
            try
            {
                EditingFloorPlan.Name = EditingFloorPlanDraft.Name;
                EditingFloorPlan.Tables = new ObservableCollection<FloorPlanTablePlacementModel>(
                    EditingFloorPlanDraft.Tables.Select(t => t.Clone()));

                if (pendingNewNotYetInList)
                {
                    FloorPlans.Add(EditingFloorPlan);
                }

                var ok = await SaveAllFloorPlansToApiAsync();
                if (ok)
                {
                    GlobalDataService.Instance.UpdateCachedFloorPlans(FloorPlans, IsFloorPlanLayoutEnabled, FloorPlanCustomItemTypes.ToList());
                    //MessageBox.Show("Floor plan configuration saved successfully.", "Floor Plan", MessageBoxButton.OK, MessageBoxImage.Information);
                    CancelFloorPlanEdit();
                }
                else
                {
                    if (pendingNewNotYetInList)
                    {
                        FloorPlans.Remove(EditingFloorPlan);
                    }

                    MessageBox.Show("Failed to save floor plan configuration. Please try again.", "Floor Plan", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                if (pendingNewNotYetInList && FloorPlans.Contains(EditingFloorPlan))
                {
                    FloorPlans.Remove(EditingFloorPlan);
                }

                MessageBox.Show($"Failed to save floor plan.\n\n{ex.Message}", "Floor Plan", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSavingFloorPlan = false;
            }
        }

        /// <summary>
        /// PATCH body inner JSON (object that becomes <c>data</c> in the request).
        /// </summary>
        private string BuildFloorPlanConfigDataJson()
        {
            var data = new FloorPlanConfigDataModel
            {
                Name = "Floor plan configuration",
                FloorPlanLayoutEnabled = IsFloorPlanLayoutEnabled,
                FloorPlanCustomItemTypes = FloorPlanCustomItemTypes.Select(t => t.Clone()).ToList(),
                FloorPlans = FloorPlans.Select(plan => new FloorPlanSaveItemModel
                {
                    FloorPlanId = plan.Id,
                    Name = plan.Name,
                    Tables = plan.Tables
                        .Where(t => t.Kind == FloorPlanElementKind.Table)
                        .Select(table => new FloorPlanSaveTableModel
                        {
                            TableId = table.TableId,
                            Name = table.TableName,
                            SeatCount = table.SeatCount,
                            X = table.X,
                            Y = table.Y,
                            Width = table.Width,
                            Height = table.Height,
                            Shape = table.Shape.ToString().ToLowerInvariant(),
                            ColorHex = table.ColorHex
                        }).ToList(),
                    CustomItems = plan.Tables
                        .Where(t => t.Kind == FloorPlanElementKind.CustomItem)
                        .Select(ToFloorPlanSaveCustomItem)
                        .ToList()
                }).ToList()
            };

            return System.Text.Json.JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        private static FloorPlanSaveCustomItemModel ToFloorPlanSaveCustomItem(FloorPlanTablePlacementModel c) =>
            new FloorPlanSaveCustomItemModel
            {
                InstanceId = string.IsNullOrWhiteSpace(c.InstanceId) ? Guid.NewGuid().ToString("N") : c.InstanceId,
                ItemTypeKey = c.ItemTypeKey,
                Label = c.TableName,
                IconKind = string.IsNullOrWhiteSpace(c.IconKindName) ? "MapMarker" : c.IconKindName,
                X = c.X,
                Y = c.Y,
                Width = c.Width,
                Height = c.Height,
                Shape = c.Shape.ToString().ToLowerInvariant(),
                ColorHex = c.ColorHex
            };

        private void ApplyMergedCustomItemTypes(IReadOnlyList<FloorPlanCustomItemTypeModel> merged)
        {
            FloorPlanCustomItemTypes.Clear();
            foreach (var t in merged)
            {
                FloorPlanCustomItemTypes.Add(t.Clone());
            }
        }

        private async Task OpenSelectCustomItemForFloorPlanAsync()
        {
            if (EditingFloorPlanDraft == null)
            {
                return;
            }

            if (FloorPlanCustomItemTypes.Count == 0)
            {
                ApplyMergedCustomItemTypes(FloorPlanCustomItemCatalog.MergeWithApi(GlobalDataService.Instance.CachedFloorPlanCustomItemTypes));
            }

            var dialogVm = new FloorPlanCustomItemSelectionDialogViewModel(FloorPlanCustomItemTypes);
            var dialog = new FloorPlanCustomItemSelectionDialog { DataContext = dialogVm };
            var result = await DialogHost.Show(dialog, "RootDialog");
            if (result is FloorPlanAddFloorItemPickResult pick)
            {
                if (pick.CatalogType != null)
                {
                    AddCustomItemFromType(pick.CatalogType);
                }
                else if (pick.ShapePrimitive.HasValue)
                {
                    AddShapePrimitiveToFloorPlan(pick.ShapePrimitive.Value);
                }
            }
            else if (result is FloorPlanCustomItemTypeModel typeOnly)
            {
                AddCustomItemFromType(typeOnly);
            }
        }

        private void AddShapePrimitiveToFloorPlan(FloorPlanShapeType shape)
        {
            if (EditingFloorPlanDraft == null)
            {
                return;
            }

            var (w, h) = FloorPlanShapePrimitiveHelper.DefaultSize(shape);
            var count = EditingFloorPlanDraft.Tables.Count;
            var placement = new FloorPlanTablePlacementModel
            {
                Kind = FloorPlanElementKind.CustomItem,
                InstanceId = Guid.NewGuid().ToString("N"),
                ItemTypeKey = FloorPlanShapePrimitiveHelper.ItemTypeKey,
                IconKindName = FloorPlanShapePrimitiveHelper.IconKindName(shape),
                TableId = 0,
                TableName = FloorPlanShapePrimitiveHelper.DisplayLabel(shape),
                SeatCount = 0,
                X = 30 + ((count % 5) * 140),
                Y = 30 + ((count / 5) * 110),
                Width = w,
                Height = h,
                Shape = shape,
                ColorHex = "#B0BEC5"
            };

            EditingFloorPlanDraft.Tables.Add(placement);
            SelectPlacedFloorPlanTable(placement);
        }

        private void AddCustomItemFromType(FloorPlanCustomItemTypeModel type)
        {
            if (EditingFloorPlanDraft == null)
            {
                return;
            }

            var count = EditingFloorPlanDraft.Tables.Count;
            var placement = new FloorPlanTablePlacementModel
            {
                Kind = FloorPlanElementKind.CustomItem,
                InstanceId = Guid.NewGuid().ToString("N"),
                ItemTypeKey = type.Key,
                IconKindName = type.IconKind,
                TableId = 0,
                TableName = type.DisplayName,
                SeatCount = 0,
                X = 30 + ((count % 5) * 140),
                Y = 30 + ((count / 5) * 110),
                Width = type.DefaultWidth,
                Height = type.DefaultHeight,
                Shape = type.DefaultShape,
                ColorHex = type.DefaultColorHex
            };

            EditingFloorPlanDraft.Tables.Add(placement);
            SelectPlacedFloorPlanTable(placement);
        }

        private async Task<bool> SaveAllFloorPlansToApiAsync()
        {
            var localStorage = new LocalStorageService();
            var shopDetails = localStorage.GetShopDetails();
            if (shopDetails == null)
            {
                throw new InvalidOperationException("Shop details not found.");
            }

            var (_, _, brandIdText) = _settingsService.LoadSettings();
            if (!int.TryParse(brandIdText, out var brandId))
            {
                throw new InvalidOperationException("Invalid brand ID in settings.");
            }

            _apiService.RefreshHeadersFromSettings();
            var innerJson = BuildFloorPlanConfigDataJson();
            return await _apiService.SaveFloorPlanConfigAsync(shopDetails.Id, brandId, innerJson, "1");
        }

        private void DeleteFloorPlan(FloorPlanModel? floorPlan)
        {
            if (floorPlan == null)
            {
                return;
            }

            var result = MessageBox.Show($"Remove floor plan '{floorPlan.Name}'?", "Delete Floor Plan", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            if (EditingFloorPlan?.Id == floorPlan.Id)
            {
                CancelFloorPlanEdit();
            }

            FloorPlans.Remove(floorPlan);
            SelectedFloorPlan = FloorPlans.FirstOrDefault();

            _ = PersistFloorPlansAfterListChangeAsync();
        }

        private async Task PersistFloorPlansAfterListChangeAsync()
        {
            try
            {
                _apiService.RefreshHeadersFromSettings();
                if (await SaveAllFloorPlansToApiAsync())
                {
                    GlobalDataService.Instance.UpdateCachedFloorPlans(FloorPlans, IsFloorPlanLayoutEnabled, FloorPlanCustomItemTypes.ToList());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Floor plan list delete sync failed: {ex.Message}");
                MessageBox.Show(
                    $"Floor plan was removed locally but could not be saved to the server.\n\n{ex.Message}",
                    "Floor Plan",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async Task PersistFloorPlanLayoutToggleAsync(bool previousEnabled, bool newEnabled)
        {
            try
            {
                IsFloorPlanLayoutToggleBusy = true;
                _apiService.RefreshHeadersFromSettings();
                if (!await SaveAllFloorPlansToApiAsync())
                {
                    throw new InvalidOperationException("Server rejected the floor plan configuration save.");
                }

                GlobalDataService.Instance.UpdateCachedFloorPlans(FloorPlans, newEnabled, FloorPlanCustomItemTypes.ToList());
            }
            catch (Exception ex)
            {
                _suppressFloorPlanLayoutPersist = true;
                _isFloorPlanLayoutEnabled = previousEnabled;
                _suppressFloorPlanLayoutPersist = false;
                OnPropertyChanged(nameof(IsFloorPlanLayoutEnabled));
                MessageBox.Show(
                    $"Could not update \"Use floor plan for table selection\".\n\n{ex.Message}",
                    "Floor Plan",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                IsFloorPlanLayoutToggleBusy = false;
            }
        }
    }
}