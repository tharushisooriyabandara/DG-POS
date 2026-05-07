using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using POS_UI.Models;
using System.Windows;
using System.Collections.Generic;
using POS_UI.Services;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using POS_UI.Models;
using System.Drawing.Printing;
using System.Globalization;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using POS_UI.View;

namespace POS_UI.ViewModels
{
    public enum ProductSortOption
    {
        None,
        AZ,
        ZA,
        PriceLowHigh,
        PriceHighLow
    }

    public enum PaymentMethod { Card, ManualCard, Cash, COD, COT , PAY_LATER }

    public class CashierHomeViewModel : LoadingViewModelBase
    {
        private readonly ApiService _apiService = new ApiService();
        private readonly CartService _cartService = CartService.Instance;
        private readonly CardMachineApiService _cardMachineApiService = new CardMachineApiService();
        private readonly DraftStorageService _draftStorageService = new DraftStorageService();
        private CardTransactionResult _lastCardTransactionResult;
        
        // Track original items loaded from order to distinguish from newly added items
        private readonly HashSet<Guid> _originalItemIds = new HashSet<Guid>();
        // Track original quantities at load to detect quantity increases on existing items
        private readonly Dictionary<Guid, int> _originalItemQuantities = new Dictionary<Guid, int>();
        // Track whether the order was in a kitchen-progressing status at load time
        private bool _wasKitchenLockedAtLoad = false;
        // Snapshot of locally locked items (by name|unitPrice) from dine-in JSON
        private HashSet<string> _lockedLocalItemKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Cache flag to track if data has been loaded
        private bool _isDataLoadedInMemory = false;
        // Track if we need to reload data (set when leaving the page)
        private bool _needsDataReload = false;

        public ObservableCollection<string> Categories { get; set; }
        public ObservableCollection<POS_UI.Models.ProductItemModel> AllProducts { get; set; }
        public ObservableCollection<POS_UI.Models.ProductItemModel> Products { get; set; }
        public ObservableCollection<POS_UI.Models.MenuDisplayItem> MixedMenuItems { get; set; } = new ObservableCollection<POS_UI.Models.MenuDisplayItem>();
        public ObservableCollection<OrderItem> OrderItems => _cartService.OrderItems;
        
        // Menu tabs configuration
        private ObservableCollection<MenuTabModel> _menuTabs = new ObservableCollection<MenuTabModel>();
        public ObservableCollection<MenuTabModel> MenuTabs
        {
            get => _menuTabs;
            set { _menuTabs = value; OnPropertyChanged(); }
        }
        
        private MenuTabModel _selectedMenuTab;
        public MenuTabModel SelectedMenuTab
        {
            get => _selectedMenuTab;
            set
            {
                _selectedMenuTab = value;
                OnPropertyChanged();
                
                // Update IsSelected property on all tabs
                foreach (var tab in MenuTabs)
                {
                    tab.IsSelected = (tab == _selectedMenuTab);
                }
                
                FilterByMenuTab();
            }
        }
        public decimal Total => _cartService.Total;
        public decimal SubTotal => _cartService.SubTotal;
        public decimal GrandTotal => _cartService.GrandTotal;
        public ObservableCollection<TaxSummaryRow> TaxSummaryRows { get; } = new ObservableCollection<TaxSummaryRow>();
        public bool HasTaxSummary => _cartService.OrderItems.Count > 0 && TaxSummaryRows.Count > 0;
        public decimal TotalTax => _cartService.CurrentTaxResult?.TotalTax ?? 0m;
        public int ItemCount => _cartService.ItemCount;
        public string OrderType
        {
            get => _cartService.OrderType;
            set
            {
                _cartService.OrderType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimeButtonText));
                OnPropertyChanged(nameof(CanPlaceOrder));
                // Update shop fees when order type changes
                OnPropertyChanged(nameof(ShopFeeRows));
                OnPropertyChanged(nameof(HasShopFees));
                OnPropertyChanged(nameof(TotalShopFees));
                OnPropertyChanged(nameof(SubTotal));
                OnPropertyChanged(nameof(GrandTotal));
            }
        }
        public string CustomerName
        {
            get => _cartService.CustomerName;
            set { _cartService.CustomerName = value; OnPropertyChanged(); }
        }
        public string CustomerPhone
        {
            get => _cartService.CustomerPhone;
            set { _cartService.CustomerPhone = value; OnPropertyChanged(); }
        }
        public int? TableNumber
        {
            get => _cartService.TableNumber;
            set { _cartService.TableNumber = value; OnPropertyChanged(); }
        }
        public string Note
        {
            get => _cartService.Note;
            set { _cartService.Note = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasNote)); }
        }
        public bool HasNote => _cartService.HasNote;
        public decimal DiscountAmount
        {
            get => _cartService.DiscountAmount;
            set 
            { 
                _cartService.DiscountAmount = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(Discount)); 
                OnPropertyChanged(nameof(HasDiscount));
                // Recalculate coupon when discount changes, as coupon is applied to (subtotal - discount amount)
                RecalculateCoupon();
            }
        }
        public string DiscountDescription
        {
            get
            {
                // Always show just "Discount" - percentage is shown separately in UI
                return "Discount";
            }
        }
        public string CouponCode
        {
            get => _cartService.CouponCode;
            set { _cartService.CouponCode = value; OnPropertyChanged(); OnPropertyChanged(nameof(CouponDescriptionWithAmount)); OnPropertyChanged(nameof(HasCoupon)); }
        }
        public bool HasCoupon => _cartService.HasCoupon;
        public decimal CouponAmount
        {
            get => _cartService.CouponAmount;
            set { _cartService.CouponAmount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CouponDiscount)); OnPropertyChanged(nameof(SubTotal)); OnPropertyChanged(nameof(CouponDescriptionWithAmount)); OnPropertyChanged(nameof(HasCoupon)); }
        }
        public string CouponDescription
        {
            get => _cartService.CouponDescription;
            set { _cartService.CouponDescription = value; OnPropertyChanged(); OnPropertyChanged(nameof(CouponDescriptionWithAmount)); OnPropertyChanged(nameof(HasCoupon)); }
        }
        public bool CanAddNote => !HasNote;
        public bool CanAddCoupon => !HasCoupon;
        public bool HasDiscount => DiscountAmount > 0;
        private string _timeButtonText = "Now";
        public string TimeButtonText
        {
            get 
            {
                if (OrderType == "Dine In")
                {
                    if (SelectedTable != null)
                    {
                        return $"Table {SelectedTable.Name}";
                    }
                    return "Table";
                }
                
                // For Take Away and Delivery orders, calculate estimated pickup time based on PrepTime
                if ((OrderType == "Take Away" || OrderType == "Delivery") && !SelectedOrderTime.HasValue)
                {
                    var shopDetails = GlobalDataService.Instance.ShopDetails;
                    if (shopDetails?.DeliveryPlatform?.PrepTime > 0)
                    {
                        var currentTime = DateTime.Now;
                        var estimatedPickupTime = currentTime.AddMinutes(shopDetails.DeliveryPlatform.PrepTime);
                        return estimatedPickupTime.ToString("hh:mm tt");
                    }
                }
                
                return _timeButtonText;
            }
            set
            {
                _timeButtonText = value;
                OnPropertyChanged();
            }
        }
        public ICommand AddToOrderCommand { get; }
        public ICommand RemoveFromOrderCommand { get; }
        public ICommand DecreaseQuantityCommand { get; }
        public ICommand ChangeOrderTypeCommand { get; }
        public ICommand PlaceOrderCommand { get; }
        public ICommand SaveOrderCommand { get; }
        public ICommand UpdateOrderCommand { get; }
        public ICommand CancelOrderCommand { get; }
        public ICommand FinishOrderCommand { get; }

        // Dynamic primary button text for CheckoutDialog
        public string CheckoutPrimaryButtonText => POS_UI.Services.GlobalDataService.Instance.IsFinishFlow ? "Finish Order" : "Place Order";
        public ICommand ApplyDiscountCommand { get; }
        public ICommand AddNoteCommand { get; }
        public ICommand EditNoteCommand { get; }
        public ICommand RemoveNoteCommand { get; }
        public ICommand SelectCategoryCommand { get; }
        public ICommand BackToCategoriesCommand { get; }
        public ICommand SelectMenuTabCommand { get; }
        public ICommand OpenAddItemDialogCommand { get; }
        public ICommand MixedItemClickCommand { get; }
        public ICommand EditOrderItemCommand { get; }
        public string CurrentPage { get; set; }
        public ObservableCollection<TableModel> Tables { get; set; }
        private TableModel _selectedTable;
        public TableModel SelectedTable
        {
            get => _selectedTable;
            set
            {
                _selectedTable = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimeButtonText));
                OnPropertyChanged(nameof(CanPlaceOrder));
                // Persist table selection in cart for dine-in so it survives tab changes
                if (OrderType == "Dine In")
                {
                    _cartService.TableNumber = _selectedTable?.TableNumber;
                    _cartService.TableName = _selectedTable?.Name;
                }
            }
        }
        public ICommand OpenTableSelectionCommand { get; }
        public ICommand OpenCouponDialogCommand { get; }
        //public ICommand ApplyCouponCommand { get; }
        public ICommand RemoveCouponCommand { get; }
        public ICommand RemoveDiscountCommand { get; }
        public ICommand OpenDiscountDialogCommand { get; }
        public ICommand OpenDeliveryChargeDialogCommand { get; }
        public ICommand RemoveShopFeeCommand { get; }
        public ICommand AddShopFeeCommand { get; }
        public ICommand OpenSelectAddressDialogCommand { get; }
        public ICommand OpenTimePickerCommand { get; }
        public ICommand ClearPlaceSearchCommand { get; }
        private RelayCommand printCommand;
        public ICommand PrintCommand => printCommand ??= new RelayCommand(OpenCashDrawerManual);
        private RelayCommand printCartCommand;
        public ICommand PrintCartCommand => printCartCommand ??= new RelayCommand(async () => await PrintCartReceiptAsync());
        public DateTime? SelectedOrderTime
        {
            get => _cartService.SelectedOrderTime;
            set
            {
                _cartService.SelectedOrderTime = value;
                TimeButtonText = value.HasValue ? value.Value.ToString("hh:mm tt") : "Now";
                OnPropertyChanged();
            }
        }

        public ObservableCollection<POS_UI.Models.CategoryModel> CategoriesWithCount
        {
            get
            {
                var list = Categories
                    .Select(cat => new POS_UI.Models.CategoryModel
                    {
                        CategoryName = cat,
                        Quantity = cat == "All Items" ? AllProducts.Count : AllProducts.Count(p => p.Category == cat)
                    })
                    .ToList();
                return new ObservableCollection<POS_UI.Models.CategoryModel>(list);
            }
        }
        /// <summary>When the cart host already shows a dialog (split payment, checkout), errors must use the sibling overlay host or they never appear.</summary>
        private void ShowCashDrawerErrorStatusDialog(StatusDialogViewModel vm)
        {
            const string mainHost = "AddItemDialogHost";
            const string overlayHost = "NestedModifiersDialogHost";
            var dlg = new View.StatusDialog { DataContext = vm };
            if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen(mainHost))
            {
                if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen(overlayHost))
                    MaterialDesignThemes.Wpf.DialogHost.Close(overlayHost);
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, overlayHost);
            }
            else
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, mainHost);
        }

        private async void OpenCashDrawerManual()
        {
            try
            {
                // Show cash drawer access dialog first
                var accessDialog = new POS_UI.View.CashDrawerAccessDialog();
                var result = await MaterialDesignThemes.Wpf.DialogHost.Show(accessDialog, "AddItemDialogHost");
                
                // Only proceed if user successfully got access (result is user ID as int)
                if (result is int userId && userId > 0)
                {
                    // Show reason dialog after successful access
                    var reasonDialog = new POS_UI.View.CashdrawerOpenReasonDialog();
                    var reasonResult = await MaterialDesignThemes.Wpf.DialogHost.Show(reasonDialog, "AddItemDialogHost");
                    
                    // Only proceed if user entered reason and clicked Open (result is string with reason)
                    if (reasonResult is string reason && !string.IsNullOrWhiteSpace(reason))
                    {
                        // Collect all active printers; if none marked active, use all detected printers
                        var printersService = PrintersService.Instance;
                        var targetPrinters = printersService.Printers.Where(p => p.IsActive).Select(p => p.DeviceName).ToList();
                        if (targetPrinters.Count == 0)
                        {
                            targetPrinters = printersService.Printers.Select(p => p.DeviceName).ToList();
                        }

                        // ESC/POS pulse command to open cash drawer (Kick-out)
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
                            //MessageBox.Show("Failed to trigger cash drawer.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed", "Failed to trigger cash drawer.");
                            ShowCashDrawerErrorStatusDialog(vm);
                            return;
                        }

                        // Log user activity after successful drawer opening
                        // Use the user ID returned from the dialog
                        // If logged-in user is admin, the dialog returns admin's ID
                        // If logged-in user is cashier, the dialog returns selected admin's ID
                        // Use the reason entered by the user as the description
                        try
                        {
                            await _apiService.LogUserActivityAsync("open", "cash_drawer", 1, userId, reason);
                        }
                        catch
                        {
                            // Silently fail logging - don't block drawer opening if logging fails
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show("Error opening cash drawer: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Opening Cash Drawer", $"Error opening cash drawer: {ex.Message}");
                ShowCashDrawerErrorStatusDialog(vm);
            }
        }


        private async void OpenCashDrawer()
        {
            try
            {
                // Collect all active printers; if none marked active, use all detected printers
                var printersService = PrintersService.Instance;
                var targetPrinters = printersService.Printers.Where(p => p.IsActive).Select(p => p.DeviceName).ToList();
                if (targetPrinters.Count == 0)
                {
                    targetPrinters = printersService.Printers.Select(p => p.DeviceName).ToList();
                }

                // ESC/POS pulse command to open cash drawer (Kick-out)
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
                    //MessageBox.Show("Failed to trigger cash drawer.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed", "Failed to trigger cash drawer.");
                    ShowCashDrawerErrorStatusDialog(vm);
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
                //MessageBox.Show("Error opening cash drawer: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Opening Cash Drawer", $"Error opening cash drawer: {ex.Message}");
                ShowCashDrawerErrorStatusDialog(vm);
            }
        }

        private async Task PrintCartReceiptAsync()
        {
            try
            {
                if (_cartService == null || !_cartService.OrderItems.Any())
                    return;
                // Sync ViewModel's DisplayOrderId to cart so receipt shows the same order ID as the cart UI (cart only gets it on Place Order otherwise)
                _cartService.DisplayOrderId = DisplayOrderId;
                // Sync pickup time so receipt shows selected/estimated pickup time instead of order time (cart.PickupTime is otherwise only set on Place Order)
                DateTime pickupTime;
                if (SelectedOrderTime.HasValue)
                    pickupTime = SelectedOrderTime.Value;
                else if (OrderType == "Take Away" || OrderType == "Delivery")
                {
                    var shopDetails = GlobalDataService.Instance.ShopDetails;
                    if (shopDetails?.DeliveryPlatform?.PrepTime > 0)
                        pickupTime = DateTime.Now.AddMinutes(shopDetails.DeliveryPlatform.PrepTime);
                    else
                        pickupTime = DateTime.Now;
                }
                else
                    pickupTime = DateTime.Now;
                _cartService.PickupTime = pickupTime;
                await ReceiptPrintingService.Instance.PrintCartReceiptAsync(_cartService);
                await ReceiptPrintingService.Instance.PrintKitchenReceiptAsync(_cartService);
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Print Error", $"Failed to print: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
            }
        }

        private List<SplitPaymentItem> _pendingSplitPayments;
        /// <summary>Cart line IDs read-only after a charged split (temp payment). Each charge/sync locks every line currently in the cart; lines added later stay editable until the next charge.</summary>
        private readonly HashSet<Guid> _cartItemIdsLockedForChargedSplitPayments = new HashSet<Guid>();
        private string _activeSplitDialogHostId = "SplitPaymentDialogHost";
        public List<SplitPaymentItem> PendingSplitPayments 
        { 
            get => _pendingSplitPayments; 
            private set 
            { 
                _pendingSplitPayments = value;
                OnPropertyChanged(); 
            } 
        }

        private async Task OpenSplitPaymentDialogAsync(string hostId = "SplitPaymentDialogHost")
        {
            const string cartDialogHostId = "AddItemDialogHost";
            try
            {
                var splitDialogHostId = string.IsNullOrWhiteSpace(hostId) ? "SplitPaymentDialogHost" : hostId;

                // Split from CheckoutDialog: checkout lives on AddItemDialogHost; nested SplitPaymentDialogHost is inside it.
                // Close checkout first, then show split on the cart host so the split dialog is not stacked inside checkout.
                if (string.Equals(splitDialogHostId, "SplitPaymentDialogHost", StringComparison.Ordinal)
                    && MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen(cartDialogHostId))
                {
                    MaterialDesignThemes.Wpf.DialogHost.Close(cartDialogHostId, null);
                    await Task.Delay(50).ConfigureAwait(true);
                    splitDialogHostId = cartDialogHostId;
                }

                _activeSplitDialogHostId = splitDialogHostId;
                var total = PaymentDue;
                var vm = new SplitPaymentDialogViewModel(
                    total,
                    splitDialogHostId,
                    runCardPaymentAsync: RunCardPaymentForSplitAsync,
                    onCardPaymentError: (title, message) =>
                    {
                        var errVm = POS_UI.ViewModels.StatusDialogViewModel.CreateError(title, message);
                        MaterialDesignThemes.Wpf.DialogHost.Show(new View.StatusDialog { DataContext = errVm }, splitDialogHostId);
                    },
                    openCashDrawerOnSplitCashCharge: () => { try { OpenCashDrawer(); } catch { } },
                    orderDisplayOrderId: DisplayOrderId);
                await vm.LoadExistingTempPaymentsAsync();
                var view = new View.SplitPaymentDialog { DataContext = vm };
                var result = await MaterialDesignThemes.Wpf.DialogHost.Show(view, splitDialogHostId);
                try
                {
                    await ApplyChargedSplitPaymentReadOnlyFromApiIfNeededAsync().ConfigureAwait(true);
                }
                catch
                {
                    /* non-fatal */
                }

                if (result is SplitPaymentDialogResult splitResult)
                {
                    if (splitResult.Confirmed && splitResult.Payments != null && splitResult.Payments.Count > 0)
                    {
                        PendingSplitPayments = splitResult.Payments;
                        await Task.Delay(50); // let split dialog host close before checkout placement UI
                        await ConfirmOrderAsync();
                    }
                    else if (!splitResult.Confirmed && splitResult.Payments != null && splitResult.Payments.Count > 0)
                    {
                        PendingSplitPayments = null;
                        RegisterChargedSplitPaymentReadOnlyOnCurrentCartItems();
                    }
                    else
                    {
                        PendingSplitPayments = null;
                    }
                }
                else
                {
                    PendingSplitPayments = null;
                }
            }
            catch (Exception ex)
            {
                var errVm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Split Payment", $"Failed to open: {ex.Message}");
                var errHost = string.IsNullOrWhiteSpace(_activeSplitDialogHostId) ? cartDialogHostId : _activeSplitDialogHostId;
                MaterialDesignThemes.Wpf.DialogHost.Show(new View.StatusDialog { DataContext = errVm }, errHost);
            }
        }

        /// <summary>
        /// Split payment already uses <see cref="_activeSplitDialogHostId"/>; a second dialog on that host does not show.
        /// Match checkout card validation: clear overlay host if needed, then show on <c>NestedModifiersDialogHost</c>.
        /// </summary>
        private async Task ShowSplitCardMachineWarningAsync(StatusDialogViewModel errVm)
        {
            const string overlayHost = "NestedModifiersDialogHost";
            if (DialogHost.IsDialogOpen(overlayHost))
                DialogHost.Close(overlayHost);
            await Task.Delay(50).ConfigureAwait(true);
            await DialogHost.Show(new View.StatusDialog { DataContext = errVm }, overlayHost).ConfigureAwait(true);
        }

        /// <summary>Runs card terminal for a split row; returns result for storing in PaymentModel (AuthCode, CardPan, etc.).</summary>
        private async Task<CardTransactionResult> RunCardPaymentForSplitAsync(decimal amount, string reference)
        {
            var cardMachines = CardMachineService.Instance.CardMachines;
            var availableCardMachines = cardMachines.Where(cm => cm.IsActive).ToList();
            if (!availableCardMachines.Any())
            {
                var errVm = StatusDialogViewModel.CreateWarning("Card Machine Not Available", "No active card machines available. Please activate a card machine in settings or select a different payment method.");
                await ShowSplitCardMachineWarningAsync(errVm).ConfigureAwait(true);
                return new CardTransactionResult { IsSuccess = false, UserAlreadyNotifiedOfFailure = true, ErrorMessage = "No active card machines available." };
            }
            var machinesWithAuth = availableCardMachines.Where(cm => !string.IsNullOrEmpty(cm.AuthToken)).ToList();
            if (!machinesWithAuth.Any())
            {
                var errVm = StatusDialogViewModel.CreateWarning("Card Machine Not Authorized", "No authorized card machines found. Please authorize a card machine in settings.");
                await ShowSplitCardMachineWarningAsync(errVm).ConfigureAwait(true);
                return new CardTransactionResult { IsSuccess = false, UserAlreadyNotifiedOfFailure = true, ErrorMessage = "No authorized card machines found. Please authorize a card machine in settings." };
            }
            var refId = !string.IsNullOrWhiteSpace(DisplayOrderId) ? DisplayOrderId : reference;
            return await ProcessCardPaymentWithLoadingAsync(machinesWithAuth.First(), amount, refId);
        }

        private CustomerModel _selectedCustomer;
        public CustomerModel SelectedCustomer
        {
            get => _selectedCustomer;
            set 
            { 
                _selectedCustomer = value; 
                // Update CartService customer name when customer is selected
                if (value != null)
                {
                    _cartService.CustomerName = $"{value.FirstName} {value.LastName}".Trim();
                    _cartService.CustomerPhone = value.Phone;
                    // Pre-populate delivery address list/selection when Delivery
                    if (OrderType == "Delivery")
                    {
                        CustomerAddresses.Clear();
                        // Insert the sentinel first item
                        CustomerAddresses.Add(new CustomerAddressModel { Id = 0, Label = "Add New Address", AddressLine1 = string.Empty });
                        if (value.Addresses != null)
                        {
                            foreach (var a in value.Addresses)
                                CustomerAddresses.Add(a);
                        }
                        var defaultAddr = value.Addresses?.FirstOrDefault(a => a?.IsDefault == true) ?? value.Addresses?.FirstOrDefault();
                        _suppressAddressModal = true;
                        SelectedAddress = defaultAddr ?? CustomerAddresses.FirstOrDefault(); // updates DeliveryAddress
                        OnPropertyChanged(nameof(HasCustomerAddresses));
                    }

                    // Removed informational alert on customer selection as per requirement
                }
                else
                {
                    _cartService.CustomerName = null;
                    _cartService.CustomerPhone = null;
                    DeliveryAddress = null;
                    CustomerAddresses.Clear();
                    CustomerAddresses.Add(new CustomerAddressModel { Id = 0, Label = "Add New Address", AddressLine1 = string.Empty });
                    _suppressAddressModal = true;
                    SelectedAddress = CustomerAddresses.FirstOrDefault();
                    OnPropertyChanged(nameof(HasCustomerAddresses));
                }
                OnPropertyChanged(); 
            }
        }
        public ObservableCollection<CustomerModel> Customers { get; set; }
        public ICommand SelectCustomerCommand { get; }

        public ObservableCollection<DraftOrderModel> DraftOrders { get; set; } = new ObservableCollection<DraftOrderModel>();
        public int DraftCount => DraftOrders.Count;
        public ICommand OpenDraftsCommand { get; }

        private ProductSortOption _selectedSortOption = ProductSortOption.None;
        public ProductSortOption SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                if (_selectedSortOption != value)
                {
                    _selectedSortOption = value;
                    OnPropertyChanged();
                    ApplySortFilter();
                }
            }
        }

        public ICommand ClearSortCommand { get; }

        public List<ProductSortOption> SortOptions { get; } = new List<ProductSortOption>
        {
            ProductSortOption.None,
            ProductSortOption.AZ,
            ProductSortOption.ZA,
            ProductSortOption.PriceLowHigh,
            ProductSortOption.PriceHighLow
        };

        private PaymentMethod _selectedPaymentMethod = PaymentMethod.Card;
        private bool _isCashInputPrimed;
        public PaymentMethod SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set
            {
                if (_selectedPaymentMethod != value)
                {
                    _selectedPaymentMethod = value;
                    _isCashInputPrimed = value == PaymentMethod.Cash;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCashPaymentSelected));
                OnPropertyChanged(nameof(IsCODPaymentSelected));
                OnPropertyChanged(nameof(IsCOTPaymentSelected));
                OnPropertyChanged(nameof(IsCashPaymentValid));
            }
        }
        public ICommand SelectPaymentMethodCommand { get; }
        public ICommand EnableSplitPaymentCommand { get; }
        public ICommand ConfirmOrderCommand { get; }
        public ICommand ClearCashGivenCommand { get; }
        public ICommand NumberPadCommand { get; }
        //public ICommand ManualCardSelectedCommand { get; }
        
        public bool CanPlaceOrder
        {
            get
            {
                // Must have items in the order
                if (OrderItems == null || OrderItems.Count == 0)
                    return false;

                // For Dine In orders, must have a table selected
                if (OrderType == "Dine In" && SelectedTable == null)
                    return false;

                // For Delivery orders, must have a valid address selected
                if (OrderType == "Delivery")
                {
                    if (SelectedAddress == null || SelectedAddress.Id == 0 || string.IsNullOrWhiteSpace(SelectedAddress.FullAddress))
                        return false;
                }

                return true;
            }
        }

        private decimal _cashGiven;
        public decimal CashGiven
        {
            get => _cashGiven;
            set
            {
                if (_cashGiven != value)
                {
                    _cashGiven = value;
                    OnPropertyChanged(nameof(CashGiven));
                    OnPropertyChanged(nameof(CashGivenString));
                    OnPropertyChanged(nameof(CashBalance));
                    OnPropertyChanged(nameof(IsCashPaymentValid));
                }
            }
        }

        private string _cashInputString = "";
        public string CashInputString
        {
            get => _cashInputString;
            set
            {
                _cashInputString = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CashGivenString));
                
                // Update the actual cash amount if we have a valid input
                if (decimal.TryParse(value, out decimal result))
                {
                    CashGiven = result;
                }
                else
                {
                    // If parsing fails or value is empty, set to 0
                    // This ensures the button is disabled when input is invalid or empty
                    CashGiven = 0;
                }
                // Notify IsCashPaymentValid so the button state updates immediately
                OnPropertyChanged(nameof(IsCashPaymentValid));
            }
        }

        // String property for binding to TextBox
        public string CashGivenString
        {
            get => string.IsNullOrEmpty(CashInputString) ? CashGiven.ToString("F2") : CashInputString;
            set
            {
                if (decimal.TryParse(value, out decimal result))
                {
                    CashGiven = result;
                }
                OnPropertyChanged(nameof(CashGivenString));
            }
        }

        // Use SubTotal during Finish flow to avoid stale API overrides; otherwise use EffectiveTotalForPayment
        // Round to 2 decimal places to match currency precision and avoid precision issues in comparisons
        public decimal PaymentDue => Math.Round(
            POS_UI.Services.GlobalDataService.Instance.IsFinishFlow ? _cartService.SubTotal : _cartService.EffectiveTotalForPayment,
            2,
            MidpointRounding.AwayFromZero
        );

        public decimal CashBalance => CashGiven - PaymentDue;

        public bool IsCashPaymentValid => SelectedPaymentMethod == PaymentMethod.Cash && CashGiven >= PaymentDue;

        public bool IsCashPaymentSelected => SelectedPaymentMethod == PaymentMethod.Cash;
        public bool IsCODPaymentSelected => SelectedPaymentMethod == PaymentMethod.COD;
        public bool IsCOTPaymentSelected => SelectedPaymentMethod == PaymentMethod.COT;
        public bool IsDeliveryOrder => OrderType == "Delivery";
        public bool IsTakeAwayOrder => OrderType == "Take Away";
        public bool IsDineInOrder => OrderType == "Dine In";
        // Show Pay Later option only for Dine In orders when NOT in finish flow
        public bool ShowPayLaterOption => OrderType == "Dine In" && !POS_UI.Services.GlobalDataService.Instance.IsFinishFlow;
        // Keep IsDininOrder for backward compatibility (typo in XAML)
        public bool IsDininOrder => ShowPayLaterOption;
        
        // For COD orders, we don't need immediate payment validation since payment is collected later
        public bool IsCODPaymentValid => SelectedPaymentMethod == PaymentMethod.COD;
        public bool IsCOTPaymentValid => SelectedPaymentMethod == PaymentMethod.COT;

        private bool _shouldFocusCashInput;
        public bool ShouldFocusCashInput
        {
            get => _shouldFocusCashInput;
            set
            {
                _shouldFocusCashInput = value;
                OnPropertyChanged();
            }
        }

        public decimal Discount => DiscountAmount;

        public decimal DiscountPercent
        {
            get => _cartService.DiscountPercent;
            set 
            { 
                if (_cartService.DiscountPercent != value)
                {
                    _cartService.DiscountPercent = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(DiscountDescription)); 
                }
            }
        }

        public decimal CouponDiscount => CouponAmount;

        public decimal VoucherDiscount
        {
            get => _cartService.VoucherDiscount;
        }

        public bool HasVoucherDiscount => VoucherDiscount > 0;

        public decimal DeliveryCharge
        {
            get => _cartService.DeliveryCharge;
            set { _cartService.DeliveryCharge = value; OnPropertyChanged(); OnPropertyChanged(nameof(SubTotal)); }
        }

        // Shop fees display rows computed from current cart and shop settings
        public IEnumerable<ShopFeeDisplayModel> ShopFeeRows
        {
            get
            {
                try
                {
                    var rows = new List<ShopFeeDisplayModel>();
                    foreach (var fee in _cartService.GetShopFeeRowsForDisplay())
                    {
                        if (fee == null) continue;
                        var type = (fee.FeeType ?? string.Empty).Trim().ToUpperInvariant();
                        var bracket = type == "PERCENTAGE" ? $"{fee.FeeValue:0.##}%" : "value";
                        rows.Add(new ShopFeeDisplayModel
                        {
                            Name = fee.Name,
                            ShopFeeId = fee.ShopFeeId,
                            Label = $"{fee.Name}({bracket})",
                            Amount = fee.Amount,
                            IsMandatory = fee.IsMandatory,
                            IsRemoved = fee.IsRemoved,
                            RemoveCommand = RemoveShopFeeCommand,
                            AddCommand = AddShopFeeCommand
                        });
                    }
                    return rows;
                }
                catch { return Enumerable.Empty<ShopFeeDisplayModel>(); }
            }
        }

        public bool HasShopFees => OrderItems.Count > 0 && ShopFeeRows.Any();
        public decimal TotalShopFees => _cartService.GetCalculatedShopFees().Sum(f => f.Amount);

        // Delivery address binding
        public string DeliveryAddress
        {
            get => _cartService.DeliveryAddress;
            set { _cartService.DeliveryAddress = value; OnPropertyChanged(); }
        }

        // Fields used by add-address dialog (kept blank by default)
        public string NewAddressLabel { get; set; }
        public string NewAddressHouseNo { get; set; }
        private string _newAddressLatitude;
        private string _newAddressLongitude;
        private bool _isDefaultAddress;
        public bool IsDefaultAddress
        {
            get => _isDefaultAddress;
            set
            {
                _isDefaultAddress = value;
                OnPropertyChanged();
            }
        }
        private string _placeSearchText;
        private bool _suppressPredictionFetch;
        public string PlaceSearchText
        {
            get => _placeSearchText;
            set
            {
                if (_placeSearchText == value) return;
                _placeSearchText = value;
                OnPropertyChanged();
                if (!_suppressPredictionFetch)
                {
                    _ = FetchPlacePredictionsAsync(_placeSearchText);
                }
            }
        }
        public ObservableCollection<string> PlacePredictions { get; } = new ObservableCollection<string>();
        public bool ArePredictionsVisible => PlacePredictions.Count > 0;
        private string _selectedPrediction;
        public string SelectedPrediction
        {
            get => _selectedPrediction;
            set
            {
                if (_selectedPrediction == value) return;
                _selectedPrediction = value;
                OnPropertyChanged();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    // Update text box to selected suggestion without triggering new fetch
                    _suppressPredictionFetch = true;
                    PlaceSearchText = value;
                    _suppressPredictionFetch = false;
                    _ = ResolveSelectedPlaceAsync(value);
                    // Hide predictions after selection
                    PlacePredictions.Clear();
                    OnPropertyChanged(nameof(ArePredictionsVisible));
                }
            }
        }
        public string DistanceText { get; set; }

        private async Task FetchPlacePredictionsAsync(string input)
        {
            try
            {
                PlacePredictions.Clear();
                if (string.IsNullOrWhiteSpace(input) || input.Trim().Length < 3)
                {
                    OnPropertyChanged(nameof(ArePredictionsVisible));
                    return;
                }
                var country = GlobalDataService.Instance.ShopDetails?.CountryCode ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(country) && country.Length > 2)
                {
                    // Normalize to ISO 3166-1 alpha-2 if a longer code like "LK-Sri Lanka" is provided
                    var dash = country.IndexOf('-');
                    country = dash > 0 ? country.Substring(0, dash) : country;
                }
                System.Diagnostics.Debug.WriteLine($"[Places] Fetch predictions - CountryCode='{country}', Input='{input}'");
                var list = await _apiService.GoogleGetPredictionsAsync(input.Trim(), country);
                foreach (var p in list)
                {
                    PlacePredictions.Add(p);
                }
                OnPropertyChanged(nameof(ArePredictionsVisible));
            }
            catch
            {
                // swallow prediction errors
                OnPropertyChanged(nameof(ArePredictionsVisible));
            }
        }

        private async Task ResolveSelectedPlaceAsync(string description)
        {
            try
            {
                var country = GlobalDataService.Instance.ShopDetails?.CountryCode ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(country) && country.Length > 2)
                {
                    var dash = country.IndexOf('-');
                    country = dash > 0 ? country.Substring(0, dash) : country;
                }
                var countryIso = country?.Trim().ToUpperInvariant();
                var useImperialUnits = !string.IsNullOrWhiteSpace(countryIso) && countryIso != "LK";
                var distanceUnits = useImperialUnits ? "imperial" : "metric";
                System.Diagnostics.Debug.WriteLine($"[Places] Resolve selection - CountryCode='{country}', Description='{description}'");
                var (placeId, lat, lng, formatted) = await _apiService.GoogleResolvePlaceAsync(description, country);
                if (!string.IsNullOrWhiteSpace(formatted))
                {
                    DeliveryAddress = formatted;
                }
                // Store coordinates for backend payload
                _newAddressLatitude = lat != 0 ? lat.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
                _newAddressLongitude = lng != 0 ? lng.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
                // In modal: always compute and show distance for the searched address, independent of order type
                if (_isAddressDialogOpen)
                {
                    var shop = GlobalDataService.Instance.ShopDetails;
                    if (shop != null && double.TryParse(shop.Latitude, out var shopLat) && double.TryParse(shop.Longitude, out var shopLng))
                    {
                        var text = await _apiService.GoogleGetDistanceTextAsync((shopLat, shopLng), (lat, lng), units: distanceUnits);
                        var compactFallback = useImperialUnits ? "0.0mi" : "0.0km";
                        var compact = string.IsNullOrWhiteSpace(text) ? compactFallback : text.Replace(" ", "");
                        ModalDistanceText = compact;
                        OnPropertyChanged(nameof(ModalDistanceText));
                    }
                }
                else if (OrderType == "Delivery")
                {
                    // On cart/delivery tab, keep existing behavior (show standard distance text with space)
                    var shop = GlobalDataService.Instance.ShopDetails;
                    if (shop != null && double.TryParse(shop.Latitude, out var shopLat) && double.TryParse(shop.Longitude, out var shopLng))
                    {
                        var text = await _apiService.GoogleGetDistanceTextAsync((shopLat, shopLng), (lat, lng), units: distanceUnits);
                        DistanceText = string.IsNullOrWhiteSpace(text) ? (useImperialUnits ? "0.0 mi" : "0.0 km") : text;
                        OnPropertyChanged(nameof(DistanceText));
                    }
                }
            }
            catch
            {
                // ignore for now
            }
        }

        // Customer addresses for Delivery orders
        public ObservableCollection<CustomerAddressModel> CustomerAddresses { get; } = new ObservableCollection<CustomerAddressModel>();
        private CustomerAddressModel _selectedAddress;
        private bool _suppressAddressModal;
        private bool _isAddressDialogOpen;
        private string _prevAddressLabel;
        private string _prevAddressHouseNo;
        private string _prevDeliveryAddress;
        public string ModalDistanceText { get; set; }
        public CustomerAddressModel SelectedAddress
        {
            get => _selectedAddress;
            set
            {
                // Do not auto-open modal from setter; modal is opened only via UI click handler
                if (_suppressAddressModal)
                {
                    _suppressAddressModal = false;
                }
                _selectedAddress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedAddressLabel));
                OnPropertyChanged(nameof(CanPlaceOrder));
                DeliveryAddress = value?.FullAddress;
                if (OrderType == "Delivery")
                {
                    _ = ComputeAndSetDistanceForSelectedAddressAsync();
                }
            }
        }
        public bool HasCustomerAddresses => true;
        public string SelectedAddressLabel => SelectedAddress?.Label ?? "Select Address";

        // Control dropdown open state to suppress opening when choosing sentinel
        private bool _isAddressDropdownOpen;
        public bool IsAddressDropdownOpen
        {
            get => _isAddressDropdownOpen;
            set { _isAddressDropdownOpen = value; OnPropertyChanged(); }
        }

        public string CouponDescriptionWithAmount
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CouponCode) && decimal.TryParse(CouponCode, out var percent) && percent > 0 && percent <= 100)
                {
                    return $"Coupon ({percent}%)";
                }
                else if (!string.IsNullOrWhiteSpace(CouponDescription))
                {
                    return CouponDescription;
                }
                else
                {
                    return "Coupon";
                }
            }
        }

        //Alert Notification
        private bool _isOrderAlertVisible;
        public bool IsOrderAlertVisible
        {
            get => _isOrderAlertVisible;
            set { _isOrderAlertVisible = value; OnPropertyChanged(); }
        }

        private string _pendingOrderData;
        public string PendingOrderData
        {
            get => _pendingOrderData;
            set
            {
                _pendingOrderData = value;
                OnPropertyChanged();
            }
        }

        /// <summary>CREATED orders waiting for a new-order alert after the current alert is cleared.</summary>
        private readonly Queue<string> _pendingNewOrderAlertQueue = new Queue<string>();
        private readonly HashSet<string> _queuedNewOrderAlertDisplayIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static bool TryParseDisplayOrderIdFromIncomingJson(string orderJson, out string displayId)
        {
            displayId = null;
            if (string.IsNullOrEmpty(orderJson)) return false;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(orderJson);
                if (doc.RootElement.TryGetProperty("display_order_id", out var disp) && disp.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    displayId = disp.GetString();
                    return !string.IsNullOrWhiteSpace(displayId);
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// True if this CREATED order is already on the incoming row (full persistent list).
        /// <see cref="IncomingOrdersBanner"/> only holds the first few chips for UI; do not use it alone for alert de-dupe.
        /// </summary>
        private static bool IsDisplayOrderIdAlreadySurfacedIncoming(string displayOrderId)
        {
            if (string.IsNullOrWhiteSpace(displayOrderId)) return false;
            try
            {
                return GlobalDataService.Instance.GetPersistentIncomingOrderBanners().Any(b =>
                    b != null && string.Equals(b.DisplayOrderId, displayOrderId, StringComparison.OrdinalIgnoreCase));
            }
            catch { return false; }
        }

        /// <summary>
        /// Called when CREATED orders are fetched. Enqueues every order not in the banner and not already
        /// shown as the current popup, then shows the next alert when nothing else is blocking.
        /// </summary>
        public void SyncIncomingOrderAlertsFromApi(IEnumerable<string> createdOrderJsonInApiOrder)
        {
            if (createdOrderJsonInApiOrder == null) return;
            PruneNewOrderAlertQueueAgainstBanner();
            string currentPopupDisplayId = null;
            if (IsOrderAlertVisible && !string.IsNullOrEmpty(PendingOrderData))
                TryParseDisplayOrderIdFromIncomingJson(PendingOrderData, out currentPopupDisplayId);

            foreach (var json in createdOrderJsonInApiOrder)
            {
                if (!TryParseDisplayOrderIdFromIncomingJson(json, out var disp) || string.IsNullOrWhiteSpace(disp))
                    continue;
                if (IsDisplayOrderIdAlreadySurfacedIncoming(disp))
                    continue;
                if (!string.IsNullOrEmpty(currentPopupDisplayId) && string.Equals(disp, currentPopupDisplayId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (_queuedNewOrderAlertDisplayIds.Contains(disp))
                    continue;
                _pendingNewOrderAlertQueue.Enqueue(json);
                _queuedNewOrderAlertDisplayIds.Add(disp);
            }

            TryShowNextNewOrderAlertFromQueue();
            try { GlobalDataService.Instance.RefreshCashierIncomingOrdersBadgeCount(); } catch { /* ignore */ }
        }

        private void PruneNewOrderAlertQueueAgainstBanner()
        {
            if (_pendingNewOrderAlertQueue.Count == 0) return;
            var snapshot = _pendingNewOrderAlertQueue.ToArray();
            _pendingNewOrderAlertQueue.Clear();
            _queuedNewOrderAlertDisplayIds.Clear();
            foreach (var json in snapshot)
            {
                if (!TryParseDisplayOrderIdFromIncomingJson(json, out var disp) || string.IsNullOrWhiteSpace(disp))
                    continue;
                if (IsDisplayOrderIdAlreadySurfacedIncoming(disp))
                    continue;
                _pendingNewOrderAlertQueue.Enqueue(json);
                _queuedNewOrderAlertDisplayIds.Add(disp);
            }
        }

        /// <summary>
        /// Incoming orders not yet on the banner: the alert popup (if visible) plus orders still waiting in the queue.
        /// Used so the sidebar badge matches when multiple CREATED orders arrive at once.
        /// </summary>
        public int GetCashierIncomingOrdersPendingSurfaceCount()
        {
            int n = _pendingNewOrderAlertQueue.Count;
            if (IsOrderAlertVisible && !string.IsNullOrEmpty(PendingOrderData))
            {
                if (TryParseDisplayOrderIdFromIncomingJson(PendingOrderData, out var disp) &&
                    !string.IsNullOrWhiteSpace(disp) &&
                    !IsDisplayOrderIdAlreadySurfacedIncoming(disp))
                    n += 1;
            }
            return n;
        }

        /// <summary>Shows the next queued new-order alert when the current one is dismissed and no modal is open.</summary>
        public void TryShowNextNewOrderAlertFromQueue()
        {
            if (IsOrderAlertVisible) return;
            try
            {
                if (DialogHost.IsDialogOpen("AddItemDialogHost"))
                    return;
            }
            catch { }

            while (_pendingNewOrderAlertQueue.Count > 0)
            {
                var json = _pendingNewOrderAlertQueue.Dequeue();
                if (!TryParseDisplayOrderIdFromIncomingJson(json, out var disp) || string.IsNullOrWhiteSpace(disp))
                    continue;
                _queuedNewOrderAlertDisplayIds.Remove(disp);
                if (IsDisplayOrderIdAlreadySurfacedIncoming(disp))
                    continue;
                PendingOrderData = json;
                IsOrderAlertVisible = false;
                IsOrderAlertVisible = true;
                GlobalDataService.Instance.NotifyNewOrderAlertShowing(PendingOrderData);
                return;
            }
        }

        /// <summary>
        /// User closed/tapped the new-order alert: move the visible order and any other queued CREATED orders
        /// into the incoming banner row (beside Drafts) in one step.
        /// </summary>
        public void DismissNewOrderAlertAndSurfaceAllPendingToBanner()
        {
            IsOrderAlertVisible = false;

            if (!string.IsNullOrEmpty(PendingOrderData))
                AddIncomingOrderFromJson(PendingOrderData);
            PendingOrderData = null;

            while (_pendingNewOrderAlertQueue.Count > 0)
            {
                var json = _pendingNewOrderAlertQueue.Dequeue();
                if (TryParseDisplayOrderIdFromIncomingJson(json, out var disp) && !string.IsNullOrWhiteSpace(disp))
                    _queuedNewOrderAlertDisplayIds.Remove(disp);
                AddIncomingOrderFromJson(json);
            }

            try { GlobalDataService.Instance.ClearNewOrderAlertPopupTracking(); } catch { /* ignore */ }
        }

        private async Task ComputeAndSetDistanceForSelectedAddressAsync(bool forModal = false)
        {
            try
            {
                var shop = GlobalDataService.Instance.ShopDetails;
                // Determine units based on shop country (use miles when not LK)
                var ccRaw = GlobalDataService.Instance.ShopDetails?.CountryCode ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(ccRaw) && ccRaw.Length > 2)
                {
                    var dashCc = ccRaw.IndexOf('-');
                    ccRaw = dashCc > 0 ? ccRaw.Substring(0, dashCc) : ccRaw;
                }
                var ccIso = ccRaw?.Trim().ToUpperInvariant();
                var useImperialUnits = !string.IsNullOrWhiteSpace(ccIso) && ccIso != "LK";
                var distanceUnits = useImperialUnits ? "imperial" : "metric";
                if (shop == null || !double.TryParse(shop.Latitude, out var shopLat) || !double.TryParse(shop.Longitude, out var shopLng))
                {
                    if (forModal)
                    {
                        ModalDistanceText = string.Empty;
                        OnPropertyChanged(nameof(ModalDistanceText));
                    }
                    else
                    {
                        DistanceText = useImperialUnits ? "0.0 mi" : "0.0 km";
                        OnPropertyChanged(nameof(DistanceText));
                    }
                    return;
                }

                double addrLat = 0, addrLng = 0;
                bool haveCoords = false;
                if (!string.IsNullOrWhiteSpace(SelectedAddress?.Latitude) && !string.IsNullOrWhiteSpace(SelectedAddress?.Longitude))
                {
                    haveCoords = double.TryParse(SelectedAddress.Latitude, out addrLat) && double.TryParse(SelectedAddress.Longitude, out addrLng);
                }
                if (!haveCoords && !string.IsNullOrWhiteSpace(DeliveryAddress))
                {
                    var country = GlobalDataService.Instance.ShopDetails?.CountryCode ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(country) && country.Length > 2)
                    {
                        var dash = country.IndexOf('-');
                        country = dash > 0 ? country.Substring(0, dash) : country;
                    }
                    var (_, lat, lng, _) = await _apiService.GoogleResolvePlaceAsync(DeliveryAddress, country);
                    addrLat = lat;
                    addrLng = lng;
                    haveCoords = (lat != 0 || lng != 0);
                }

                string text = null;
                if (haveCoords)
                {
                    text = await _apiService.GoogleGetDistanceTextAsync((shopLat, shopLng), (addrLat, addrLng), units: distanceUnits);
                }

                if (forModal)
                {
                    ModalDistanceText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Replace(" ", "");
                    OnPropertyChanged(nameof(ModalDistanceText));
                }
                else
                {
                    DistanceText = string.IsNullOrWhiteSpace(text) ? (useImperialUnits ? "0.0 mi" : "0.0 km") : text;
                    OnPropertyChanged(nameof(DistanceText));
                }
            }
            catch
            {
                if (forModal)
                {
                    ModalDistanceText = string.Empty;
                    OnPropertyChanged(nameof(ModalDistanceText));
                }
                else
                {
                    // Fallback to evaluating country again to choose units
                    var cc = GlobalDataService.Instance.ShopDetails?.CountryCode?.Trim().ToUpperInvariant();
                    var useImperialUnitsFallback = !string.IsNullOrWhiteSpace(cc) && cc != "LK";
                    DistanceText = useImperialUnitsFallback ? "0.0 mi" : "0.0 km";
                    OnPropertyChanged(nameof(DistanceText));
                }
            }
        }

        private ICommand _showOrderAlertCommand;
        public ICommand ShowOrderAlertCommand => _showOrderAlertCommand ??= new RelayCommand(ShowOrderAlert);


        private void ShowOrderAlert()
        {
            // Only show the alert if there are actually incoming orders
            var globalDataService = GlobalDataService.Instance;
            var incomingOrdersCount = globalDataService.CurrentIncomingOrdersCount;
            
            if (incomingOrdersCount > 0)
            {
                IsOrderAlertVisible = true;
            }
            // If count is 0, do nothing - don't show the modal
        }

        private ICommand _viewOrderCommand;
        public ICommand ViewOrderCommand => _viewOrderCommand ??= new RelayCommand(ViewOrder);
        private void ViewOrder()
        {
            // Hide the popup and show the order details dialog
            IsOrderAlertVisible = false;
            
            // Show the order details dialog with the pending order data
            if (!string.IsNullOrEmpty(PendingOrderData))
            {
                var globalDataService = GlobalDataService.Instance;
                globalDataService.ShowOrderDetailsDialog(PendingOrderData);
            }
        }

        // Incoming order banner (shown after closing the alert)
        private bool _isIncomingOrderBannerVisible;
        public bool IsIncomingOrderBannerVisible
        {
            get => _isIncomingOrderBannerVisible;
            set { _isIncomingOrderBannerVisible = value; OnPropertyChanged(); }
        }

        private string _incomingOrderDisplayId;
        public string IncomingOrderDisplayId
        {
            get => _incomingOrderDisplayId;
            set { _incomingOrderDisplayId = value; OnPropertyChanged(); }
        }

        private ICommand _closeIncomingBannerCommand;
        public ICommand CloseIncomingBannerCommand => _closeIncomingBannerCommand ??= new RelayCommand(() => IsIncomingOrderBannerVisible = false);

        // Use GlobalDataService's IncomingOrderBannerItem class
        private System.Collections.ObjectModel.ObservableCollection<GlobalDataService.IncomingOrderBannerItem> _incomingOrdersBanner = new System.Collections.ObjectModel.ObservableCollection<GlobalDataService.IncomingOrderBannerItem>();
        public System.Collections.ObjectModel.ObservableCollection<GlobalDataService.IncomingOrderBannerItem> IncomingOrdersBanner
        {
            get => _incomingOrdersBanner;
            set { _incomingOrdersBanner = value; OnPropertyChanged(); }
        }

        // Properties for "+N" display logic
        private bool _showRemainingOrdersIndicator;
        public bool ShowRemainingOrdersIndicator
        {
            get => _showRemainingOrdersIndicator;
            set { _showRemainingOrdersIndicator = value; OnPropertyChanged(); }
        }

        private int _remainingOrdersCount;
        public int RemainingOrdersCount
        {
            get => _remainingOrdersCount;
            set { _remainingOrdersCount = value; OnPropertyChanged(); }
        }

        // Loading indicator for incoming orders refresh
        private bool _isRefreshingIncomingOrders;
        public bool IsRefreshingIncomingOrders
        {
            get => _isRefreshingIncomingOrders;
            set { _isRefreshingIncomingOrders = value; OnPropertyChanged(); }
        }

        private ICommand _viewIncomingOrderCommand;
        public ICommand ViewIncomingOrderCommand => _viewIncomingOrderCommand ??= new RelayCommand<object>(param =>
        {
            if (param is GlobalDataService.IncomingOrderBannerItem item && !string.IsNullOrEmpty(item.OrderJson))
            {
                // Stop incoming-order sound only for the clicked order id
                try { GlobalDataService.Instance.RequestStopIncomingOrderSound(); } catch { }
                var globalDataService = GlobalDataService.Instance;
                globalDataService.ShowOrderDetailsDialog(item.OrderJson);
            }
        });

        public void AddIncomingOrderFromJson(string orderJson)
        {
            try
            {
                if (string.IsNullOrEmpty(orderJson)) return;
                
                // Add to persistent storage in GlobalDataService
                var globalDataService = GlobalDataService.Instance;
                globalDataService.AddIncomingOrderToPersistentBanner(orderJson);
                
                // Refresh the observable collection from persistent storage
                RefreshIncomingOrdersBannerFromPersistentStorage();
            }
            catch { /* ignore parse errors */ }
        }

        public void RefreshIncomingOrdersBannerFromPersistentStorage()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("RefreshIncomingOrdersBannerFromPersistentStorage: Starting refresh");
                var globalDataService = GlobalDataService.Instance;
                var persistentBanners = globalDataService.GetPersistentIncomingOrderBanners();
                
                System.Diagnostics.Debug.WriteLine($"RefreshIncomingOrdersBannerFromPersistentStorage: Found {persistentBanners.Count} persistent banners");
                
                // Clear and repopulate the observable collection
                IncomingOrdersBanner.Clear();
                
                // Implement "+N" display logic: show max 3 banners, then "+N" for remaining
                const int maxVisibleBanners = 3;
                var bannersToShow = persistentBanners.Take(maxVisibleBanners).ToList();
                var remainingCount = persistentBanners.Count - maxVisibleBanners;
                
                foreach (var banner in bannersToShow)
                {
                    IncomingOrdersBanner.Add(banner);
                }
                
                // Set "+N" indicator properties
                ShowRemainingOrdersIndicator = remainingCount > 0;
                RemainingOrdersCount = remainingCount;
                
                // Ensure banner visible
                IsIncomingOrderBannerVisible = IncomingOrdersBanner.Count > 0;
                
                System.Diagnostics.Debug.WriteLine($"RefreshIncomingOrdersBannerFromPersistentStorage: Set IsIncomingOrderBannerVisible to {IsIncomingOrderBannerVisible}");
                
                // Keep single-id binding in sync for current UI
                if (IncomingOrdersBanner.Count > 0)
                {
                    IncomingOrderDisplayId = IncomingOrdersBanner[0].DisplayOrderId;
                    PendingOrderData = IncomingOrdersBanner[0].OrderJson;
                    System.Diagnostics.Debug.WriteLine($"RefreshIncomingOrdersBannerFromPersistentStorage: Set IncomingOrderDisplayId to {IncomingOrderDisplayId}");
                }
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"RefreshIncomingOrdersBannerFromPersistentStorage: Exception caught: {ex.Message}");
                /* ignore errors */ 
            }
        }

        /// <summary>
        /// Refreshes incoming orders from API with loading indicator
        /// This method is called when returning to cashier page to ensure data consistency
        /// </summary>
        public async Task RefreshIncomingOrdersFromApiAsync()
        {
            try
            {
                IsRefreshingIncomingOrders = true;
                System.Diagnostics.Debug.WriteLine("CashierHomeViewModel: Starting API refresh for incoming orders");
                
                var globalDataService = GlobalDataService.Instance;
                await globalDataService.RefreshIncomingOrdersFromApiAsync();
                
                // Refresh the UI after API call
                RefreshIncomingOrdersBannerFromPersistentStorage();
                
                System.Diagnostics.Debug.WriteLine("CashierHomeViewModel: API refresh completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CashierHomeViewModel: API refresh failed: {ex.Message}");
                // Don't show error to user - this is a background refresh
            }
            finally
            {
                IsRefreshingIncomingOrders = false;
            }
        }

        public void RemoveIncomingOrderByDisplayId(string displayOrderId)
        {
            try
            {
                if (string.IsNullOrEmpty(displayOrderId))
                {
                    System.Diagnostics.Debug.WriteLine("RemoveIncomingOrderByDisplayId: displayOrderId is null or empty");
                    return;
                }

                // Remove from persistent storage
                var globalDataService = GlobalDataService.Instance;
                globalDataService.RemoveIncomingOrderFromPersistentBanner(displayOrderId);
                
                // Refresh the observable collection from persistent storage
                RefreshIncomingOrdersBannerFromPersistentStorage();
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"Error removing incoming order banner: {ex.Message}");
            }
        }

        // Open a simple address selection/entry dialog
        private async void OpenSelectAddressDialog()
        {
            // Immediate debug dump of current shop details (no awaits)
            var sd = GlobalDataService.Instance.ShopDetails;
            System.Diagnostics.Debug.WriteLine($"[Shop][DialogOpen] ShopDetails: {(sd==null?"<null>":$"Id={sd.Id}, Name={sd.Name}, CountryCode={sd.CountryCode}, Lat={sd.Latitude}, Lng={sd.Longitude}")}");

            // Snapshot current values to allow full revert on cancel/close
            _prevAddressLabel = SelectedAddress?.Label;
            _prevAddressHouseNo = SelectedAddress?.HouseNo;
            _prevDeliveryAddress = DeliveryAddress;
            _isAddressDialogOpen = true;

            // Clear temporary new-address fields
            NewAddressLabel = string.Empty;
            NewAddressHouseNo = string.Empty;
            _newAddressLatitude = null;
            _newAddressLongitude = null;
            IsDefaultAddress = false;
            PlaceSearchText = string.Empty;
            PlacePredictions.Clear();
            ModalDistanceText = (!string.IsNullOrWhiteSpace(GlobalDataService.Instance.ShopDetails?.CountryCode) && GlobalDataService.Instance.ShopDetails.CountryCode.Trim().ToUpperInvariant() != "LK") ? "0.0 mi" : "0.0 km";
            // Ensure the dialog starts with an empty address input so existing address isn't reused
            DeliveryAddress = string.Empty;
            OnPropertyChanged(nameof(NewAddressLabel));
            OnPropertyChanged(nameof(NewAddressHouseNo));
            OnPropertyChanged(nameof(IsDefaultAddress));
            OnPropertyChanged(nameof(PlaceSearchText));
            OnPropertyChanged(nameof(ArePredictionsVisible));
            OnPropertyChanged(nameof(ModalDistanceText));
            OnPropertyChanged(nameof(DeliveryAddress));

            // Pre-calc distance for currently selected address when Delivery tab is active
            try
            {
                if (OrderType == "Delivery")
                {
                    await ComputeAndSetDistanceForSelectedAddressAsync(forModal:true);
                }
            }
            catch { /* ignore distance pre-calc errors */ }

            var dlg = new POS_UI.View.SelectAddressDialog { DataContext = this };
            var dialogResult = await MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");

            // Only commit address when user clicks Use Address
            if (dialogResult is string result && result == "use")
            {
                // Validation loop: keep showing warning and reopening dialog until validation passes
                while (string.IsNullOrWhiteSpace(NewAddressLabel) || string.IsNullOrWhiteSpace(DeliveryAddress))
                {
                    // Ensure the address dialog is fully closed before showing warning
                    try
                    {
                        if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("AddItemDialogHost"))
                        {
                            MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost");
                            await Task.Delay(100); // Wait for dialog to close
                        }
                    }
                    catch { /* Ignore if already closed */ }

                    // Determine specific warning message based on which fields are empty
                    string warningMessage;
                    string warningTitle;
                    bool isLabelEmpty = string.IsNullOrWhiteSpace(NewAddressLabel);
                    bool isPlaceEmpty = string.IsNullOrWhiteSpace(DeliveryAddress);
                    
                    if (isLabelEmpty && isPlaceEmpty)
                    {
                        warningMessage = "Please enter label and place details before confirming.";
                        warningTitle = "Address Required";
                    }
                    else if (isLabelEmpty)
                    {
                        warningMessage = "Please enter label details before confirming.";
                        warningTitle = "Label Required";
                    }
                    else // isPlaceEmpty
                    {
                        warningMessage = "Please enter place details before confirming.";
                        warningTitle = "Place Required";
                    }

                    // Show warning dialog with specific message
                    var vmEmpty = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning(warningTitle, warningMessage);
                    var dlgEmpty = new POS_UI.View.StatusDialog { DataContext = vmEmpty };
                    await MaterialDesignThemes.Wpf.DialogHost.Show(dlgEmpty, "AddItemDialogHost");
                    
                    // Reopen the address dialog so user can fix the issue (preserve current input)
                    _isAddressDialogOpen = true;
                    var dlgReopen = new POS_UI.View.SelectAddressDialog { DataContext = this };
                    var reopenResult = await MaterialDesignThemes.Wpf.DialogHost.Show(dlgReopen, "AddItemDialogHost");
                    
                    // If user cancelled, exit
                    if (!(reopenResult is string reopenResultStr && reopenResultStr == "use"))
                    {
                        _isAddressDialogOpen = false;
                        return; // User cancelled
                    }
                    
                    // If user confirmed, loop will check validation again
                    // If valid, loop exits and continues with normal processing below
                }
                
                // Validation passed, reset flag before continuing
                _isAddressDialogOpen = false;
                
                // If user typed a new label or house no, create a temp address entry
                if (!string.IsNullOrWhiteSpace(NewAddressLabel) || !string.IsNullOrWhiteSpace(DeliveryAddress))
                {
                    var temp = new CustomerAddressModel
                    {
                        Id = -1,
                        Label = string.IsNullOrWhiteSpace(NewAddressLabel) ? DeliveryAddress : NewAddressLabel,
                        HouseNo = NewAddressHouseNo,
                        AddressLine1 = DeliveryAddress,
                        Latitude = _newAddressLatitude,
                        Longitude = _newAddressLongitude,
                        IsDefault = IsDefaultAddress
                    };
                    if (!CustomerAddresses.Any(a => a.Label == temp.Label))
                        CustomerAddresses.Insert(Math.Min(1, CustomerAddresses.Count), temp);
                    _suppressAddressModal = true;
                    SelectedAddress = temp;

                    // Commit distance to cart display after user confirms
                    try
                    {
                        if (OrderType == "Delivery")
                        {
                            DistanceText = string.IsNullOrWhiteSpace(ModalDistanceText)
                                ? ((!string.IsNullOrWhiteSpace(GlobalDataService.Instance.ShopDetails?.CountryCode) && GlobalDataService.Instance.ShopDetails.CountryCode.Trim().ToUpperInvariant() != "LK") ? "0.0 mi" : "0.0 km")
                                : ModalDistanceText;
                            OnPropertyChanged(nameof(DistanceText));
                        }
                    }
                    catch { }

                    // Persist to backend
                    try
                    {
                        if (SelectedCustomer == null)
                        {
                            //System.Windows.MessageBox.Show("Please select a customer before saving the address.", "Address", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                            var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Customer Required", "Please select a customer before saving the address.");
                            var dlg1 = new POS_UI.View.StatusDialog { DataContext = vm };
                            MaterialDesignThemes.Wpf.DialogHost.Show(dlg1, "AddItemDialogHost");
                        }
                        else
                        {
                            // Resolve missing id edge-case immediately after customer creation
                            if (SelectedCustomer.CustomerId <= 0)
                            {
                                try
                                {
                                    var allCustomers = await _apiService.GetCustomersAsync();
                                    var match = allCustomers.FirstOrDefault(c =>
                                        (!string.IsNullOrWhiteSpace(c.Phone) && !string.IsNullOrWhiteSpace(SelectedCustomer.Phone) && c.Phone.Trim() == SelectedCustomer.Phone.Trim()) ||
                                        (!string.IsNullOrWhiteSpace(c.FullPhoneNumber) && !string.IsNullOrWhiteSpace(SelectedCustomer.FullPhoneNumber) && c.FullPhoneNumber.Trim() == SelectedCustomer.FullPhoneNumber.Trim()));
                                    if (match != null)
                                    {
                                        SelectedCustomer.CustomerId = match.CustomerId;
                                    }
                                }
                                catch { /* best-effort */ }
                                if (SelectedCustomer.CustomerId <= 0)
                                {
                                    var vmNoId = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Customer Not Synced", "Please reselect the newly created customer from the list before adding an address.");
                                    var dlgNoId = new POS_UI.View.StatusDialog { DataContext = vmNoId };
                                    await MaterialDesignThemes.Wpf.DialogHost.Show(dlgNoId, "AddItemDialogHost");
                                    return;
                                }
                            }
                            await _apiService.CreateCustomerAddressAsync(SelectedCustomer.CustomerId, temp);
                            // Refresh customer list to get server-side id and addresses
                            var customers = await _apiService.GetCustomersAsync();
                            var updated = customers.FirstOrDefault(c => c.CustomerId == SelectedCustomer.CustomerId);
                            if (updated != null)
                            {
                                SelectedCustomer = updated;
                                // Rebuild dropdown items for Delivery
                                if (OrderType == "Delivery")
                                {
                                    CustomerAddresses.Clear();
                                    CustomerAddresses.Add(new CustomerAddressModel { Id = 0, Label = "Add New Address", AddressLine1 = string.Empty });
                                    foreach (var a in updated.Addresses)
                                    {
                                        CustomerAddresses.Add(a);
                                    }
                                    var def = updated.Addresses.FirstOrDefault(a => a.IsDefault) ?? updated.Addresses.FirstOrDefault();
                                    _suppressAddressModal = true;
                                    SelectedAddress = def ?? temp;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //System.Windows.MessageBox.Show($"Failed to save address: {ex.Message}", "Address", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed", $"Failed to save address: {ex.Message}");
                        var dlg1 = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg1, "AddItemDialogHost");
                    }
                }
            }
            else
            {
                // Cancelled: revert any temporary text from dialog if it changed
                // Reset to currently selected saved address, if any
                if (SelectedAddress != null && SelectedAddress.Id > 0)
                {
                    // restore previous label/house
                    if (_prevAddressLabel != null) SelectedAddress.Label = _prevAddressLabel;
                    if (_prevAddressHouseNo != null) SelectedAddress.HouseNo = _prevAddressHouseNo;
                    DeliveryAddress = _prevDeliveryAddress ?? SelectedAddress.FullAddress;
                }
                else
                {
                    DeliveryAddress = _prevDeliveryAddress;
                }
            }
            _isAddressDialogOpen = false;
        }

        private string _displayOrderId;
        public string DisplayOrderId
        {
            get => _displayOrderId;
            set
            {
                _displayOrderId = value;
                if (!string.IsNullOrWhiteSpace(value))
                    _cartService.CashierSessionDisplayOrderId = value;
                OnPropertyChanged(nameof(DisplayOrderId));
            }
        }

        private bool _isOrderLoadedForEdit;
        public bool IsOrderLoadedForEdit
        {
            get => _isOrderLoadedForEdit;
            set 
            { 
                _isOrderLoadedForEdit = value; 
                OnPropertyChanged(nameof(IsOrderLoadedForEdit));
                OnPropertyChanged(nameof(ShowUpdateOrderButton));
                OnPropertyChanged(nameof(ShowFinishButtons));
                OnPropertyChanged(nameof(ShowSavePlaceOrderButtons));
            }
        }

        // Track when finish order flow is loading to disable buttons during transition
        private bool _isFinishOrderLoading = false;
        public bool IsFinishOrderLoading
        {
            get => _isFinishOrderLoading;
            set
            {
                _isFinishOrderLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowUpdateOrderButton));
                OnPropertyChanged(nameof(ShowFinishButtons));
                OnPropertyChanged(nameof(ShowSavePlaceOrderButtons));
            }
        }

        public bool ShowUpdateOrderButton => IsOrderLoadedForEdit && !POS_UI.Services.GlobalDataService.Instance.IsFinishFlow && !IsFinishOrderLoading;
        public bool ShowFinishButtons => IsOrderLoadedForEdit && POS_UI.Services.GlobalDataService.Instance.IsFinishFlow && !IsFinishOrderLoading;
        public bool ShowSavePlaceOrderButtons => !IsOrderLoadedForEdit && !IsFinishOrderLoading && !POS_UI.Services.GlobalDataService.Instance.IsFinishFlow;

        // Controls whether cart items and editing controls are interactive
        private bool _isCartEditable = true;
        public bool IsCartEditable
        {
            get => _isCartEditable;
            private set
            {
                if (_isCartEditable == value) return;
                _isCartEditable = value;
                OnPropertyChanged(nameof(IsCartEditable));
            }
        }

        // Track if a draft was loaded into the cart
        private bool _isDraftLoadedIntoCart = false;
        // Keep the loaded draft reference so we can restore it if user navigates away
        private DraftOrderModel _activeLoadedDraft = null;
        // Track whether the user has interacted with the cart since an order/draft was loaded
        private bool _hasCartInteractionSinceLoad = false;
        // Suppresses setting interaction flag during initial load of order/draft
        private bool _suppressCartInteractionFlag = false;

        // Called by Sidebar before navigating away from Cashier
        public void HandleNavigatingAwayFromCashier()
        {
            try
            {
                if (ShouldAutoClearCartOnNavigateAway())
                {
                    // If a draft was loaded into the cart, ensure it remains in Drafts list
                    if (_isDraftLoadedIntoCart && _activeLoadedDraft != null)
                    {
                        try
                        {
                            if (!DraftOrders.Contains(_activeLoadedDraft))
                            {
                                DraftOrders.Add(_activeLoadedDraft);
                                OnPropertyChanged(nameof(DraftOrders));
                                OnPropertyChanged(nameof(DraftCount));
                                _draftStorageService.SaveDrafts(DraftOrders);
                            }
                        }
                        catch { /* ignore restore errors */ }
                    }
                    CancelLoadedOrder();
                    // When leaving with a draft or non-interacted edit, reset customer context to Guest/default
                    try { _cartService.ResetCustomerHistory(); } catch { }
                    try { SelectedCustomer = FindOrCreateGuestCustomer(); OnPropertyChanged(nameof(SelectedCustomer)); } catch { }
                    // If a dine-in table was selected, persist a minimal placeholder so the UI recalls it
                    if (OrderType == "Dine In" && _cartService.TableNumber.HasValue)
                    {
                        SelectedTable = new TableModel
                        {
                            TableNumber = _cartService.TableNumber.Value,
                            Name = _cartService.TableName,
                            ApiId = _cartService.TableNumber.Value
                        };
                    }
                }
                
                // Mark data for reload when coming back to cashier page
                _needsDataReload = true;
                System.Diagnostics.Debug.WriteLine("[CashierVM] Navigating away - data will be reloaded on return");
            }
            catch { /* ignore */ }
            finally
            {
                // Reset flags regardless
                _isDraftLoadedIntoCart = false;
                _hasCartInteractionSinceLoad = false;
                _suppressCartInteractionFlag = false;
                _activeLoadedDraft = null;
            }
        }

        private bool ShouldAutoClearCartOnNavigateAway()
        {
            // Always clear if a draft is currently loaded
            if (_isDraftLoadedIntoCart) return true;
            // Clear if an existing order was loaded for edit/finish and no interactions occurred
            if (IsOrderLoadedForEdit && !_hasCartInteractionSinceLoad) return true;
            return false;
        }
        
        // Public method to refresh data when page is loaded
        public async Task RefreshDataIfNeededAsync()
        {
            if (_needsDataReload || !_isDataLoadedInMemory)
            {
                System.Diagnostics.Debug.WriteLine("[CashierVM] Reloading data from API...");
                await LoadDataAsync(forceReload: true);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[CashierVM] Using cached data");
            }
        }

        private static bool IsKitchenLockedStatus(string apiStatus)
        {
            var s = (apiStatus ?? string.Empty).Trim().ToUpperInvariant();
            return s == "PREPARING" || s == "READY" || s == "SERVED";
        }

        // Check if a specific item is editable: during edit, only items added after load are editable
        public bool IsItemEditable(OrderItem item)
        {
            if (item == null) return true;
            if (_cartItemIdsLockedForChargedSplitPayments.Contains(item.Id))
                return false;
            if (!IsOrderLoadedForEdit) return true;
            
            // If the item is NOT part of the original loaded order, it is a newly added item and should be editable
            // This prevents locking new items that happen to match the name/price of a locked existing item
            if (!_originalItemIds.Contains(item.Id)) return true;

            // Respect explicit read-only flags (e.g., from local dine-in statuses)
            if (item.IsReadOnly) return false;
            
            // Check original status first. If it is explicitly QUEUE, it should be editable regardless of lock keys.
            bool isQueue = string.Equals((item.OriginalStatus ?? string.Empty).Trim(), "QUEUE", StringComparison.OrdinalIgnoreCase);
            
            // Cross-check with local file snapshot keys (name|unitPrice)
            // Only apply this lock if the item is NOT in QUEUE status
            if (!isQueue)
            {
                var keyName = item.Product?.ItemName ?? item.Name ?? string.Empty;
                var keyUnit = Math.Round(item.Price, 2, MidpointRounding.AwayFromZero).ToString("0.00");
                var compositeKey = $"{keyName}|{keyUnit}";
                if (_lockedLocalItemKeys.Contains(compositeKey)) return false;
            }

            // Also respect captured original per-item status: any non-QUEUE item is locked
            if (!string.IsNullOrWhiteSpace(item.OriginalStatus) && !isQueue)
            {
                return false;
            }
            // If the order was loaded from PREPARING/READY/SERVED, only newly added items are editable
            if (_wasKitchenLockedAtLoad)
            {
                return !_originalItemIds.Contains(item.Id);
            }
            // Otherwise (loaded from QUEUE), original items should remain editable
            return true;
        }

        private static string GenerateOrderId()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 4).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        //Persists terminal order config via PATCH: display_order_id
        private async Task PersistDisplayOrderIdToOrderConfigAsync(string displayOrderId)
        {
            if (string.IsNullOrWhiteSpace(displayOrderId)) return;
            try
            {
                var settingsService = new SettingsService();
                var (_, outletCode, brandIdStr) = settingsService.LoadSettings();
                if (string.IsNullOrWhiteSpace(outletCode) || string.IsNullOrWhiteSpace(brandIdStr) || !int.TryParse(brandIdStr, out int brandId))
                    return;
                var shopDetails = await _apiService.GetShopDetailsAsync(outletCode, brandIdStr).ConfigureAwait(false);
                if (shopDetails == null || shopDetails.Id <= 0) return;

                var gds = GlobalDataService.Instance;
                var useLive = gds != null && gds.UseLiveOrdersPage;
                var orderConfig = new
                {
                    page_name = useLive ? "live_orders" : "orders",
                    is_live_orders_page = useLive,
                    is_takeaway = gds?.IsTakeawayAutoCompleteEnabled ?? false,
                    takeaway_timer_mins = gds != null ? gds.TakeawayAutoCompleteTimerMins : 0,
                    is_dinein = gds?.IsDineInAutoCompleteEnabled ?? false,
                    dinein_timer_mins = gds != null ? gds.DineInAutoCompleteTimerMins : 0,
                    is_delivery = gds?.IsDeliveryAutoCompleteEnabled ?? false,
                    delivery_timer_mins = gds != null ? gds.DeliveryAutoCompleteTimerMins : 0,
                    idle_logout_minutes = gds?.IdleLogoutMinutes ?? 10,
                    display_order_id = displayOrderId.Trim(),
                    ongoing_order = System.Array.Empty<object>()
                };
                var orderConfigJson = JsonConvert.SerializeObject(orderConfig);
                await _apiService.SaveOrderConfigAsync(shopDetails.Id, brandId, orderConfigJson).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CashierHomeVM] PersistDisplayOrderIdToOrderConfigAsync: {ex.Message}");
            }
        }

        // Clears the cart and exits edit mode when user cancels after loading an order from tables
        private void CancelLoadedOrder()
        {
            try
            {
                // Clear cart content
                _cartService.ClearCart();
                _cartService.ResetCustomerHistory();

                // Finish flow must clear before IsOrderLoadedForEdit so ShowSavePlaceOrderButtons re-evaluates to true
                // when the IsOrderLoadedForEdit setter raises PropertyChanged (it depends on IsFinishFlow).
                POS_UI.Services.GlobalDataService.Instance.IsFinishFlow = false;

                // Reset UI state
                IsOrderLoadedForEdit = false;
                IsCartEditable = true;
                _originalItemIds.Clear();
                _originalItemQuantities.Clear();
                _wasKitchenLockedAtLoad = false;
                _lockedLocalItemKeys.Clear();
                DisplayOrderId = GenerateOrderId();
                _ = PersistDisplayOrderIdToOrderConfigAsync(DisplayOrderId);
                SelectedCustomer = FindOrCreateGuestCustomer();
                OrderType = "Take Away";
                SelectedTable = null;
                SelectedOrderTime = null;
                Note = null;
                DiscountAmount = 0;
                DiscountPercent = 0;
                CouponCode = null;
                CouponDescription = null;
                CouponAmount = 0;
                DeliveryCharge = 0;
                ClearCashGiven();
                UpdateEstimatedPickupTime();

                // Notify bindings
                OnPropertyChanged(nameof(OrderItems));
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(SubTotal));
                OnPropertyChanged(nameof(Note));
                OnPropertyChanged(nameof(DiscountAmount));
                OnPropertyChanged(nameof(DiscountDescription));
                OnPropertyChanged(nameof(DiscountPercent));
                OnPropertyChanged(nameof(HasDiscount));
                OnPropertyChanged(nameof(CouponCode));
                OnPropertyChanged(nameof(CouponDescription));
                OnPropertyChanged(nameof(CouponAmount));
                OnPropertyChanged(nameof(HasCoupon));
                OnPropertyChanged(nameof(DeliveryCharge));
                OnPropertyChanged(nameof(CheckoutPrimaryButtonText));
                OnPropertyChanged(nameof(ShowFinishButtons));
                OnPropertyChanged(nameof(ShowUpdateOrderButton));
                OnPropertyChanged(nameof(ShowSavePlaceOrderButtons));
                OnPropertyChanged(nameof(CanAddCoupon));
                (PlaceOrderCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ConfirmOrderCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                //System.Windows.MessageBox.Show($"Error cancelling edit: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Cancelling Edit", $"Error cancelling edit: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
            }
        }

        private bool _showCategoryView = true;
        public bool ShowCategoryView
        {
            get => _showCategoryView;
            set
            {
                if (_showCategoryView != value)
                {
                    _showCategoryView = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowItemsView));
                    if (value) ShowMixedView = false;
                }
            }
        }

        private bool _showMixedView;
        public bool ShowMixedView
        {
            get => _showMixedView;
            set
            {
                if (_showMixedView != value)
                {
                    _showMixedView = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowItemsView));
                }
            }
        }

        public bool ShowItemsView => !ShowCategoryView && !ShowMixedView;

        private bool _canGoBack;
        public bool CanGoBack
        {
            get => _canGoBack;
            set
            {
                if (_canGoBack != value)
                {
                    _canGoBack = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _selectedCategory;
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory != value)
                {
                    _selectedCategory = value;
                    OnPropertyChanged();
                    
                    // When any category is selected (including "All Items"), show items view
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        ShowCategoryView = false;
                        // Only filter when actually selecting a category (not when clearing)
                        FilterProducts();
                    }
                }
            }
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    
                    // When searching, switch to universal items view (hide category/mixed grids)
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        ShowCategoryView = false;
                        ShowMixedView = false;
                        OnPropertyChanged(nameof(ShowItemsView));
                        SelectedCategory = "All Items";
                    }
                    
                    FilterProducts();
                }
            }
        }

        public CashierHomeViewModel()
        {
            _cartService.PropertyChanged += CartService_PropertyChanged;
            _cartService.TaxesUpdated += CartServiceOnTaxesUpdated;
            CartServiceOnTaxesUpdated(_cartService.CurrentTaxResult);
            // Attach handler to all existing items at startup
            foreach (OrderItem item in OrderItems)
            {
                item.PropertyChanged += OrderItem_PropertyChanged;
            }
            OrderItems.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (OrderItem item in e.NewItems)
                    {
                        item.PropertyChanged += OrderItem_PropertyChanged;
                    }
                }
                if (e.OldItems != null)
                {
                    foreach (OrderItem item in e.OldItems)
                    {
                        item.PropertyChanged -= OrderItem_PropertyChanged;
                    }
                }
                // Handle Reset: re-attach to all items
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                {
                    foreach (OrderItem item in OrderItems)
                    {
                        item.PropertyChanged -= OrderItem_PropertyChanged; // avoid duplicates
                        item.PropertyChanged += OrderItem_PropertyChanged;
                    }
                }
                RecalculateDiscount();
                RecalculateCoupon();
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(Discount));
                OnPropertyChanged(nameof(SubTotal));
                OnPropertyChanged(nameof(CanPlaceOrder));
                if (OrderItems.Count == 0)
                    ClearChargedSplitPaymentCartItemLocks();
            };
            // Set the bearer token for protected API calls
            var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
            if (!string.IsNullOrEmpty(accessToken))
            {
                _apiService.SetBearerToken(accessToken);
            } 
            
            // Subscribe to menu data refresh events
            GlobalDataService.Instance.MenuDataRefreshed += OnMenuDataRefreshed;
            
            // Initialize collections
            Categories = new ObservableCollection<string>();
            AllProducts = new ObservableCollection<POS_UI.Models.ProductItemModel>();
            Products = new ObservableCollection<POS_UI.Models.ProductItemModel>();
            
            // Initialize Tables collection - will be loaded from API
            Tables = new ObservableCollection<TableModel>();
            AddToOrderCommand = new CashierRelayCommand<OrderItem>(AddToOrder);
            RemoveFromOrderCommand = new CashierRelayCommand<OrderItem>(RemoveFromOrder);
            DecreaseQuantityCommand = new CashierRelayCommand<OrderItem>(DecreaseQuantity);
            ChangeOrderTypeCommand = new CashierRelayCommand<string>(type => 
            { 
                // Clear delivery charge if switching away from Delivery
                if (OrderType == "Delivery" && type != "Delivery")
                {
                    DeliveryCharge = 0;
                    DeliveryAddress = null;
                }
                OrderType = type;
                
                // Update TimeButtonText based on new order type
                if (type == "Dine In")
                {
                    TimeButtonText = "Table";
                }
                else
                {
                    UpdateEstimatedPickupTime();
                }

                // When switching to Delivery, populate address dropdown from selected customer
                if (type == "Delivery")
                {
                    CustomerAddresses.Clear();
                    CustomerAddresses.Add(new CustomerAddressModel { Id = 0, Label = "Add New Address", AddressLine1 = string.Empty });
                    if (SelectedCustomer?.Addresses != null && SelectedCustomer.Addresses.Count > 0)
                    {
                        foreach (var a in SelectedCustomer.Addresses)
                        {
                            if (a != null) CustomerAddresses.Add(a);
                        }
                        var defaultAddr = SelectedCustomer.Addresses.FirstOrDefault(a => a?.IsDefault == true) ?? SelectedCustomer.Addresses.FirstOrDefault();
                        _suppressAddressModal = true;
                        SelectedAddress = defaultAddr ?? CustomerAddresses.FirstOrDefault();
                        _ = ComputeAndSetDistanceForSelectedAddressAsync();
                    }
                    else
                    {
                        _suppressAddressModal = true;
                        SelectedAddress = CustomerAddresses.FirstOrDefault();
                        DistanceText = (!string.IsNullOrWhiteSpace(GlobalDataService.Instance.ShopDetails?.CountryCode) && GlobalDataService.Instance.ShopDetails.CountryCode.Trim().ToUpperInvariant() != "LK") ? "0.0 mi" : "0.0 km";
                        OnPropertyChanged(nameof(DistanceText));
                    }
                    OnPropertyChanged(nameof(HasCustomerAddresses));
                }
                
                OnPropertyChanged(nameof(OrderType));
            });
            PlaceOrderCommand = new RelayCommand(PlaceOrder, () => CanPlaceOrder && !IsPlacingOrder);
            SaveOrderCommand = new CashierRelayCommand(SaveOrder);
            UpdateOrderCommand = new CashierRelayCommand(UpdateOrder);
            CancelOrderCommand = new CashierRelayCommand(CancelLoadedOrder);
            FinishOrderCommand = new CashierRelayCommand(FinishOrder);
            ApplyDiscountCommand = new CashierRelayCommand(ApplyDiscount);
            AddNoteCommand = new CashierRelayCommand(AddNote);
            EditNoteCommand = new CashierRelayCommand(EditNote);
            RemoveNoteCommand = new CashierRelayCommand(RemoveNote);
            SelectCategoryCommand = new CashierRelayCommand<string>(SelectCategory);
            BackToCategoriesCommand = new CashierRelayCommand(BackToCategories);
            SelectMenuTabCommand = new CashierRelayCommand<MenuTabModel>(tab => SelectedMenuTab = tab);
            OpenAddItemDialogCommand = new CashierRelayCommand<POS_UI.Models.ProductItemModel>(OpenAddItemDialog);
            MixedItemClickCommand = new CashierRelayCommand<POS_UI.Models.MenuDisplayItem>(HandleMixedItemClick);
            EditOrderItemCommand = new CashierRelayCommand<OrderItem>(EditOrderItem);
            CurrentPage = "Cashier"; // Set the active page
            SelectedSortOption = ProductSortOption.None; // Default to custom menu order
            OpenTableSelectionCommand = new CashierRelayCommand(OpenTableSelection);
            OpenCouponDialogCommand = new CashierRelayCommand(OpenCouponDialog, () => CanAddCoupon);
            //ApplyCouponCommand = new CashierRelayCommand<string>(ApplyCoupon);
            RemoveCouponCommand = new CashierRelayCommand(RemoveCoupon);
            RemoveDiscountCommand = new CashierRelayCommand(RemoveDiscount);
            OpenDiscountDialogCommand = new CashierRelayCommand(OpenDiscountDialog);
            OpenDeliveryChargeDialogCommand = new CashierRelayCommand(OpenDeliveryChargeDialog);
            RemoveShopFeeCommand = new CashierRelayCommand<ShopFeeDisplayModel>(RemoveShopFee);
            AddShopFeeCommand = new CashierRelayCommand<ShopFeeDisplayModel>(AddShopFee);
            OpenSelectAddressDialogCommand = new RelayCommand(OpenSelectAddressDialog);
            OpenTimePickerCommand = new RelayCommand(async () => await OpenTimePicker());
            ClearPlaceSearchCommand = new RelayCommand(ClearPlaceSearch);
            // Load data from API
            // If an order is being loaded from Tables (Update Order flow), skip fetching customers now
            //if (Services.GlobalDataService.Instance.CurrentOrderForEdit == null)
            //{
            _ = LoadCustomersAsync();
            //}
            _ = LoadTablesFromApiAsync();
            // Attempt to sync selected table from cart immediately (in case tables already cached)
            SyncSelectedTableFromCart();
            
            // Load saved drafts from file
            LoadSavedDrafts();
            
            // Subscribe to DraftOrders collection changes to update DraftCount
            DraftOrders.CollectionChanged += (s, e) => OnPropertyChanged(nameof(DraftCount));
            
            SelectCustomerCommand = new CashierRelayCommand(OpenSelectCustomerDialog);
            OpenDraftsCommand = new RelayCommand(OpenDrafts);
            ClearSortCommand = new RelayCommand(ClearSort);
            SelectPaymentMethodCommand = new RelayCommand<PaymentMethod>(pm =>
            {
                SelectedPaymentMethod = pm;
            });
            EnableSplitPaymentCommand = new RelayCommand(async () => await OpenSplitPaymentDialogAsync());
            
            // Ensure we start with a valid default Guest customer when no explicit selection
            if (SelectedCustomer == null)
            {
                var guest = FindGuestInList();
                if (guest != null)
                {
                    SelectedCustomer = guest;
                    OnPropertyChanged(nameof(SelectedCustomer));
                }
            }
           /* ManualCardSelectedCommand = new RelayCommand<PaymentMethod>(async pm =>
            {
                try
                {
                    SelectedPaymentMethod = pm;
                    var confirmVm = POS_UI.ViewModels.StatusDialogViewModel.CreateInfo("Manual Card Payment", "Did you receive the money?");
                    var dialog = new POS_UI.View.StatusDialog { DataContext = confirmVm };
                    System.Windows.Application.Current.Dispatcher.Invoke(async () =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close(dialog, "AddItemDialogHost");
                    });
                    await Task.Delay(100);
                    var result = await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "AddItemDialogHost");
                    var answeredYes = string.Equals(result as string, "Yes", System.StringComparison.OrdinalIgnoreCase) || (result is bool b && b);
                    if (answeredYes)
                    {
                        ConfirmOrder();
                    }
                }
                catch { }
            });*/
            ConfirmOrderCommand = new RelayCommand(ConfirmOrder, () => !IsPlacingOrder && CanPlaceOrder);
            ClearCashGivenCommand = new RelayCommand(ClearCashGiven);
            NumberPadCommand = new RelayCommand<string>(HandleNumberPadInput);
            StartShiftCommand = new RelayCommand(async () => await StartShiftAsync());
            //SetQuickAmountCommand = new RelayCommand<decimal>(SetQuickAmount);
            // Restore order id from cart (loaded order) or last cashier session so it does not change when navigating away and back
            if (!string.IsNullOrWhiteSpace(_cartService.DisplayOrderId))
                DisplayOrderId = _cartService.DisplayOrderId;
            else if (!string.IsNullOrWhiteSpace(_cartService.CashierSessionDisplayOrderId))
                DisplayOrderId = _cartService.CashierSessionDisplayOrderId;
            else
            {
                DisplayOrderId = GenerateOrderId();
                _ = PersistDisplayOrderIdToOrderConfigAsync(DisplayOrderId);
            }

            if (!string.IsNullOrWhiteSpace(DisplayOrderId) && OrderItems.Count > 0)
                _ = ApplyChargedSplitPaymentReadOnlyFromApiIfNeededAsync();

            // Initialize delivery address dropdown with sentinel when no addresses
            if (CustomerAddresses.Count == 0)
            {
                CustomerAddresses.Add(new CustomerAddressModel { Id = 0, Label = "Add New Address", AddressLine1 = string.Empty });
                SelectedAddress = CustomerAddresses.First();
            }
            
            // Initialize the timer with estimated pickup time
            UpdateEstimatedPickupTime();
            
            // Load categories from API
            _ = LoadDataAsync();

            // React to external order status changes (from Kitchen, etc.)
            POS_UI.Services.GlobalDataService.Instance.OrderStatusChanged += OnExternalOrderStatusChanged;
        }

        private CustomerModel FindOrCreateGuestCustomer()
        {
            var guest = FindGuestInList();
            if (guest != null) return guest;
            // Create a transient Guest customer object if not in list
            return new CustomerModel
            {
                FirstName = "Guestttt",
                LastName = "Customer",
                Phone = string.Empty,
                CustomerId = 0
            };
        }

        private CustomerModel FindGuestInList()
        {
            try
            {
                if (Customers == null || Customers.Count == 0) return null;
                return Customers.FirstOrDefault(c => string.Equals(($"{c.FirstName} {c.LastName}").Trim(), "Guest Customer", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.FirstName?.Trim(), "Guest", StringComparison.OrdinalIgnoreCase));
            }
            catch { return null; }
        }

        private void SyncSelectedTableFromCart()
        {
            try
            {
                if (OrderType != "Dine In") return;
                if (!_cartService.TableNumber.HasValue && string.IsNullOrWhiteSpace(_cartService.TableName))
                {
                    SelectedTable = null;
                    return;
                }

                // Prefer match by TableNumber; fallback to ApiId or Name
                var found = Tables?.FirstOrDefault(t => t.TableNumber == _cartService.TableNumber);
                if (found == null && _cartService.TableNumber.HasValue)
                {
                    found = Tables?.FirstOrDefault(t => t.ApiId == _cartService.TableNumber.Value);
                }
                if (found == null && !string.IsNullOrWhiteSpace(_cartService.TableName))
                {
                    found = Tables?.FirstOrDefault(t => string.Equals(t.Name, _cartService.TableName, StringComparison.OrdinalIgnoreCase));
                }

                if (found != null)
                {
                    SelectedTable = found;
                }
                else if (_cartService.TableNumber.HasValue || !string.IsNullOrWhiteSpace(_cartService.TableName))
                {
                    SelectedTable = new TableModel
                    {
                        ApiId = _cartService.TableNumber ?? 0,
                        TableNumber = _cartService.TableNumber ?? 0,
                        Name = string.IsNullOrWhiteSpace(_cartService.TableName) ? ($"T{_cartService.TableNumber}") : _cartService.TableName,
                        Status = TableStatus.Drafted
                    };
                }
            }
            catch { }
        }

        private async void OnExternalOrderStatusChanged(int orderId, string newStatus)
        {
            try
            {
                if (!IsOrderLoadedForEdit) return;
                if (!_cartService.CurrentOrderApiId.HasValue) return;
                if (_cartService.CurrentOrderApiId.Value != orderId) return;
                IsCartEditable = !IsKitchenLockedStatus(newStatus);

                // If status changed to QUEUE while editing, keep original items locked.
                // Only items added after the initial load should be editable.
                if (string.Equals((newStatus ?? string.Empty).Trim(), "QUEUE", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var item in OrderItems)
                    {
                        if (_originalItemIds.Contains(item.Id))
                        {
                            item.IsReadOnly = true;
                        }
                    }
                }

                // Additionally, listen for per-item local status progression using LocalItemId mapping
                if (!string.IsNullOrWhiteSpace(newStatus) && !string.Equals(newStatus.Trim(), "QUEUE", StringComparison.OrdinalIgnoreCase))
                {
                    // If some items progressed (e.g., PREPARING/READY/SERVED), persist local status and re-apply overlay
                    if (OrderType == "Dine In" && !string.IsNullOrWhiteSpace(DisplayOrderId))
                    {
                        string localItemStatus = POS_UI.Models.DineInOrderItemStatus.QUEUE;
                        var s = (newStatus ?? string.Empty).Trim().ToUpperInvariant();
                        if (s == "PREPARING") localItemStatus = POS_UI.Models.DineInOrderItemStatus.PREPARE;
                        else if (s == "READY") localItemStatus = POS_UI.Models.DineInOrderItemStatus.READY;
                        else if (s == "SERVED" || s == "DELIVERED") localItemStatus = POS_UI.Models.DineInOrderItemStatus.SERVED;

                        // Persist to local JSON first, then refresh overlay to ensure statuses are reflected
                        await POS_UI.Services.DineInOrderService.Instance.UpdateAllItemsStatusAsync(DisplayOrderId, localItemStatus);
                        await RefreshLocalOverlayAsync();
                    }
                }
                else if (string.Equals((newStatus ?? string.Empty).Trim(), "QUEUE", StringComparison.OrdinalIgnoreCase))
                {
                    // When returning to QUEUE after adding items, refresh overlay from local file so PREPARE items remain locked
                    if (OrderType == "Dine In" && !string.IsNullOrWhiteSpace(DisplayOrderId))
                    {
                        await RefreshLocalOverlayAsync();
                    }
                }
            }
            catch { }
        }

        private async Task RefreshLocalOverlayAsync()
        {
            try
            {
                // Pull local dine-in file and compute lock keys
                _lockedLocalItemKeys.Clear();
                var order = await POS_UI.Services.DineInOrderService.Instance.LoadDineInOrderAsync(DisplayOrderId);
                if (order != null && order.Items != null)
                {
                    foreach (var it in order.Items)
                    {
                        if (!string.Equals((it.ItemStatus ?? string.Empty).Trim(), POS_UI.Models.DineInOrderItemStatus.QUEUE, StringComparison.OrdinalIgnoreCase))
                        {
                            var key = $"{it.ItemName}|{Math.Round(it.UnitPrice, 2, MidpointRounding.AwayFromZero):0.00}";
                            _lockedLocalItemKeys.Add(key);
                        }
                    }
                }

                // Apply lock to cart items based on local file snapshot
                // Only apply to original items to avoid locking newly added identical items
                foreach (var ci in OrderItems)
                {
                    // Skip new items
                    if (!_originalItemIds.Contains(ci.Id)) continue;
                    
                    // Skip items that are explicitly in QUEUE status from API
                    if (string.Equals((ci.OriginalStatus ?? string.Empty).Trim(), "QUEUE", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var name = ci.Product?.ItemName ?? ci.Name ?? string.Empty;
                    var unit = Math.Round(ci.Price, 2, MidpointRounding.AwayFromZero).ToString("0.00");
                    var key = $"{name}|{unit}";
                    if (_lockedLocalItemKeys.Contains(key))
                    {
                        ci.IsReadOnly = true;
                        if (string.IsNullOrWhiteSpace(ci.OriginalStatus)) ci.OriginalStatus = "PREPARE";
                    }
                }

                // Also run existing overlay logic for completeness
                await POS_UI.Services.DineInOrderService.Instance.LoadOrderIntoCartForModificationAsync(DisplayOrderId);
            }
            catch { }
        }

        private void CartService_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Ensure UI shows API-provided discount when editing loaded orders
            if (e.PropertyName == nameof(_cartService.DiscountAmount) ||
                e.PropertyName == nameof(_cartService.SubTotal) ||
                e.PropertyName == nameof(_cartService.Total) ||
                e.PropertyName == nameof(_cartService.VoucherDiscount))
            {
                OnPropertyChanged(nameof(Discount));
                OnPropertyChanged(nameof(HasDiscount));
                OnPropertyChanged(nameof(SubTotal));
                OnPropertyChanged(nameof(VoucherDiscount));
                OnPropertyChanged(nameof(HasVoucherDiscount));
                OnPropertyChanged(nameof(ShopFeeRows));
                OnPropertyChanged(nameof(HasShopFees));
                OnPropertyChanged(nameof(TotalShopFees));
                OnPropertyChanged(nameof(Total));
                if (!_suppressCartInteractionFlag)
                {
                    _hasCartInteractionSinceLoad = true;
                }
            }
        }

        private void CartServiceOnTaxesUpdated(CartTaxResult result)
        {
            if (Application.Current == null)
            {
                UpdateTaxSummaryRows(result);
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => UpdateTaxSummaryRows(result));
            }
        }

        private void UpdateTaxSummaryRows(CartTaxResult result)
        {
            TaxSummaryRows.Clear();
            if (result?.SummaryRows != null)
            {
                foreach (var row in result.SummaryRows)
                {
                    TaxSummaryRows.Add(new TaxSummaryRow
                    {
                        TaxCode = row.TaxCode,
                        TaxableAmount = row.TaxableAmount,
                        TaxAmount = row.TaxAmount,
                        Rate = row.Rate
                    });
                }
            }

            OnPropertyChanged(nameof(HasTaxSummary));
            OnPropertyChanged(nameof(TotalTax));
            OnPropertyChanged(nameof(ShopFeeRows));
            OnPropertyChanged(nameof(HasShopFees));
            OnPropertyChanged(nameof(TotalShopFees));
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(GrandTotal));
        }
        
        /// <summary>
        /// Handler for when menu data is refreshed in GlobalDataService
        /// </summary>
        private async void OnMenuDataRefreshed()
        {
            System.Diagnostics.Debug.WriteLine("[CashierVM] Menu data refreshed event received, reloading data...");
            
            // Force reload data from the refreshed cache
            await LoadDataAsync(forceReload: true);
            
            System.Diagnostics.Debug.WriteLine("[CashierVM] Menu data reloaded successfully");
        }
        
        private async Task LoadDataAsync(bool forceReload = false)
        {
            // If data is already loaded and we don't need to reload, skip the API call
            if (_isDataLoadedInMemory && !forceReload && !_needsDataReload)
            {
                System.Diagnostics.Debug.WriteLine("[CashierVM] Data already in memory, skipping load");
                return;
            }
            
            await SetLoadingAsync(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[CashierVM] Loading data from cache...");
                    
                    // Check shift status first
                    await CheckShiftStatusAsync();

                    // Use cached data from GlobalDataService (loaded on login)
                    var globalData = GlobalDataService.Instance;
                    
                    if (globalData.IsMenuDataLoaded)
                    {
                        System.Diagnostics.Debug.WriteLine("[CashierVM] Using cached menu data from GlobalDataService");
                        
                        // Load from cache
                        Categories.Clear();
                        foreach (var category in globalData.CachedCategories)
                        {
                            Categories.Add(category);
                        }

                        AllProducts.Clear();
                        foreach(var product in globalData.CachedProducts)
                        {
                            AllProducts.Add(product);
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[CashierVM] Loaded from cache: {Categories.Count} categories, {AllProducts.Count} products");
                    }
                    else
                    {
                        // Fallback: Load from API if cache is not available
                        System.Diagnostics.Debug.WriteLine("[CashierVM] Cache not available, loading from API...");
                        var (apiCategories, apiProducts) = await _apiService.GetProductsAndCategoriesAsync();
                        
                        // Add "All Items" as the first category
                        Categories.Clear();
                        Categories.Add("All Items");
                        
                        // Add categories from API
                        foreach (var category in apiCategories)
                        {
                            Categories.Add(category);
                        }

                        AllProducts.Clear();
                        foreach(var product in apiProducts)
                        {
                            AllProducts.Add(product);
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[CashierVM] Loaded from API: {apiCategories.Count} categories, {apiProducts.Count} products");
                    }
                    
                    // Don't auto-select a category - let user choose from category view
                    SelectedCategory = null;
                    
                    OnPropertyChanged(nameof(CategoriesWithCount));
                    FilterProducts();
                    
                    // Mark data as loaded in memory
                    _isDataLoadedInMemory = true;
                    _needsDataReload = false;
                    
                    // Load menu tabs from cache or API
                    await LoadMenuTabsAsync();
                    
                    System.Diagnostics.Debug.WriteLine($"[CashierVM] Data loading complete");
                    
                    if (IsOrderLoadedForEdit && OrderItems?.Count > 0)
                    {
                        RehydrateLoadedOrderItemsWithCatalog();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CashierVM] Error loading data: {ex.Message}");
                    
                    // Fallback to hardcoded categories if everything fails
                    Categories.Clear();
                    Categories.Add("All Items");
                    Categories.Add("Desert");
                    Categories.Add("Bakery");
                    Categories.Add("Starter");
                    Categories.Add("Main Dish");
                    Categories.Add("Beverage");
                    
                    // Don't auto-select a category - let user choose from category view
                    SelectedCategory = null;
                    
                    OnPropertyChanged(nameof(CategoriesWithCount));
                    
                    // Mark data as loaded even if we use fallback
                    _isDataLoadedInMemory = true;
                    _needsDataReload = false;
        
                }
            });
        }

        private async Task LoadCustomersAsync()
        {
            try
            {
                var customers = await _apiService.GetCustomersAsync();
                Customers = new ObservableCollection<CustomerModel>(customers);
                OnPropertyChanged(nameof(Customers));
                if (SelectedCustomer == null)
                {
                    // Restore previously selected customer from cart state when available
                    var lastPhone = _cartService?.LastCustomerPhone;
                    var lastName = _cartService?.LastCustomerName;
                    CustomerModel match = null;

                    if (!string.IsNullOrWhiteSpace(lastPhone))
                    {
                        var normalizedLastPhone = lastPhone.Trim();
                        match = Customers.FirstOrDefault(c =>
                            (!string.IsNullOrWhiteSpace(c.Phone) && c.Phone.Trim() == normalizedLastPhone) ||
                            (!string.IsNullOrWhiteSpace(c.FullPhoneNumber) && c.FullPhoneNumber.Trim() == normalizedLastPhone));
                    }

                    if (match == null && !string.IsNullOrWhiteSpace(lastName))
                    {
                        var normalizedLastName = lastName.Trim();
                        match = Customers.FirstOrDefault(c => ($"{c.FirstName} {c.LastName}").Trim() == normalizedLastName);
                    }

                    // Default to Guest Customer if present; otherwise first customer
                    var guest = FindGuestInList();
                    SelectedCustomer = match ?? guest ?? Customers.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedCustomer));
                }
            }
            catch (Exception ex)
            {
                // Check if it's a network connectivity issue
                var networkService = POS_UI.Services.NetworkConnectivityService.Instance;
                bool isNetworkError = !networkService.IsConnected || 
                                    ex.Message.Contains("Unable to connect") ||
                                    ex.Message.Contains("No such host") ||
                                    ex.Message.Contains("Connection refused") ||
                                    ex.Message.Contains("Network is unreachable") ||
                                    ex.Message.Contains("Timeout") ||
                                    ex.Message.Contains("The remote name could not be resolved") ||
                                    ex.Message.Contains("A connection attempt failed") ||
                                    ex.Message.Contains("The operation has timed out");
                
                if (isNetworkError)
                {
                    // Don't show error message if there's no internet connection
                    // The internet connection dialog will handle this
                    return;
                }
                
                //System.Windows.MessageBox.Show($"Failed to load customers: {ex.Message}");
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed", $"Failed to load customers: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
            }
        }

        public async Task LoadTablesFromApiAsync()
        {
            try
            {
                var tables = await _apiService.GetTablesAsync();
                Tables.Clear();
                foreach (var table in tables)
                {
                    Tables.Add(table);
                }
                OnPropertyChanged(nameof(Tables));
                // After refreshing tables from API, try to restore selected table from cart
                SyncSelectedTableFromCart();
            }
            catch (Exception ex)
            {
                // Check if it's a network connectivity issue
                var networkService = POS_UI.Services.NetworkConnectivityService.Instance;
                bool isNetworkError = !networkService.IsConnected || 
                                    ex.Message.Contains("Unable to connect") ||
                                    ex.Message.Contains("No such host") ||
                                    ex.Message.Contains("Connection refused") ||
                                    ex.Message.Contains("Network is unreachable") ||
                                    ex.Message.Contains("Timeout") ||
                                    ex.Message.Contains("The remote name could not be resolved") ||
                                    ex.Message.Contains("A connection attempt failed") ||
                                    ex.Message.Contains("The operation has timed out");
                
                if (isNetworkError)
                {
                    // Don't show error message if there's no internet connection
                    // The internet connection dialog will handle this
                    return;
                }
                
                //System.Windows.MessageBox.Show($"Failed to load tables: {ex.Message}");
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed", $"Failed to load tables: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
            }
        }

        private void AddToOrder(OrderItem item)
        {
            if (item == null) return;
            if (!IsItemEditable(item)) return;
            item.Quantity++;
            UpdateEstimatedPickupTime();
        }
        
        private void UpdateEstimatedPickupTime()
        {
            if (OrderType == "Dine In" && SelectedTable != null)
            {
                // For Dine In orders with selected table
                TimeButtonText = $"Table {SelectedTable.ApiId}";
            }
            else if (SelectedOrderTime.HasValue)
            {
                // If a specific time is selected (either manually or from loaded order)
                TimeButtonText = SelectedOrderTime.Value.ToString("hh:mm tt");
            }
            else if (OrderType == "Take Away" || OrderType == "Delivery")
            {
                // For Take Away and Delivery orders when no specific time is selected
                var shopDetails = GlobalDataService.Instance.ShopDetails;
                if (shopDetails?.DeliveryPlatform?.PrepTime > 0)
                {
                    var currentTime = DateTime.Now;
                    var estimatedPickupTime = currentTime.AddMinutes(shopDetails.DeliveryPlatform.PrepTime);
                    TimeButtonText = estimatedPickupTime.ToString("hh:mm tt");
                }
                else
                {
                    TimeButtonText = "Now";
                }
            }
            else
            {
                // Default fallback
                TimeButtonText = "Now";
            }
        }

        private void AddProductToOrder(ProductItemModel product)
        {
            if (product == null) return;
            // Open add item dialog or add as new item as before
            OpenAddItemDialog(product);
        }

        // Helper method to find matching order item considering all properties
        private OrderItem FindMatchingOrderItem(string itemName, Dictionary<int, List<string>> selectedModifiers, Dictionary<string, List<string>> nestedModifierDetails)
        {
            // Only look for exact matches - same item, modifiers, and nested modifiers
            return OrderItems?.FirstOrDefault(i => 
                !i.IsReadOnly &&
                ((i.Product != null && i.Product.ItemName == itemName) ||
                 (i.Product == null && i.Name == itemName)) &&
                AreModifiersEqual(i.SelectedModifiers, selectedModifiers) &&
                AreNestedModifiersEqual(i.NestedModifierDetails, nestedModifierDetails));
        }

        // Helper method to compare modifier selections
        private bool AreModifiersEqual(Dictionary<int, List<string>> modifiers1, Dictionary<int, List<string>> modifiers2)
        {
            if (modifiers1 == null && modifiers2 == null) return true;
            if (modifiers1 == null || modifiers2 == null) return false;
            if (modifiers1.Count != modifiers2.Count) return false;

            foreach (var kvp in modifiers1)
            {
                if (!modifiers2.TryGetValue(kvp.Key, out var list2)) return false;
                if (kvp.Value == null && list2 == null) continue;
                if (kvp.Value == null || list2 == null) return false;
                if (kvp.Value.Count != list2.Count) return false;
                if (!kvp.Value.OrderBy(x => x).SequenceEqual(list2.OrderBy(x => x))) return false;
            }
            return true;
        }

        // Helper method to compare nested modifier details
        private bool AreNestedModifiersEqual(Dictionary<string, List<string>> nested1, Dictionary<string, List<string>> nested2)
        {
            if (nested1 == null && nested2 == null) return true;
            if (nested1 == null || nested2 == null) return false;
            if (nested1.Count != nested2.Count) return false;

            foreach (var kvp in nested1)
            {
                if (!nested2.TryGetValue(kvp.Key, out var list2)) return false;
                if (kvp.Value == null && list2 == null) continue;
                if (kvp.Value == null || list2 == null) return false;
                if (kvp.Value.Count != list2.Count) return false;
                if (!kvp.Value.OrderBy(x => x).SequenceEqual(list2.OrderBy(x => x))) return false;
            }
            return true;
        }
        private void RemoveFromOrder(OrderItem item)
        {
            if (!IsItemEditable(item)) return;
            _cartService.RemoveItem(item);
            UpdateEstimatedPickupTime();
        }
        
        private void DecreaseQuantity(OrderItem item)
        {
            if (!IsItemEditable(item)) return;
            if (item.Quantity > 1)
            {
                // Decrease quantity - discount will be recalculated automatically due to property change notifications
                item.Quantity--;
                UpdateEstimatedPickupTime();
            }
            else
            {
                // If quantity is 1, remove the entire item
                _cartService.RemoveItem(item);
                UpdateEstimatedPickupTime();
            }
        }
        private async void PlaceOrder()
        {  
            // Guard: prevent placing order when total is negative
            if (SubTotal < 0)
            {
                var vmNeg = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Invalid Total", "Total amount cannot be negative.");
                var dlgNeg = new POS_UI.View.StatusDialog { DataContext = vmNeg };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlgNeg, "AddItemDialogHost");
                return;
            }

            
            // For Dine In orders, show the checkout dialog
            if (OrderType == "Dine In")
            {
                // Validate that a table is selected for Dine In orders
                if (SelectedTable == null)
                {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Table Required", "Please select a table for Dine In orders.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    return;
                }
                
                // For Dine In orders, show the checkout dialog
                // Set default cash amount to the effective total for payment
                CashGiven = PaymentDue;
                CashInputString = PaymentDue.ToString("F2");
                OnPropertyChanged(nameof(CashGivenString));
                OnPropertyChanged(nameof(CashBalance));
                OnPropertyChanged(nameof(IsCashPaymentValid));
                // Default to Card payment when opening checkout
                SelectedPaymentMethod = PaymentMethod.Card;
                OnPropertyChanged(nameof(CheckoutPrimaryButtonText));
                OnPropertyChanged(nameof(ShowPayLaterOption));
                OnPropertyChanged(nameof(IsDininOrder));
                await OpenCheckoutOrSplitDialogAsync().ConfigureAwait(true);
            }
            else if (OrderType == "Delivery")
            {
                // Validate delivery order requirements
                if (SelectedCustomer == null)
                {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Customer Required", "Please select a customer for Delivery orders.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    return;
                }
                
                if (SelectedAddress == null || SelectedAddress.Id == 0 || string.IsNullOrWhiteSpace(SelectedAddress.FullAddress))
                {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Address Required", "Please select a delivery address for Delivery orders.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    return;
                }
                
                // For Delivery orders, show the checkout dialog
                // Default to Card payment when opening checkout
                SelectedPaymentMethod = PaymentMethod.Card;
                // Set default cash amount to the effective total for payment
                CashGiven = PaymentDue;
                CashInputString = PaymentDue.ToString("F2");
                OnPropertyChanged(nameof(CashGivenString));
                OnPropertyChanged(nameof(CashBalance));
                OnPropertyChanged(nameof(IsCODPaymentValid));
                OnPropertyChanged(nameof(CheckoutPrimaryButtonText));
                OnPropertyChanged(nameof(IsCODPaymentSelected));
                
                OnPropertyChanged(nameof(CheckoutPrimaryButtonText));
                //
                //OnPropertyChanged(nameof(CheckoutPrimaryButtonText));
                await OpenCheckoutOrSplitDialogAsync().ConfigureAwait(true);
            }
            else
            {
                // For Take Away orders, show the checkout dialog
                // Set default cash amount to the effective total for payment
                CashGiven = PaymentDue;
                CashInputString = PaymentDue.ToString("F2");
                OnPropertyChanged(nameof(CashGivenString));
                OnPropertyChanged(nameof(CashBalance));
                OnPropertyChanged(nameof(IsCashPaymentValid));
                // Default to Card payment when opening checkout
                SelectedPaymentMethod = PaymentMethod.Card;

                await OpenCheckoutOrSplitDialogAsync().ConfigureAwait(true);
            }
        }

        private async Task OpenCheckoutOrSplitDialogAsync()
        {
            if (await HasExistingTempPaymentsAsync(DisplayOrderId).ConfigureAwait(true))
            {
                await OpenSplitPaymentDialogAsync("AddItemDialogHost").ConfigureAwait(true);
                return;
            }

            var dialog = new POS_UI.View.CheckoutDialog { DataContext = this };
            await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "AddItemDialogHost").ConfigureAwait(true);
        }

        private async Task<bool> HasExistingTempPaymentsAsync(string displayOrderId)
        {
            var orderId = (displayOrderId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(orderId))
                return false;

            try
            {
                _apiService.RefreshHeadersFromSettings();
                var (_, _, list) = await _apiService.GetTempPaymentsByDisplayOrderIdAsync(orderId).ConfigureAwait(true);
                return list != null && list.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private void ClearChargedSplitPaymentCartItemLocks()
        {
            _cartItemIdsLockedForChargedSplitPayments.Clear();
        }

        /// <summary>
        /// Locks every line currently in the cart (e.g. after another split is charged). Lines added after this call stay editable until the next register/API sync.
        /// </summary>
        private void RegisterChargedSplitPaymentReadOnlyOnCurrentCartItems()
        {
            foreach (var item in OrderItems)
            {
                if (item == null) continue;
                _cartItemIdsLockedForChargedSplitPayments.Add(item.Id);
                item.IsReadOnly = true;
            }
        }

        private async Task ApplyChargedSplitPaymentReadOnlyFromApiIfNeededAsync()
        {
            var orderId = (DisplayOrderId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(orderId) || OrderItems.Count == 0) return;

            try
            {
                _apiService.RefreshHeadersFromSettings();
                var (_, _, list) = await _apiService.GetTempPaymentsByDisplayOrderIdAsync(orderId).ConfigureAwait(true);
                if (list != null && list.Count > 0)
                    RegisterChargedSplitPaymentReadOnlyOnCurrentCartItems();
            }
            catch
            {
                /* ignore */
            }
        }

        private async void FinishOrder()
        {
            try
            {
                if (OrderItems.Count == 0)
                {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Empty Order", "Cannot finish an empty order.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    return;
                }


                if (SubTotal < 0)
                {
                    var vmNeg = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Invalid Total", "Total amount cannot be negative.");
                    var dlgNeg = new POS_UI.View.StatusDialog { DataContext = vmNeg };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlgNeg, "AddItemDialogHost");
                    return;
                }
                
                // Default values for checkout (same as Place Order path)
                CashGiven = PaymentDue;
                CashInputString = PaymentDue.ToString("F2");
                OnPropertyChanged(nameof(CashGivenString));
                OnPropertyChanged(nameof(CashBalance));

                // Default to Card payment when opening checkout
                SelectedPaymentMethod = PaymentMethod.Card;
                OnPropertyChanged(nameof(IsCashPaymentSelected));
                OnPropertyChanged(nameof(IsCashPaymentValid));

                // Notify button text binding and Pay Later visibility
                OnPropertyChanged(nameof(CheckoutPrimaryButtonText));
                OnPropertyChanged(nameof(ShowPayLaterOption));
                OnPropertyChanged(nameof(IsDininOrder));

                // Temp payments for this display order → split dialog; otherwise checkout (matches Place Order)
                await OpenCheckoutOrSplitDialogAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Opening Checkout", $"Error opening checkout{ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                //MessageBox.Show($"Error opening checkout: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async void SaveOrder()
        {
            if (IsCartActionProcessing) return;
            IsSavingOrder = true;
            try
            {
                if (OrderItems.Count == 0)
                {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Empty Order", "Cannot save an empty order.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    //MessageBox.Show("Cannot save an empty order.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Save current order as draft
                var draft = new DraftOrderModel
                {
                    CustomerName = SelectedCustomer?.FirstName + " " + SelectedCustomer?.LastName,
                    CustomerPhone = CustomerPhone,
                    Amount = SubTotal,
                    CreatedAt = DateTime.Now,
                    OrderType = OrderType,
                    TableNumber = OrderType == "Dine In" ? SelectedTable?.TableNumber.ToString() : null,
                    TableName = OrderType == "Dine In" ? (SelectedTable?.Name ?? (SelectedTable?.TableNumber > 0 ? $"T{SelectedTable?.TableNumber}" : null)) : null,
                    ScheduledTime = SelectedOrderTime, // Save the scheduled time
                    Note = Note,
                    DiscountAmount = DiscountAmount,
                    DiscountPercent = DiscountPercent,
                    DiscountModeApplied = DiscountPercent > 0 ? "percentage" : "value",
                    DiscountDescription = DiscountDescription,
                    CouponCode = CouponCode,
                    CouponAmount = CouponAmount,
                    CouponDescription = CouponDescription,
                    DeliveryCharge = DeliveryCharge,
                    Items = OrderItems.Select(i => new Models.OrderItem
                    {
                        Name = i.Product?.ItemName ?? i.Name,
                        Price = Math.Round(i.Price, 2, MidpointRounding.AwayFromZero),
                        BaseUnitPrice = i.BaseUnitPrice, // Save original/custom price before discount
                        DisAmount = i.DisAmount,
                        DiscountPercent = 0, // Persist final per-unit price; avoid reapplying percent later
                        Quantity = i.Quantity,
                        Notes = i.Note
                    }).ToList(),
                    ItemModifiers = BuildDraftModifiersMap(OrderItems),
                    ItemNestedModifiers = BuildDraftNestedModifiersMap(OrderItems),
                    // Save removed shop fees so they don't come back when loading draft
                    RemovedShopFeeIds = _cartService.GetRemovedShopFeeIds(),
                    RemovedShopFeeNames = _cartService.GetRemovedShopFeeNames()
                };

                // Log draft order details for debugging
                Console.WriteLine($"Saving draft order: Customer={draft.CustomerName}, OrderType={draft.OrderType}, Amount={draft.Amount}, Items={draft.Items.Count}");

                // Add to appropriate collection based on order type
                DraftOrders.Add(draft);
                OnPropertyChanged(nameof(DraftOrders));
                OnPropertyChanged(nameof(DraftCount));
                
                // Save all drafts to file
                _draftStorageService.SaveDrafts(DraftOrders);

                // Clear the current order and reset customer context to Guest/default
                _cartService.ClearCart();
                _cartService.ResetCustomerHistory();
                IsOrderLoadedForEdit = false; // Reset edit mode
                SelectedCustomer = FindOrCreateGuestCustomer();
                OnPropertyChanged(nameof(SelectedCustomer));

                
                // Reset order type and time/table selection to default state
                OrderType = "Take Away"; // Reset to default order type
                SelectedTable = null; // Clear selected table
                SelectedOrderTime = null; // Clear selected time
                
                // Update TimeButtonText based on new order type and PrepTime
                UpdateEstimatedPickupTime();
                
//Dev branch Insert
                Note = null;
                DiscountAmount = 0;
                DiscountPercent = 0;
                CouponCode = null;
                CouponDescription = null;
                CouponAmount = 0;
                ClearCashGiven();
                OnPropertyChanged(nameof(OrderItems));
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(Discount));
                OnPropertyChanged(nameof(SubTotal));

                OnPropertyChanged(nameof(OrderType));
                OnPropertyChanged(nameof(SelectedTable));
                OnPropertyChanged(nameof(SelectedOrderTime));
                OnPropertyChanged(nameof(TimeButtonText));
//Dev branch Insert
                OnPropertyChanged(nameof(Note));
                OnPropertyChanged(nameof(CanAddNote));
                OnPropertyChanged(nameof(DiscountAmount));
                OnPropertyChanged(nameof(DiscountDescription));
                OnPropertyChanged(nameof(DiscountPercent));
                OnPropertyChanged(nameof(HasDiscount));
                OnPropertyChanged(nameof(CouponCode));
                OnPropertyChanged(nameof(CouponDescription));
                OnPropertyChanged(nameof(CouponAmount));
                OnPropertyChanged(nameof(HasCoupon));
                OnPropertyChanged(nameof(DeliveryCharge));

                IsSavingOrder = false;
                await ShowSuccessModal("Order saved as draft successfully!");
            }
            catch (Exception ex)
            {
                IsSavingOrder = false;
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Saving Draft", $"Error saving draft: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
            }
        }

        // Loads saved drafts from the local file on application startup
        private void LoadSavedDrafts()
        {
            try
            {
                var savedDrafts = _draftStorageService.LoadDrafts();
                foreach (var draft in savedDrafts)
                {
                    DraftOrders.Add(draft);
                }
                OnPropertyChanged(nameof(DraftCount));
                
                if (savedDrafts.Count > 0)
                {
                    Console.WriteLine($"Loaded {savedDrafts.Count} saved drafts from file");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading saved drafts: {ex.Message}");
            }
        }

        private async void UpdateOrder()
        {
            if (IsCartActionProcessing) return;
            IsUpdatingOrder = true;
            try
            {
                if (OrderItems.Count == 0)
                {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Empty Order", "Cannot update an empty order.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    await MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    //MessageBox.Show("Cannot update an empty order.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validate that a table is selected for Dine In orders
                if (OrderType == "Dine In" && SelectedTable == null)
                {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Table Required", "Please select a table for Dine In orders.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    await MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    //MessageBox.Show("Please select a table for Dine In orders.", "Table Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Create order model from current cart state
                var orderModel = _cartService.CreateOrderModel(DisplayOrderId, SelectedCustomer, DiscountPercent, SelectedAddress);

                // Ensure required fields (like create) are populated for PUT
                string shippingMethod = OrderType switch
                {
                    "Take Away" => "TAKEAWAY",
                    "Dine In" => "DINE-IN",
                    "Delivery" => "DELIVERY",
                    _ => "TAKEAWAY"
                };
                orderModel.ShippingMethod = shippingMethod;
                orderModel.TableId = OrderType == "Dine In" ? (SelectedTable?.ApiId ?? 0) : 0;

                // Make sure we have the API order id to PUT to
                if (orderModel.ApiId <= 0 && _cartService.CurrentOrderApiId.HasValue)
                {
                    orderModel.ApiId = _cartService.CurrentOrderApiId.Value;
                }
                if (orderModel.ApiId <= 0)
                {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Updating Order", "Cannot update order: missing API order id.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    //MessageBox.Show("Cannot update order: missing API order id.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Prepare debug payload
                var requestPayload = orderModel.ToApiRequest();
                string requestJson = System.Text.Json.JsonSerializer.Serialize(requestPayload);
                
                // Update the order via API
                var apiService = new ApiService();
                var result = await orderModel.UpdateOrderAsync(apiService);
                
                IsUpdatingOrder = false;

                if (result)
                {
                    // Save local dine-in order modifications
                    if (OrderType == "Dine In" && !string.IsNullOrWhiteSpace(DisplayOrderId))
                    {
                        try
                        {
                            await Services.DineInOrderService.Instance.SaveCartModificationsToOrderAsync(DisplayOrderId);
                        }
                        catch { /* non-fatal for API success */ }
                    }
                    // Check if any new items were added OR quantities increased on existing items
                    bool hasNewItems = false;
                    var itemsToPrintForKitchen = new List<OrderItem>();
                    if (IsOrderLoadedForEdit && _originalItemIds.Count > 0)
                    {
                        // Count items that are not in the original items set (i.e., newly added items)
                        int newItemCount = OrderItems.Count(item => !_originalItemIds.Contains(item.Id));
                        hasNewItems = newItemCount > 0;
                        
                        //Console.WriteLine($"Order update analysis:");
                        //Console.WriteLine($"  Original items count: {_originalItemIds.Count}");
                        //onsole.WriteLine($"  Current items count: {OrderItems.Count}");
                        //MessageBox.Show($"  New items added: {newItemCount}");
                        //MessageBox.Show($"  Will change status to QUEUE: {hasNewItems}");
                        
                        // Debug: List the new items
                        if (newItemCount > 0)
                        {
                            var newItems = OrderItems.Where(item => !_originalItemIds.Contains(item.Id)).ToList();
                            itemsToPrintForKitchen.AddRange(newItems);
                            //MessageBox.Show($"  New items: {string.Join(", ", newItems.Select(item => item.Name ?? item.Product?.ItemName))}");
                        }
                        // Also detect quantity increases on existing items still in QUEUE
                        foreach (var item in OrderItems.Where(i => _originalItemIds.Contains(i.Id)))
                        {
                            if (_originalItemQuantities.TryGetValue(item.Id, out var originalQty))
                            {
                                if (item.OriginalStatus == null || string.Equals(item.OriginalStatus, POS_UI.Models.DineInOrderItemStatus.QUEUE, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (item.Quantity > originalQty)
                                    {
                                        // Add a synthetic line to print only the additional quantity
                                        var delta = item.Quantity - originalQty;
                                        if (delta > 0)
                                        {
                                            var deltaItem = new OrderItem
                                            {
                                                Product = item.Product,
                                                Name = item.Name,
                                                Quantity = delta,
                                                Price = item.Price,
                                                Note = item.Note,
                                                CategoryName = item.Product?.Category ?? item.CategoryName,
                                                SelectedModifiers = item.SelectedModifiers != null ? new Dictionary<int, List<string>>(item.SelectedModifiers) : null,
                                                NestedModifierDetails = item.NestedModifierDetails != null ? new Dictionary<string, List<string>>(item.NestedModifierDetails) : null,
                                                ExternalModifierDetailsForDisplay = item.ExternalModifierDetailsForDisplay?.ToList(),
                                                ExternalModifierTaxDetails = item.ExternalModifierTaxDetails != null ? new Dictionary<string, TaxDetailModel>(item.ExternalModifierTaxDetails) : new Dictionary<string, TaxDetailModel>()
                                            };
                                            itemsToPrintForKitchen.Add(deltaItem);
                                            hasNewItems = true;
                                        }
                                    }
                                }
                            }
                        }
                        // If there is anything to print, do it once
                        if (itemsToPrintForKitchen.Count > 0)
                        {
                            try { await POS_UI.Services.ReceiptPrintingService.Instance.PrintKitchenReceiptForItemsAsync(_cartService, itemsToPrintForKitchen); } catch { }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Order update: Not loaded for edit or no original items tracked. Status will not be changed to QUEUE.");
                    }
                    
                    // Only change status to QUEUE if new items were added
                    if (hasNewItems)
                    {
                        try
                        {
                            await apiService.UpdateOrderStatusAsync(orderModel.ApiId, "QUEUE");
                            
                            // Notify other parts of the application about the status change
                            GlobalDataService.Instance.NotifyOrderStatusChanged(orderModel.ApiId, "QUEUE");
                            
                            Console.WriteLine($"Order {orderModel.ApiId} status changed to QUEUE due to new items added");
                        }
                        catch (Exception statusEx)
                        {
                            Console.WriteLine($"Warning: Failed to update order status to QUEUE: {statusEx.Message}");
                            // Don't show error to user as the order update was successful
                        }
                        
                        //MessageBox.Show($"Order {DisplayOrderId} has been updated successfully with new items. Status changed to QUEUE.", "Order Updated", MessageBoxButton.OK, MessageBoxImage.Information);
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateSuccess("Order Updated", $"Order {DisplayOrderId} has been updated successfully with new items.");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    }
                    else
                    {
                        //MessageBox.Show($"No changes were made to the Order {DisplayOrderId}.", "Order Updated", MessageBoxButton.OK, MessageBoxImage.Information);
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateSuccess("Order Updated", $"Order {DisplayOrderId} has been updated successfully");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    }
                    
                    // Clear the cart and reset the edit mode
                    _cartService.ClearCart();
                    _cartService.ResetCustomerHistory();
                    IsOrderLoadedForEdit = false;
                    IsCartEditable = true;
                    _originalItemIds.Clear();
                    _originalItemQuantities.Clear();
                    _wasKitchenLockedAtLoad = false;
                    _lockedLocalItemKeys.Clear();
                    POS_UI.Services.GlobalDataService.Instance.IsFinishFlow = false;
                    DisplayOrderId = GenerateOrderId();
                    await PersistDisplayOrderIdToOrderConfigAsync(DisplayOrderId).ConfigureAwait(true);
                    
                    // Reset customer to Guest
                    SelectedCustomer = FindOrCreateGuestCustomer();
                    
                    // Reset order type and time/table selection to default state
                    OrderType = "Take Away";
                    SelectedTable = null;
                    SelectedOrderTime = null;
                    UpdateEstimatedPickupTime();
                    
                    // Clear other fields
                    Note = null;
                    DiscountAmount = 0;
                    DiscountPercent = 0;
                    CouponCode = null;
                    CouponDescription = null;
                    CouponAmount = 0;
                    DeliveryCharge = 0;
                    ClearCashGiven();
                    
                    // Trigger property change notifications
                    OnPropertyChanged(nameof(OrderItems));
                    OnPropertyChanged(nameof(Total));
                    OnPropertyChanged(nameof(SubTotal));
                    OnPropertyChanged(nameof(Note));
                    OnPropertyChanged(nameof(DiscountAmount));
                    OnPropertyChanged(nameof(DiscountDescription));
                    OnPropertyChanged(nameof(DiscountPercent));
                    OnPropertyChanged(nameof(HasDiscount));
                    OnPropertyChanged(nameof(CouponCode));
                    OnPropertyChanged(nameof(CouponDescription));
                    OnPropertyChanged(nameof(CouponAmount));
                    OnPropertyChanged(nameof(HasCoupon));
                    OnPropertyChanged(nameof(DeliveryCharge));
                    OnPropertyChanged(nameof(CanAddCoupon));
                    OnPropertyChanged(nameof(TimeButtonText));
                    OnPropertyChanged(nameof(SelectedCustomer));
                    OnPropertyChanged(nameof(CheckoutPrimaryButtonText));
                    OnPropertyChanged(nameof(ShowFinishButtons));
                    OnPropertyChanged(nameof(ShowUpdateOrderButton));
                    OnPropertyChanged(nameof(ShowSavePlaceOrderButtons));
                }
                else
                {
                    //MessageBox.Show($"Failed to update order.\nOrderId: {orderModel.ApiId}\nShipping: {orderModel.ShippingMethod}\nTableId: {orderModel.TableId}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Updating Order", $"Failed to update order.\nOrderId: {orderModel.ApiId}\nShipping: {orderModel.ShippingMethod}\nTableId: {orderModel.TableId}");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var settingsService = new POS_UI.Services.SettingsService();
                    var (tenantCodeDbg, outletCodeDbg, brandDbg) = settingsService.LoadSettings();
                    //MessageBox.Show(
                    //    $"Error updating order:\n{ex.Message}\n\nDebug:\nOrderId: {_cartService.CurrentOrderApiId?.ToString() ?? "(null)"}\nOrderType: {OrderType}\nShipping: {(OrderType == "Dine In" ? "DINE-IN" : (OrderType == "Take Away" ? "TAKEAWAY" : "DELIVERY"))}\nTableId: {(SelectedTable?.ApiId ?? 0)}\nTenant: {tenantCodeDbg}\nOutlet: {outletCodeDbg}\nBrand: {brandDbg}",
                    //    "Update Order Error",
                    //    MessageBoxButton.OK,
                    //    MessageBoxImage.Error);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Updating Order", $"Error updating order:\n{ex.Message}\n\nDebug:\nOrderId: {_cartService.CurrentOrderApiId?.ToString() ?? "(null)"}\nOrderType: {OrderType}\nShipping: {(OrderType == "Dine In" ? "DINE-IN" : (OrderType == "Take Away" ? "TAKEAWAY" : "DELIVERY"))}\nTableId: {(SelectedTable?.ApiId ?? 0)}\nTenant: {tenantCodeDbg}\nOutlet: {outletCodeDbg}\nBrand: {brandDbg}");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                }
                catch
            {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Updating Order", $"Error updating order: {ex.Message}");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                }
            }
            finally
            {
                IsUpdatingOrder = false;
            }
        }
        private void ApplyDiscount() { /* Discount logic */ }
        private async void AddNote()
        {
            try
            {
                var dialogVm = new NoteDialogViewModel(Note);
                var dialog = new POS_UI.View.NoteDialog { DataContext = dialogVm };
                
                dialogVm.NoteSaved += (note) =>
                {
                    Note = note;
                    OnPropertyChanged(nameof(CanAddNote));
                    OnPropertyChanged(nameof(HasNote));
                };
                
                dialogVm.DialogClosed += () => DialogHost.CloseDialogCommand.Execute(null, null);
                await DialogHost.Show(dialog, "AddItemDialogHost");
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error adding note: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Adding Note", $"Error adding note: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
            }
        }
        private async void EditNote()
        {
            try
            {
                var dialogVm = new NoteDialogViewModel(Note);
                var dialog = new POS_UI.View.NoteDialog { DataContext = dialogVm };
                
                dialogVm.NoteSaved += (note) =>
                {
                    Note = note;
                    OnPropertyChanged(nameof(CanAddNote));
                    OnPropertyChanged(nameof(HasNote));
                };
                
                dialogVm.DialogClosed += () => DialogHost.CloseDialogCommand.Execute(null, null);
                await DialogHost.Show(dialog, "AddItemDialogHost");
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error editing note: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Editing Note", $"Error editing note: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
            }
        }
        private void RemoveNote()
        {
            Note = null;
            OnPropertyChanged(nameof(Note));
            OnPropertyChanged(nameof(CanAddNote));
            OnPropertyChanged(nameof(HasNote));
        }
        private void SelectCategory(string category)
        {
            CanGoBack = true;
            SelectedCategory = category;
        }

        private void HandleMixedItemClick(POS_UI.Models.MenuDisplayItem displayItem)
        {
            if (displayItem == null) return;

            if (displayItem.IsCategory)
            {
                // Tapping a category in mixed view switches to that category's items
                ShowMixedView = false;
                ShowCategoryView = false;
                OnPropertyChanged(nameof(ShowItemsView));
                CanGoBack = true;
                SelectedCategory = displayItem.CategoryName;
            }
            else if (displayItem.IsItem && displayItem.Product != null)
            {
                OpenAddItemDialog(displayItem.Product);
            }
        }

        private void BackToCategories()
        {
            CanGoBack = false;
            // If this was a mixed tab, go back to mixed view instead of category view
            if (SelectedMenuTab != null && SelectedMenuTab.ContentType == "mixed"
                && SelectedMenuTab.Slots != null
                && SelectedMenuTab.Slots.Any(s => s.Type == "category")
                && SelectedMenuTab.Slots.Any(s => s.Type == "item"))
            {
                ShowCategoryView = false;
                ShowMixedView = true;
                OnPropertyChanged(nameof(ShowItemsView));
            }
            else
            {
                ShowCategoryView = true;
            }
            SearchText = string.Empty;
            SelectedCategory = null;
        }
        private void FilterProducts()
        {
            ApplySortFilter();
        }
        private void ApplySortFilter()
        {
            bool isSearching = !string.IsNullOrWhiteSpace(SearchText);

            IEnumerable<POS_UI.Models.ProductItemModel> filtered = AllProducts;
            
            // When searching, bypass ALL tab/category filters — universal item search
            if (!isSearching)
            {
                // Apply menu tab filter if we're on an items-type menu tab with specific items
                if (SelectedMenuTab != null && SelectedMenuTab.ContentType == "items" 
                    && SelectedMenuTab.ItemIds != null && SelectedMenuTab.ItemIds.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"[CashierVM] Applying menu tab filter: {SelectedMenuTab.ItemIds.Count} items");
                    filtered = filtered.Where(p => SelectedMenuTab.ItemIds.Contains(p.Id));
                }

                // Apply category filter
                if (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != "All Items")
                {
                    filtered = filtered.Where(p => p.Category == SelectedCategory);
                }
            }
            else
            {
                // Search across ALL products regardless of menu tab
                filtered = filtered.Where(p => p.ItemName.ToLower().Contains(SearchText.ToLower()));
            }
            
            // Apply sorting OR custom order (skip custom order when searching — results come from all products)
            if (!isSearching && SelectedSortOption == ProductSortOption.None && 
                SelectedMenuTab != null && SelectedMenuTab.ContentType == "items" &&
                SelectedMenuTab.ItemIds != null && SelectedMenuTab.ItemIds.Any())
            {
                // No sort + custom items menu = use custom order from ItemIds list
                var productDict = filtered.ToDictionary(p => p.Id, p => p);
                var orderedProducts = new List<POS_UI.Models.ProductItemModel>();
                foreach (var itemId in SelectedMenuTab.ItemIds)
                {
                    if (productDict.ContainsKey(itemId))
                    {
                        orderedProducts.Add(productDict[itemId]);
                    }
                }
                Products = new ObservableCollection<POS_UI.Models.ProductItemModel>(orderedProducts);
                System.Diagnostics.Debug.WriteLine($"[CashierVM] Applied custom menu order");
            }
            else
            {
                // Apply sorting
                switch (SelectedSortOption)
                {
                    case ProductSortOption.None:
                        // No sorting - keep natural order
                        break;
                    case ProductSortOption.AZ:
                        filtered = filtered.OrderBy(p => p.ItemName);
                        break;
                    case ProductSortOption.ZA:
                        filtered = filtered.OrderByDescending(p => p.ItemName);
                        break;
                    case ProductSortOption.PriceLowHigh:
                        filtered = filtered.OrderBy(p => p.Price);
                        break;
                    case ProductSortOption.PriceHighLow:
                        filtered = filtered.OrderByDescending(p => p.Price);
                        break;
                }
                Products = new ObservableCollection<POS_UI.Models.ProductItemModel>(filtered);
            }
            OnPropertyChanged(nameof(Products));
        }
        private void ClearSort()
        {
            SelectedSortOption = ProductSortOption.None;
            ApplySortFilter();
        }
        
        private void ClearPlaceSearch()
        {
            PlaceSearchText = string.Empty;
            PlacePredictions.Clear();
        }
        private async void OpenAddItemDialog(POS_UI.Models.ProductItemModel product)
        {
            try
            {
                if (product == null)
                {
                    //System.Windows.MessageBox.Show("Product is null", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Showing Dialog", "Product is null");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    return;
                }

                // Check if product has modifiers
                bool hasModifiers = product.Modifiers != null && product.Modifiers.Count > 0 && 
                                   product.Modifiers.FirstOrDefault()?.ModifierItems != null && 
                                   product.Modifiers.FirstOrDefault().ModifierItems.Count > 0;

                if (hasModifiers)
                {
                    // Product has modifiers - open AddModifiersDialog first
                    await OpenModifiersDialogFirst(product);
                }
                else
                {
                    // No modifiers - add directly to cart (skip dialog for efficiency)
                    await AddItemDirectlyToCart(product);
                }
            }
            catch (Exception ex)
            {
                //System.Windows.MessageBox.Show($"Error showing dialog: {ex.Message}\n\nStack Trace: {ex.StackTrace}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Showing Dialog", $"Error showing dialog: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
            }
        }

        private async Task AddItemDirectlyToCart(POS_UI.Models.ProductItemModel product)
        {
            var basePrice = product.Price > 0m
                ? product.Price
                : (product.PricePerItem > 0m ? product.PricePerItem : 0m);

            if (basePrice <= 0m)
            {
                // Zero-price item still needs the custom price dialog
                var customPriceVm = new CustomPriceDialogViewModel(product.ItemName, null);
                var customPriceDialog = new POS_UI.View.CustomPriceDialog { DataContext = customPriceVm };
                var customPriceResult = await MaterialDesignThemes.Wpf.DialogHost.Show(customPriceDialog, "AddItemDialogHost");

                if (customPriceResult is decimal manualPrice && manualPrice > 0m)
                {
                    basePrice = manualPrice;
                }
                else
                {
                    return;
                }
            }

            var emptyModifiers = new Dictionary<int, List<string>>();
            var emptyNested = new Dictionary<string, List<string>>();

            var newOrderItem = new OrderItem
            {
                Product = new POS_UI.Models.ProductItemModel
                {
                    Id = product.Id,
                    ItemName = product.ItemName,
                    PricePerItem = basePrice,
                    Price = basePrice,
                    Category = product.Category,
                    CategoryId = product.CategoryId,
                    Modifiers = product.Modifiers,
                    TaxProfileId = product.TaxProfileId,
                    PrinterGroups = product.PrinterGroups ?? new List<POS_UI.Models.PrinterGroupModel>()
                },
                Quantity = 1,
                Note = null,
                DiscountPercent = 0,
                ApiDiscountAmount = 0,
                DisAmount = 0,
                UnitDiscountAmount = 0,
                Price = basePrice,
                CategoryName = product.Category,
                SelectedModifiers = emptyModifiers,
                NestedModifierDetails = emptyNested
            };
            newOrderItem.VisibleDiscountAmount = 0;
            newOrderItem.BaseUnitPrice = basePrice;
            newOrderItem.TaxComponents = OrderItemTaxComponentBuilder.Build(newOrderItem.Product, newOrderItem.BaseUnitPrice, emptyModifiers, emptyNested);

            _cartService.AddItem(newOrderItem);
            RecalculateDiscount();
            RecalculateCoupon();
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(SubTotal));
        }

        private async Task OpenModifiersDialogFirst(POS_UI.Models.ProductItemModel product)
        {
            Dictionary<int, string> selectedModifiers = null;
            Dictionary<string, List<string>> nestedModifierDetails = null;
            bool wasCancelled = false;
            AddModifiersDialogViewModel modifiersDialogVm = null;
            try
            {
                modifiersDialogVm = new AddModifiersDialogViewModel(product.Modifiers);
                var modifiersDialog = new POS_UI.View.AddModifiersDialog { DataContext = modifiersDialogVm };
                modifiersDialogVm.ModifierSavedWithNested += (modifiers, nestedDetails) =>
                {
                    selectedModifiers = modifiers;
                    nestedModifierDetails = nestedDetails;
                };
                modifiersDialogVm.DialogClosed += () => 
                {
                    MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, null);
                    // If no modifiers were saved, it means the user cancelled
                    if (selectedModifiers == null)
                    {
                        wasCancelled = true;
                    }
                };
                // Handle nested modifiers
                modifiersDialogVm.NestedModifierRequested += async (modifierItem) =>
                {
                    try
                    {
                        // Get existing nested modifier details for pre-selection
                        var existingNestedDetails = modifiersDialogVm.GetAllNestedModifierDetails();
                        var preSelectedNestedModifiers = new Dictionary<int, List<string>>();
                        
                        // Convert existing nested details to the format expected by NestedModifiersDialogViewModel
                        if (existingNestedDetails != null && existingNestedDetails.TryGetValue(modifierItem.ItemName, out var nestedDetails))
                        {
                            // Parse the nested details to extract selected items for each group
                            foreach (var detail in nestedDetails)
                            {
                                // Format: "GroupTitle: ItemName   $Price"
                                var colonIndex = detail.IndexOf(':');
                                if (colonIndex > 0)
                                {
                                    var groupTitle = detail.Substring(0, colonIndex).Trim();
                                    var itemPart = detail.Substring(colonIndex + 1).Trim();
                                    var itemName = itemPart.Split('$')[0].Trim();
                                    
                                    // Find the group by title
                                    var group = modifierItem.NestedModifiers.FirstOrDefault(g => g.Title == groupTitle);
                                    if (group != null)
                                    {
                                        if (!preSelectedNestedModifiers.ContainsKey(group.Id))
                                        {
                                            preSelectedNestedModifiers[group.Id] = new List<string>();
                                        }
                                        preSelectedNestedModifiers[group.Id].Add(itemName);
                                    }
                                }
                            }
                        }
                        
                        var nestedDialogVm = new NestedModifiersDialogViewModel(modifierItem.NestedModifiers, modifierItem.ItemName, preSelectedNestedModifiers);
                        var nestedDialog = new POS_UI.View.NestedModifiersDialog { DataContext = nestedDialogVm };
                        Dictionary<int, string> selectedNestedModifiers = null;
                        nestedDialogVm.NestedModifierSaved += (nestedModifiers) =>
                        {
                            selectedNestedModifiers = nestedModifiers;
                        };
                        nestedDialogVm.DialogClosed += () => MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, null);
                        await MaterialDesignThemes.Wpf.DialogHost.Show(nestedDialog, "NestedModifiersDialogHost");
                        if (selectedNestedModifiers != null)
                        {
                            // Format nested details for summary
                            var formattedNestedDetails = new List<string>();
                            foreach (var group in modifierItem.NestedModifiers)
                            {
                                // Handle multiple selections from nested modifiers
                                if (selectedNestedModifiers.TryGetValue(group.Id, out var selectedNames))
                                {
                                    // Split the comma-separated string into individual selections
                                    var itemNames = selectedNames.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                                    foreach (var selectedName in itemNames)
                                {
                                    var selected = group.ModifierItems?.FirstOrDefault(item => item.ItemName == selectedName);
                                    if (selected != null)
                                    {
                                        formattedNestedDetails.Add($"{group.Title}: {selected.ItemName}   ${selected.ItemPrice:0.00}");
                                        }
                                    }
                                }
                            }
                            // Find the parent groupId for this modifierItem
                            var parentGroup = modifiersDialogVm.ModifierGroups.FirstOrDefault(g => g.ModifierItems != null && g.ModifierItems.Contains(modifierItem));
                            if (parentGroup != null)
                            {
                                modifiersDialogVm.SetNestedModifierDetails(parentGroup.Id, formattedNestedDetails, modifierItem);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //System.Windows.MessageBox.Show($"Error opening nested modifiers dialog: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Opening Nested Modifiers Dialog", $"Error opening nested modifiers dialog: {ex.Message}");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    }
                };
                await MaterialDesignThemes.Wpf.DialogHost.Show(modifiersDialog, "AddItemDialogHost");
                
                // Check if the user cancelled the operation
                if (wasCancelled)
                {
                    return; // Exit without opening the item dialog
                }
                
                // Only open AddItemDialog after the modifiers dialog is fully closed and not cancelled
                if (selectedModifiers != null)
                {
                    // Convert to multiple selection format
                    var multipleSelections = new Dictionary<int, List<string>>();
                    foreach (var kvp in selectedModifiers)
                    {
                        var itemNames = kvp.Value.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                        multipleSelections[kvp.Key] = new List<string>(itemNames);
                    }
                    
                    // Get the first selected modifier for backward compatibility
                    var firstSelected = selectedModifiers.Values.FirstOrDefault();
                    var firstItemName = firstSelected?.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    
                    await OpenAddItemDialogDirectly(product, selectedModifiers, nestedModifierDetails, multipleSelections);
                }
                else
                {
                    // User clicked Save but no modifiers selected, proceed to item dialog with empty selections
                    await OpenAddItemDialogDirectly(product, new Dictionary<int, string>(), nestedModifierDetails ?? new Dictionary<string, List<string>>(), new Dictionary<int, List<string>>());
                }
            }
            catch (Exception ex)
            {
                //System.Windows.MessageBox.Show($"Error opening modifiers dialog: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Opening Modifiers Dialog", $"Error opening modifiers dialog: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
            }
        }

        private async Task OpenAddItemDialogDirectly(POS_UI.Models.ProductItemModel product, Dictionary<int, string> selectedModifiers = null, Dictionary<string, List<string>> nestedModifierDetails = null, Dictionary<int, List<string>> selectedModifiersMultiple = null)
        {
            try
            {
                // For pre-filling, we need to find the most similar item since we don't have the exact modifiers yet
                // We'll look for an item with the same name, and if there are multiple, we'll use the first one
                var existingItem = OrderItems?.FirstOrDefault(i => 
                    (i.Product != null && i.Product.ItemName == product.ItemName && i.Product.Id == product.Id) ||
                    (i.Product == null && i.Name == product.ItemName));

                var basePrice = product.Price > 0m
                    ? product.Price
                    : (product.PricePerItem > 0m ? product.PricePerItem : 0m);

                if (basePrice <= 0m)
                {
                    var customPriceVm = new CustomPriceDialogViewModel(product.ItemName, null);
                    var customPriceDialog = new POS_UI.View.CustomPriceDialog { DataContext = customPriceVm };
                    var customPriceResult = await MaterialDesignThemes.Wpf.DialogHost.Show(customPriceDialog, "AddItemDialogHost");

                    if (customPriceResult is decimal manualPrice && manualPrice > 0m)
                    {
                        basePrice = manualPrice;
                    }
                    else
                    {
                        return;
                    }
                }

                AddItemDialogViewModel dialogVm;
                try
                {
                    // Use the new constructor for multiple selections if available, otherwise fall back to the old one
                    if (selectedModifiersMultiple != null && selectedModifiersMultiple.Count > 0)
                    {
                        dialogVm = new AddItemDialogViewModel(product.ItemName, basePrice, product, selectedModifiersMultiple, nestedModifierDetails);
                    }
                    else
                    {
                    dialogVm = new AddItemDialogViewModel(product.ItemName, basePrice, product, selectedModifiers, nestedModifierDetails);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error creating dialog view model: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }
                if (existingItem != null)
                {
                    dialogVm.Quantity = 1;
                    dialogVm.Note = null;
                    dialogVm.DiscountPercent = 0;
                    dialogVm.CustomDiscountText = "";
                    dialogVm.IsDiscount10Selected = false;
                    dialogVm.IsDiscount20Selected = false;

                    // Preserve modifier selections coming from the modifiers dialog (if any)
                    if (selectedModifiersMultiple != null)
                    {
                        dialogVm.SelectedModifiersMultiple = new Dictionary<int, List<string>>(selectedModifiersMultiple);
                    }
                    else if (selectedModifiers != null)
                    {
                        var convertedMultiple = new Dictionary<int, List<string>>();
                        foreach (var kvp in selectedModifiers)
                        {
                            var itemNames = kvp.Value.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                            convertedMultiple[kvp.Key] = new List<string>(itemNames);
                        }
                        dialogVm.SelectedModifiersMultiple = convertedMultiple;
                    }
                    else
                    {
                        dialogVm.SelectedModifiersMultiple = new Dictionary<int, List<string>>();
                    }

                    if (nestedModifierDetails != null)
                    {
                        dialogVm.NestedModifierDetails = new Dictionary<string, List<string>>(nestedModifierDetails);
                    }
                    else
                    {
                        dialogVm.NestedModifierDetails = new Dictionary<string, List<string>>();
                    }
                }
                // Create the dialog with proper error handling
                POS_UI.View.AddItemDialog dialog;
                try
                {
                    dialog = new POS_UI.View.AddItemDialog { DataContext = dialogVm };
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error creating dialog: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }
                dialogVm.ItemAdded += (itemVm) =>
                {
                    try
                    {
                        // Use the comprehensive matching function to find the exact item
                        var item = FindMatchingOrderItem(product.ItemName, itemVm.SelectedModifiersMultiple, itemVm.NestedModifierDetails);
                        
                        var modifierSelections = CloneModifierSelections(itemVm.SelectedModifiersMultiple);
                        var nestedSelections = CloneNestedDetails(itemVm.NestedModifierDetails);
                        if (item != null)
                        {
                            // When editing an existing order, adding again should create a new line
                            var newOrderItem = new OrderItem
                            {
                                Product = new POS_UI.Models.ProductItemModel 
                                { 
                                    Id = product.Id,
                                    ItemName = product.ItemName, 
                                    PricePerItem = itemVm.UnitPrice,
                                    Price = itemVm.UnitPrice, 
                                    Category = product.Category, 
                                    CategoryId = product.CategoryId,
                                    Modifiers = product.Modifiers,
                                    TaxProfileId = product.TaxProfileId,
                                    PrinterGroups = product.PrinterGroups ?? new List<POS_UI.Models.PrinterGroupModel>()
                                },
                                Quantity = itemVm.Quantity,
                                Note = itemVm.Note,
                                DiscountPercent = itemVm.DiscountPercent,
                                ApiDiscountAmount = itemVm.DiscountAmount,
                                DisAmount = itemVm.DiscountAmount,
                                UnitDiscountAmount = itemVm.Quantity > 0 ? Math.Round(itemVm.DiscountAmount / itemVm.Quantity, 2, MidpointRounding.AwayFromZero) : 0m,
                                Price = Math.Round(itemVm.FinalPrice / itemVm.Quantity, 2, MidpointRounding.AwayFromZero),
                                CategoryName = product.Category,
                                SelectedModifiers = modifierSelections,
                                NestedModifierDetails = nestedSelections
                            };
                            newOrderItem.VisibleDiscountAmount = itemVm.DiscountAmount;
                            newOrderItem.BaseUnitPrice = itemVm.BasePrice > 0 ? itemVm.BasePrice : (product.Price > 0 ? product.Price : newOrderItem.Price);
                            newOrderItem.TaxComponents = OrderItemTaxComponentBuilder.Build(newOrderItem.Product, newOrderItem.BaseUnitPrice, modifierSelections, nestedSelections);

                            _cartService.AddItemAsSeparateLine(newOrderItem);
                        }   
                        else
                        {
                            var newOrderItem = new OrderItem
                            {
                                Product = new POS_UI.Models.ProductItemModel 
                                { 
                                    Id = product.Id,
                                    ItemName = product.ItemName, 
                                    PricePerItem = itemVm.UnitPrice,
                                    Price = itemVm.UnitPrice, 
                                    Category = product.Category, 
                                    CategoryId = product.CategoryId,
                                    Modifiers = product.Modifiers,
                                    TaxProfileId = product.TaxProfileId,
                                    PrinterGroups = product.PrinterGroups ?? new List<POS_UI.Models.PrinterGroupModel>()
                                },
                                Quantity = itemVm.Quantity,
                                Note = itemVm.Note,
                                DiscountPercent = itemVm.DiscountPercent,
                                ApiDiscountAmount = itemVm.DiscountAmount,
                                DisAmount = itemVm.DiscountAmount,
                                UnitDiscountAmount = itemVm.Quantity > 0 ? Math.Round(itemVm.DiscountAmount / itemVm.Quantity, 2, MidpointRounding.AwayFromZero) : 0m,
                                Price = Math.Round(itemVm.FinalPrice / itemVm.Quantity, 2, MidpointRounding.AwayFromZero),
                                CategoryName = product.Category,
                                SelectedModifiers = modifierSelections,
                                NestedModifierDetails = nestedSelections
                            };
                            newOrderItem.VisibleDiscountAmount = itemVm.DiscountAmount;
                            newOrderItem.BaseUnitPrice = itemVm.BasePrice > 0 ? itemVm.BasePrice : (product.Price > 0 ? product.Price : newOrderItem.Price);
                            newOrderItem.TaxComponents = OrderItemTaxComponentBuilder.Build(newOrderItem.Product, newOrderItem.BaseUnitPrice, modifierSelections, nestedSelections);
                            
                            _cartService.AddItem(newOrderItem);
                        }
                        RecalculateDiscount();
                        RecalculateCoupon();
                        OnPropertyChanged(nameof(Total));
                        OnPropertyChanged(nameof(SubTotal));
                    }
                    catch (Exception ex)
                    {
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Adding Item", $"Error adding item: {ex.Message}");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                        //System.Windows.MessageBox.Show($"Error adding item: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                };
                dialogVm.DialogClosed += () => 
                {
                    try
                    {
                        DialogHost.CloseDialogCommand.Execute(null, null);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Error closing dialog: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                };
                await DialogHost.Show(dialog, "AddItemDialogHost");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error showing dialog: {ex.Message}\n\nStack Trace: {ex.StackTrace}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        private async void OpenTableSelection()
        {
            if (OrderType != "Dine In") return;
            try
            {
                // Open immediately; Tables fills in the background (same ObservableCollection the dialog binds to).
                var tablesViewModel = new POS_UI.ViewModels.TablesViewModel(subscribeToGlobalEvents: false);

                if (GlobalDataService.Instance.IsFloorPlanLayoutEnabled
                    && GlobalDataService.Instance.CachedFloorPlans is { Count: > 0 } cachedPlans)
                {
                    var planClones = cachedPlans.Select(p => p.Clone()).ToList();
                    var dialogVm = new FloorPlanCashierTableSelectionViewModel(
                        planClones,
                        tablesViewModel.Tables,
                        SelectedTable,
                        incomingTableName: null,
                        incomingOrderSessionId: null);
                    var dialog = new FloorPlanCashierTableSelectionDialog { DataContext = dialogVm };
                    try
                    {
                        var result = await DialogHost.Show(dialog, "AddItemDialogHost");
                        if (result is TableModel selected && selected != null)
                        {
                            SelectedTable = selected;
                        }
                    }
                    finally
                    {
                        dialogVm.Dispose();
                    }
                }
                else
                {
                    var dialogVm = new TableSelectionDialogViewModel(tablesViewModel.Tables, SelectedTable);
                    var dialog = new POS_UI.View.TableSelectionDialog { DataContext = dialogVm };
                    var result = await DialogHost.Show(dialog, "AddItemDialogHost");
                    if (result is TableModel selected && selected != null)
                    {
                        SelectedTable = selected;
                    }
                }
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Opening Table Selection Dialog", $"Failed to open table selection dialog: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                //MessageBox.Show($"Failed to open table selection dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void OpenCouponDialog()
        {
            try
            {
                var dialog = new POS_UI.View.SetCouponDialog();
                dialog.DataContext = this; // provide context for payment/order details
                var result = await DialogHost.Show(dialog, "AddItemDialogHost");
                /*if (result is string couponCode && !string.IsNullOrWhiteSpace(couponCode))
                {
                    ApplyCoupon(couponCode);
                }*/
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error opening coupon dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Opening Coupon Dialog", $"Error opening coupon dialog: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
            }
        }
        /*private void ApplyCoupon(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            code = code.Trim();
            if (int.TryParse(code, out int percent) && percent > 0 && percent <= 100)
            {
                CouponCode = code;
                CouponDescription = $"Coupon ({percent}%)";
                CouponAmount = Total * percent / 100m;
            }
            else
            {
                CouponCode = code;
                CouponDescription = $"Coupon ({code})";
                CouponAmount = 0;
                MessageBox.Show("Invalid coupon code. Please enter a number between 1 and 100 for percentage discount.", "Coupon", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            OnPropertyChanged(nameof(HasCoupon));
            OnPropertyChanged(nameof(CanAddCoupon));
            OnPropertyChanged(nameof(SubTotal));
        }*/
        private void RemoveCoupon()
        {
            CouponCode = null;
            CouponDescription = null;
            CouponAmount = 0;
            OnPropertyChanged(nameof(HasCoupon));
            OnPropertyChanged(nameof(CanAddCoupon));
            OnPropertyChanged(nameof(SubTotal));
        }
        private void RemoveDiscount()
        {
            DiscountAmount = 0;
            DiscountPercent = 0;
            OnPropertyChanged(nameof(Discount));
            OnPropertyChanged(nameof(DiscountDescription));
            OnPropertyChanged(nameof(HasDiscount));
            OnPropertyChanged(nameof(SubTotal));
        }

        private void RemoveShopFee(ShopFeeDisplayModel fee)
        {
            if (fee == null || fee.IsMandatory) return;
            _cartService.RemoveShopFee(fee.ShopFeeId, fee.Name, fee.IsMandatory);
            OnPropertyChanged(nameof(ShopFeeRows));
            OnPropertyChanged(nameof(HasShopFees));
            OnPropertyChanged(nameof(TotalShopFees));
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(TaxSummaryRows));
            OnPropertyChanged(nameof(TotalTax));
        }

        private void AddShopFee(ShopFeeDisplayModel fee)
        {
            if (fee == null || fee.IsMandatory || !fee.IsRemoved) return;
            _cartService.RestoreShopFee(fee.ShopFeeId, fee.Name);
            OnPropertyChanged(nameof(ShopFeeRows));
            OnPropertyChanged(nameof(HasShopFees));
            OnPropertyChanged(nameof(TotalShopFees));
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(TaxSummaryRows));
            OnPropertyChanged(nameof(TotalTax));
        }
        private async void OpenDiscountDialog()
        {
            try
            {
                var dialog = new POS_UI.View.SetDiscountDialog();
                var result = await DialogHost.Show(dialog, "AddItemDialogHost");
                if (result is string discountStr && !string.IsNullOrWhiteSpace(discountStr))
                {
                    // If dialog returned an amount payload, parse and set amount directly
                    if (discountStr.StartsWith("amount:", StringComparison.OrdinalIgnoreCase))
                    {
                        var valuePart = discountStr.Substring("amount:".Length).Trim();
                        if (decimal.TryParse(valuePart, out decimal amount) && amount >= 0)
                        {
                            DiscountPercent = 0;
                            DiscountAmount = amount;
                            OnPropertyChanged(nameof(HasDiscount));
                            OnPropertyChanged(nameof(DiscountDescription));
                            OnPropertyChanged(nameof(DiscountPercent));
                        }
                        else
                        {
                            //MessageBox.Show("Invalid amount. Please enter a valid number.", "Discount", MessageBoxButton.OK, MessageBoxImage.Warning);
                            var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateInfo("Invalid Amount", "Invalid amount. Please enter a valid number.");
                            var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                            MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                        }
                    }
                    else if (decimal.TryParse(discountStr, out decimal percent) && percent > 0 && percent <= 100)
                    {
                        // Persist percentage in cart service
                        DiscountPercent = percent; // Set the percentage first
                        // Calculate the amount based on current total; UI shows amount but percent is persisted
                        DiscountAmount = Total * percent / 100m;
                        OnPropertyChanged(nameof(HasDiscount));
                        OnPropertyChanged(nameof(DiscountDescription));
                        OnPropertyChanged(nameof(DiscountPercent));
                    }
                    else
                    {
                        //MessageBox.Show("Invalid discount. Please enter a number between 1 and 100, or a valid amount in the By Amount tab.", "Discount", MessageBoxButton.OK, MessageBoxImage.Warning);
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Invalid Discount", "Please enter a value between 1 and 100, or enter a valid amount.");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error opening discount dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Opening Discount Dialog", $"Error opening discount dialog: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
            }
        }
        private async void OpenDeliveryChargeDialog()
        {
            try
            {
                var dialog = new POS_UI.View.SetDeliveryChargeDialog();
                var result = await DialogHost.Show(dialog, "AddItemDialogHost");
                if (result is string deliveryChargeStr && !string.IsNullOrWhiteSpace(deliveryChargeStr))
                {
                    if (decimal.TryParse(deliveryChargeStr, out decimal amount) && amount >= 0)
                    {
                        DeliveryCharge = amount;
                    }
                    else
                    {
                        //MessageBox.Show("Invalid delivery charge. Please enter a valid amount.", "Delivery Charge", MessageBoxButton.OK, MessageBoxImage.Warning);
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateInfo("Invalid Delivery Charge", "Invalid delivery charge. Please enter a valid amount.");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error opening delivery charge dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Opening Delivery Charge Dialog", $"Error opening delivery charge dialog: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
            }
        }
        private async void OpenSelectCustomerDialog()
        {
            try
            {
                var dialogVm = new SelectCustomerDialogViewModel(SelectedCustomer);
                var dialog = new POS_UI.View.SelectCustomerDialog { DataContext = dialogVm };
                var result = await DialogHost.Show(dialog, "AddItemDialogHost");
                if (result is CustomerModel selected && selected != null)
                {
                    SelectedCustomer = selected;
                }
            }
            catch (Exception ex)
            {
                // Check if it's a network connectivity issue
                var networkService = POS_UI.Services.NetworkConnectivityService.Instance;
                bool isNetworkError = !networkService.IsConnected || 
                                    ex.Message.Contains("Unable to connect") ||
                                    ex.Message.Contains("No such host") ||
                                    ex.Message.Contains("Connection refused") ||
                                    ex.Message.Contains("Network is unreachable") ||
                                    ex.Message.Contains("Timeout") ||
                                    ex.Message.Contains("The remote name could not be resolved") ||
                                    ex.Message.Contains("A connection attempt failed") ||
                                    ex.Message.Contains("The operation has timed out");
                
                if (isNetworkError)
                {
                    // Don't show error message if there's no internet connection
                    // The internet connection dialog will handle this
                    return;
                }
                
                //MessageBox.Show($"Error selecting customer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Selecting Customer", $"Error selecting customer: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
            }
        }
        private async System.Threading.Tasks.Task OpenTimePicker()
        {
            // Calculate the initial time based on order type and prep time
            DateTime initialTime = DateTime.Now;
            
            if ((OrderType == "Take Away" || OrderType == "Delivery") && !SelectedOrderTime.HasValue)
            {
                var shopDetails = GlobalDataService.Instance.ShopDetails;
                if (shopDetails?.DeliveryPlatform?.PrepTime > 0)
                {
                    initialTime = DateTime.Now.AddMinutes(shopDetails.DeliveryPlatform.PrepTime);
                }
            }
            else if (SelectedOrderTime.HasValue)
            {
                // If a time is already selected, use that as the initial time
                initialTime = SelectedOrderTime.Value;
            }
            
            var dialogVm = new TimePickerDialogViewModel(initialTime);
            var dialog = new POS_UI.View.TimePickerDialog { DataContext = dialogVm };
            var result = await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "AddItemDialogHost");
            if (result is DateTime selectedTime)
            {
                SelectedOrderTime = selectedTime;
            }
        }
        private async void OpenDrafts()
        {
            try
            {
                if (DraftOrders.Count == 0)
                {
                    //MessageBox.Show("No draft orders available.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateInfo("Draft Orders", "No draft orders available.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    return;
                }

                var dialogVm = new DraftsDialogViewModel(DraftOrders);
                var dialog = new POS_UI.View.DraftsDialog { DataContext = dialogVm };
                
                // Ensure the dialog is properly initialized
                dialog.Loaded += (s, e) => 
                {
                    Console.WriteLine($"Dialog loaded with {DraftOrders.Count} drafts");
                };

                try
                {
                    var result = await DialogHost.Show(dialog, "AddItemDialogHost");
                    Console.WriteLine($"Dialog result: {result}");

                    if (result is DraftOrderModel selectedDraft)
                    {
                        // Load the selected draft
                        _suppressCartInteractionFlag = true;
                        _activeLoadedDraft = selectedDraft;
                        CustomerName = selectedDraft.CustomerName;
                        CustomerPhone = selectedDraft.CustomerPhone;
                        
                        // Try to find and select the customer if they exist
                        if (!string.IsNullOrEmpty(selectedDraft.CustomerName) && Customers != null)
                        {
                            var customer = Customers.FirstOrDefault(c => 
                                (c.FirstName + " " + c.LastName).Trim() == (selectedDraft.CustomerName ?? "").Trim() ||
                                c.Phone == selectedDraft.CustomerPhone);
                            if (customer != null)
                            {
                                SelectedCustomer = customer;
                            }
                        }
                        OrderType = selectedDraft.OrderType;
                        Console.WriteLine($"Loading draft with OrderType: {OrderType}");
                        
                        // Load table information for Dine In orders
                        if (OrderType == "Dine In" && !string.IsNullOrEmpty(selectedDraft.TableNumber))
                        {
                            if (Tables != null)
                            {
                                // Try to parse the table number and find the table
                                if (int.TryParse(selectedDraft.TableNumber, out int tableNumber))
                                {
                                    var foundTable = Tables.FirstOrDefault(t => t.TableNumber == tableNumber);
                                    SelectedTable = foundTable;
                                    
                                    // Set TimeButtonText for Dine In orders (similar to time loading for Delivery/Take Away)
                                    if (foundTable != null)
                                    {
                                        TimeButtonText = $"Table {foundTable.ApiId}";
                                        Console.WriteLine($"Successfully loaded table: {foundTable.TableNumber} for Dine In draft");
                                    }
                                    else
                                    {
                                        TimeButtonText = "Table";
                                        Console.WriteLine($"Table not found for number: {tableNumber}");
                                    }
                                }
                                else
                                {
                                    TimeButtonText = "Table";
                                    Console.WriteLine($"Could not parse table number from: {selectedDraft.TableNumber}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Warning: Tables collection is null when trying to load a Dine In draft. Cannot set SelectedTable.");
                                TimeButtonText = "Table";
                            }
                        }

                        // Load scheduled time for Delivery/Take Away orders
                        if ((OrderType == "Delivery" || OrderType == "Take Away") && selectedDraft.ScheduledTime.HasValue)
                        {
                            SelectedOrderTime = selectedDraft.ScheduledTime.Value;
                            TimeButtonText = SelectedOrderTime.Value.ToString("HH:mm");
                            Console.WriteLine($"Loaded scheduled time: {SelectedOrderTime.Value} for {OrderType} order");
                        }
                        
                        // Clear current order and load all draft details
                        _cartService.ClearCart();
						// Re-apply selected customer after clearing
						if (SelectedCustomer != null)
						{
							_cartService.CustomerName = ($"{SelectedCustomer.FirstName} {SelectedCustomer.LastName}").Trim();
							_cartService.CustomerPhone = SelectedCustomer.Phone;
						}
                        
                        // Load order items
                        Console.WriteLine($"Loading {selectedDraft.Items.Count} items from draft");
                        Console.WriteLine($"Available products count: {AllProducts.Count}");
                        if (AllProducts.Count == 0)
                        {
                            //MessageBox.Show("No products available. Please wait for products to load.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateInfo("Products Not Available", "No products available. Please wait for products to load.");
                            var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                            MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                            return;
                        }
                        foreach (var item in selectedDraft.Items)
                        {
                            Console.WriteLine($"Looking for product: {item.Name} with price {item.Price}");
                            
                            // Try to find the product with more flexible matching
                            var product = AllProducts.FirstOrDefault(p => 
                                p.ItemName.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
                            
                            if (product != null)
                            {
                                //Console.WriteLine($"Found product: {product.ItemName}");
                                // DisAmount from draft is the TOTAL discount for the line (all quantities)
                                // Calculate per-unit discount by dividing by quantity
                                var unitDiscount = item.Quantity > 0 && item.DisAmount > 0 
                                    ? Math.Round(item.DisAmount / item.Quantity, 2, MidpointRounding.AwayFromZero) 
                                    : 0m;
                                
                                var orderItem = new OrderItem
                                {
                                    Product = product,
                                    Name = item.Name, // Ensure Name is set
                                    Price = item.Price, // Final per-unit price (after discount)
                                    BaseUnitPrice = item.BaseUnitPrice, // Original/custom price before discount
                                    DisAmount = item.DisAmount, // Total discount for line (will NOT be multiplied again)
                                    UnitDiscountAmount = unitDiscount, // Per-unit discount
                                    VisibleDiscountAmount = item.DisAmount, // Total discount for display
                                    DiscountPercent = 0, // Avoid reapplying discount; Price is already final per-unit
                                    ApiDiscountAmount = unitDiscount, // Per-unit discount for API
                                    Quantity = item.Quantity,
                                    Note = item.Notes
                                };
                                
                                // Restore modifier information
                                var baseKey = BuildDraftItemKey(item.Name, item.Quantity, item.Price);
                                var itemKey = ResolveDraftKeyVariant(selectedDraft.ItemModifiers?.Keys, baseKey);
                                if (selectedDraft.ItemModifiers != null && itemKey != null && selectedDraft.ItemModifiers.ContainsKey(itemKey))
                                {
                                    // Copy selected modifiers; product provides prices
                                    orderItem.SelectedModifiers = new Dictionary<int, List<string>>(selectedDraft.ItemModifiers[itemKey]);
                                    Console.WriteLine($"Restored modifiers for {item.Name}");
                                }
                                var nestedKey = ResolveDraftKeyVariant(selectedDraft.ItemNestedModifiers?.Keys, baseKey);
                                if (selectedDraft.ItemNestedModifiers != null && nestedKey != null && selectedDraft.ItemNestedModifiers.ContainsKey(nestedKey))
                                {
                                    orderItem.NestedModifierDetails = new Dictionary<string, List<string>>(selectedDraft.ItemNestedModifiers[nestedKey]);
                                    Console.WriteLine($"Restored nested modifiers for {item.Name}");
                                }
                                
                                _cartService.AddItemFromDraft(orderItem);
                                Console.WriteLine($"Added item: {item.Name} x{item.Quantity}");
                            }
                            else
                            {
                                Console.WriteLine($"Product not found: {item.Name} with price {item.Price}");
                                Console.WriteLine($"Available products: {string.Join(", ", AllProducts.Select(p => $"{p.ItemName}(${p.Price})"))}");
                                
                                // DisAmount from draft is the TOTAL discount for the line (all quantities)
                                // Calculate per-unit discount by dividing by quantity
                                var unitDiscount = item.Quantity > 0 && item.DisAmount > 0 
                                    ? Math.Round(item.DisAmount / item.Quantity, 2, MidpointRounding.AwayFromZero) 
                                    : 0m;
                                
                                // Create a fallback OrderItem with the saved information
                                var fallbackItem = new OrderItem
                                {
                                    Name = item.Name,
                                    Price = item.Price, // Final per-unit price (after discount)
                                    BaseUnitPrice = item.BaseUnitPrice, // Original/custom price before discount
                                    DisAmount = item.DisAmount, // Total discount for line (will NOT be multiplied again)
                                    UnitDiscountAmount = unitDiscount, // Per-unit discount
                                    VisibleDiscountAmount = item.DisAmount, // Total discount for display
                                    DiscountPercent = 0, // Avoid reapplying discount; Price is already final per-unit
                                    ApiDiscountAmount = unitDiscount, // Per-unit discount for API
                                    Quantity = item.Quantity,
                                    Note = item.Notes
                                };
                                
                                // Restore modifier information for fallback item
                                var baseKey2 = BuildDraftItemKey(item.Name, item.Quantity, fallbackItem.Price);
                                var itemKey2 = ResolveDraftKeyVariant(selectedDraft.ItemModifiers?.Keys, baseKey2);
                                if (selectedDraft.ItemModifiers != null && itemKey2 != null && selectedDraft.ItemModifiers.ContainsKey(itemKey2))
                                {
                                    fallbackItem.SelectedModifiers = selectedDraft.ItemModifiers[itemKey2];
                                    Console.WriteLine($"Restored modifiers for fallback {item.Name}");
                                }
                                var nestedKey2 = ResolveDraftKeyVariant(selectedDraft.ItemNestedModifiers?.Keys, baseKey2);
                                if (selectedDraft.ItemNestedModifiers != null && nestedKey2 != null && selectedDraft.ItemNestedModifiers.ContainsKey(nestedKey2))
                                {
                                    fallbackItem.NestedModifierDetails = selectedDraft.ItemNestedModifiers[nestedKey2];
                                    Console.WriteLine($"Restored nested modifiers for fallback {item.Name}");
                                }
                                
                                _cartService.AddItemFromDraft(fallbackItem);
                                Console.WriteLine($"Added fallback item: {item.Name} x{item.Quantity}");
                            }
                        }
                        
                        Console.WriteLine($"Total items loaded: {_cartService.OrderItems.Count}");
                        foreach (var loadedItem in _cartService.OrderItems)
                        {
                            Console.WriteLine($"Loaded item: {loadedItem.Name} x{loadedItem.Quantity}");
                        }
                        
                        // Load additional order details
                        Note = selectedDraft.Note;
                        DiscountAmount = selectedDraft.DiscountAmount;
                        DiscountPercent = selectedDraft.DiscountPercent;
                        // Handle discount mode - if it's amount-based, we don't set DiscountPercent
                        // The discount amount will be handled separately in the cart
                        //DiscountDescription = selectedDraft.DiscountDescription;
                        CouponCode = selectedDraft.CouponCode;
                        CouponAmount = selectedDraft.CouponAmount;
                        CouponDescription = selectedDraft.CouponDescription;
                        DeliveryCharge = selectedDraft.DeliveryCharge;
                       // TableNumber = selectedDraft.TableNumber;
                       
                        // Restore removed shop fees so they don't reappear
                        if (selectedDraft.RemovedShopFeeIds != null || selectedDraft.RemovedShopFeeNames != null)
                        {
                            _cartService.RestoreRemovedShopFees(selectedDraft.RemovedShopFeeIds, selectedDraft.RemovedShopFeeNames);
                        }
                        
                        // Remove the draft from the DraftOrders collection since it's now loaded into the cart
                        DraftOrders.Remove(selectedDraft);
                        OnPropertyChanged(nameof(DraftOrders));
                        OnPropertyChanged(nameof(DraftCount));
                        
                        // Save updated drafts to file
                        _draftStorageService.SaveDrafts(DraftOrders);
                        // Mark draft state flags
                        _isDraftLoadedIntoCart = true;
                        _hasCartInteractionSinceLoad = false;
                        _suppressCartInteractionFlag = false;
                        
                        // Update all related properties
                        OnPropertyChanged(nameof(OrderItems));
                        OnPropertyChanged(nameof(Total));
                        OnPropertyChanged(nameof(SubTotal));
                        OnPropertyChanged(nameof(Note));
                        OnPropertyChanged(nameof(HasNote));
                        OnPropertyChanged(nameof(DiscountAmount));
                        OnPropertyChanged(nameof(DiscountPercent));
                        OnPropertyChanged(nameof(DiscountDescription));
                        OnPropertyChanged(nameof(HasDiscount));
                        OnPropertyChanged(nameof(CouponCode));
                        OnPropertyChanged(nameof(CouponAmount));
                        OnPropertyChanged(nameof(CouponDescription));
                        OnPropertyChanged(nameof(HasCoupon));
                        OnPropertyChanged(nameof(DeliveryCharge));
                        //OnPropertyChanged(nameof(TableNumber));
                        OnPropertyChanged(nameof(CanAddNote));
                        OnPropertyChanged(nameof(CanAddCoupon));
                        OnPropertyChanged(nameof(SelectedCustomer));
                        OnPropertyChanged(nameof(SelectedTable));
                        OnPropertyChanged(nameof(SelectedOrderTime));
                        OnPropertyChanged(nameof(TimeButtonText));
                        OnPropertyChanged(nameof(IsCashPaymentValid));
                        OnPropertyChanged(nameof(CashBalance));
                        
                        // Final debugging to check TimeButtonText value
                        Console.WriteLine($"Final TimeButtonText value: '{TimeButtonText}'");
                        Console.WriteLine($"Final OrderType: '{OrderType}'");
                        Console.WriteLine($"Final SelectedTable: {SelectedTable?.TableNumber}");
                    }
                }
                catch (Exception ex)
                {
                    //MessageBox.Show($"Error showing draft dialog: {ex.Message}\n{ex.StackTrace}", "Dialog Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Showing Draft Dialog", $"Error showing draft dialog: {ex.Message}");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error opening drafts: {ex.Message}\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Opening Drafts", $"Error opening drafts: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
            }
        }

        private async Task ShowSuccessModal(string message)
        {
            try
            {
                var dialog = new POS_UI.View.SuccessModal();
                await DialogHost.Show(dialog, "AddItemDialogHost");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing success modal: {ex.Message}");
                // Fallback to MessageBox if modal fails
                MessageBox.Show(message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SelectPaymentMethod(object method)
        {
            if (method is PaymentMethod pm)
                SelectedPaymentMethod = pm;
        }

        private void ClearCashGiven()
        {
            // Clear the cash amount without setting to 0.00
            CashGiven = 0;
            CashInputString = "";
            OnPropertyChanged(nameof(CashGivenString));
            OnPropertyChanged(nameof(CashBalance));
            OnPropertyChanged(nameof(IsCashPaymentValid));
            
            // Trigger focus on the cash input field
            OnPropertyChanged(nameof(ShouldFocusCashInput));

            _isCashInputPrimed = true;
        }

        private void HandleNumberPadInput(string input)
        {
            if (_isCashInputPrimed)
            {
                _isCashInputPrimed = false;
                CashInputString = string.Empty;
                CashGiven = 0;
                OnPropertyChanged(nameof(CashGivenString));
            }

            switch (input)
            {
                case "Backspace":
                    if (CashInputString.Length > 0)
                    {
                        CashInputString = CashInputString.Substring(0, CashInputString.Length - 1);
                    }
                    break;
                case ".":
                    if (!CashInputString.Contains("."))
                    {
                        CashInputString += ".";
                    }
                    break;
                default:
                    // Handle numeric input
                    if (input.All(char.IsDigit))
                    {
                        // Limit to 2 decimal places
                        if (CashInputString.Contains("."))
                        {
                            var parts = CashInputString.Split('.');
                            if (parts[1].Length < 2)
                            {
                                CashInputString += input;
                            }
                        }
                        else
                        {
                            CashInputString += input;
                        }
                    }
                    break;
            }

            // Update the actual cash amount if we have a valid input
            if (decimal.TryParse(CashInputString, out decimal result))
            {
                CashGiven = result;
            }
            else if (string.IsNullOrEmpty(CashInputString))
            {
                CashGiven = 0;
            }

            OnPropertyChanged(nameof(CashBalance));
            OnPropertyChanged(nameof(IsCashPaymentValid));
        }

        /*private void SetQuickAmount(decimal amount)
        {
            CashGiven = amount;
            OnPropertyChanged(nameof(CashGivenString));
            OnPropertyChanged(nameof(IsCashPaymentValid));
        }*/
        private async Task<CardTransactionResult> ProcessCardPaymentAsync(CardMachineModel cardMachine, decimal amount, string reference, Action<string> statusUpdateCallback = null)
        {
            try
            {
                return await _cardMachineApiService.ProcessCardPaymentAsync(cardMachine, amount, reference, statusUpdateCallback);
            }
            catch (Exception ex)
            {
                return new CardTransactionResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private async Task<CardTransactionResult> ProcessCardPaymentWithLoadingAsync(CardMachineModel cardMachine, decimal amount, string reference)
        {
            // Close the checkout dialog first
            MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, null);
            
            // Small delay to ensure dialog is closed
            await Task.Delay(100);
            
            var loadingDialog = new POS_UI.View.CardTransactionLoadingDialog();
            
            // Show loading dialog
            var dialogTask = MaterialDesignThemes.Wpf.DialogHost.Show(loadingDialog, "AddItemDialogHost");
            
            // Start the transaction processing with a timeout
            var transactionTask = ProcessCardPaymentWithProgressAsync(cardMachine, amount, reference, loadingDialog);
            
            // Create a timeout task that will complete after 125 seconds (5 seconds more than the API polling timeout of ~120s)
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(125));
            
            try
            {
                // Wait for either the dialog to be closed (cancelled), transaction to complete, or timeout
                var completedTask = await Task.WhenAny(dialogTask, transactionTask, timeoutTask);
                
                if (completedTask == dialogTask)
                {
                    // Dialog was closed (user cancelled)
                    var result = await dialogTask;
                    if (result?.ToString() == "CANCELLED")
                    {
                        return null; // User cancelled
                    }
                }
                else if (completedTask == timeoutTask)
                {
                    // Timeout occurred - force close the dialog and return timeout result
    
                    
                                    // Force close the loading dialog
                
                MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, null);
                await Task.Delay(200); // Longer delay to ensure closure
                
                // Try alternative closure method if needed
                try
                {
                    // Force close using the dialog host directly
                    var dialogHost = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>()
                        .FirstOrDefault(w => w.FindName("AddItemDialogHost") != null)?.FindName("AddItemDialogHost") as MaterialDesignThemes.Wpf.DialogHost;
                    
                    if (dialogHost != null && dialogHost.IsOpen)
                    {

                        dialogHost.IsOpen = false;
                        await Task.Delay(100);
                    }
                }
                catch (Exception ex)
                {
                    
                }
                    
                    return new CardTransactionResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Transaction timeout - no response received within 2 minutes. Please try again."
                    };
                }
                
                // Wait for transaction result (should complete quickly since we already know it's done)
                var transactionResult = await transactionTask;
                
                return transactionResult;
            }
            finally
            {
                // Always close the loading dialog, regardless of success/failure/timeout
                try
                {

                    MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, null);
                    await Task.Delay(200); // Longer delay to ensure loading dialog is closed
                    
                    // Try alternative closure method if needed
                    try
                    {
                        // Force close using the dialog host directly
                        var dialogHost = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>()
                            .FirstOrDefault(w => w.FindName("AddItemDialogHost") != null)?.FindName("AddItemDialogHost") as MaterialDesignThemes.Wpf.DialogHost;
                        
                        if (dialogHost != null && dialogHost.IsOpen)
                        {
    
                            dialogHost.IsOpen = false;
                            await Task.Delay(100);
                        }
                    }
                    catch (Exception altEx)
                    {
                        
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }

        private async Task<CardTransactionResult> ProcessCardPaymentWithProgressAsync(CardMachineModel cardMachine, decimal amount, string reference, POS_UI.View.CardTransactionLoadingDialog loadingDialog)
        {
            try
            {
                // Step 1: Connecting to card machine
                loadingDialog.UpdateStatus("Connecting to card machine...", "Step 1 of 4");
                await Task.Delay(500); // Small delay for UI update
                
                // Step 2: Initiating transaction
                loadingDialog.UpdateStatus("Initiating payment transaction...", "Step 2 of 4");
                await Task.Delay(500);
                
                // Step 3: Processing payment with real-time status updates
                loadingDialog.UpdateStatus("Processing payment with card machine...", "Step 3 of 4");
                
                var result = await _cardMachineApiService.ProcessCardPaymentAsync(cardMachine, amount, reference, 
                    (status) => loadingDialog.UpdateStatus(status, "Processing..."));
                
                if (result.IsSuccess)
                {
                    loadingDialog.UpdateStatus("Payment successful! Printing receipt and finalizing...", "Step 4 of 4");
                    await Task.Delay(1000); // Show success message briefly
                }
                else
                {
                    // Check if it's a timeout
                    if (result.ErrorMessage?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        loadingDialog.UpdateStatus("Transaction timed out. Please try again.", "Timeout");
                        await Task.Delay(2000); // Show timeout message longer
                    }
                    else
                    {
                        loadingDialog.UpdateStatus("Payment failed. Please try again.", "Failed");
                        await Task.Delay(2000); // Show error message longer
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                loadingDialog.UpdateStatus($"Error: {ex.Message}", "Error");
                await Task.Delay(2000); // Show error message
                
                return new CardTransactionResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private async void ConfirmOrder()
        {
            await ConfirmOrderAsync();
        }

        /// <summary>Shared by Place Order and split-payment Confirm so one tap completes placement after splits.</summary>
        private async Task ConfirmOrderAsync()
        {
            try
            {
                if (IsPlacingOrder) return;
                IsPlacingOrder = true;

                // If Manual Card is selected, ask for confirmation before proceeding
                /*if (SelectedPaymentMethod == PaymentMethod.ManualCard)
                {
                    try
                    {
                        var confirmVm = POS_UI.ViewModels.StatusDialogViewModel.CreateInfo("Manual Card Payment", "Did you receive the money?");
                        var dialog = new POS_UI.View.StatusDialog { DataContext = confirmVm };
                        // Show on nested host so it can appear above Checkout dialog
                        var resultManualCard = await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "NestedModifiersDialogHost");
                        var answeredYes = string.Equals(resultManualCard as string, "Ok", System.StringComparison.OrdinalIgnoreCase) || (resultManualCard is bool b && b);
                        if (!answeredYes)
                        {
                            IsPlacingOrder = false;
                            return;
                        }
                        else
                        {
                            IsPlacingOrder = true;
                            return;
                        }
                    }
                    catch { }
                }*/

                // While in checkout, keep the checkout dialog open; show nested prompts on a nested host

                // Ensure we have a valid customer id if a customer is selected (some APIs return success without id on create)
                if (SelectedCustomer != null && SelectedCustomer.CustomerId <= 0)
                {
                    try
                    {
                        var allCustomers = await _apiService.GetCustomersAsync();
                        var match = allCustomers.FirstOrDefault(c =>
                            (!string.IsNullOrWhiteSpace(c.Phone) && !string.IsNullOrWhiteSpace(SelectedCustomer.Phone) &&
                             c.Phone.Trim() == SelectedCustomer.Phone.Trim()) ||
                            (!string.IsNullOrWhiteSpace(c.FullPhoneNumber) && !string.IsNullOrWhiteSpace(SelectedCustomer.FullPhoneNumber) &&
                             c.FullPhoneNumber.Trim() == SelectedCustomer.FullPhoneNumber.Trim()));
                        if (match != null)
                        {
                            SelectedCustomer.CustomerId = match.CustomerId;
                        }
                    }
                    catch { /* best-effort lookup */ }

                    if (SelectedCustomer.CustomerId <= 0)
                    {
                        var vmMissing = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Customer Required", "Customer id is required by API. Please reselect the customer from the list after it is created, then try again.");
                        var dlgMissing = new POS_UI.View.StatusDialog { DataContext = vmMissing };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlgMissing, "AddItemDialogHost");
                        IsPlacingOrder = false;
                        return;
                    }
                }

                // Split-payment dialog already charged each part; do not run a second full-amount card transaction.
                var hasPendingSplitPayments = PendingSplitPayments != null && PendingSplitPayments.Count > 0;

                // Check if card payment is selected and validate card machine availability
                if (SelectedPaymentMethod == PaymentMethod.Card && !hasPendingSplitPayments)
                {
                    var cardMachines = CardMachineService.Instance.CardMachines;
                    var availableCardMachines = cardMachines.Where(cm => cm.IsActive).ToList();
                    
                    if (!availableCardMachines.Any())
                    {
                        //MessageBox.Show("No active card machines available. Please activate a card machine in settings or select cash payment.", 
                          //  "Card Machine Not Available", MessageBoxButton.OK, MessageBoxImage.Warning);
                        var vm1 = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Card Machine Not Available", "No active card machines available. Please activate a card machine in settings or select cash payment.");
                        var dlg1 = new POS_UI.View.StatusDialog { DataContext = vm1 };
                        await MaterialDesignThemes.Wpf.DialogHost.Show(dlg1, "NestedModifiersDialogHost");
                        IsPlacingOrder = false; // release lock
                        return; // Stay in checkout dialog, don't proceed
                    }
                    
                    // Check if any card machine has valid auth token
                    var machinesWithAuth = availableCardMachines.Where(cm => !string.IsNullOrEmpty(cm.AuthToken)).ToList();
                    if (!machinesWithAuth.Any())
                    {
                        //MessageBox.Show("No card machines are properly paired. Please pair a card machine in settings or select cash payment.", 
                         //   "Card Machine Not Paired", MessageBoxButton.OK, MessageBoxImage.Warning);
                        var vm1 = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Card Machine Not Paired", "No card machines are properly paired. Please pair a card machine in settings or select cash payment.");
                        var dlg1 = new POS_UI.View.StatusDialog { DataContext = vm1 };
                        await MaterialDesignThemes.Wpf.DialogHost.Show(dlg1, "NestedModifiersDialogHost");
                        IsPlacingOrder = false; // release lock
                        return; // Stay in checkout dialog, don't proceed
                    }

                    // Process card payment transaction with loading dialog
                    var cardTransactionResult = await ProcessCardPaymentWithLoadingAsync(machinesWithAuth.First(), SubTotal, DisplayOrderId);
                    
                    if (cardTransactionResult == null)
                    {
                        // User cancelled the transaction - show checkout dialog again
                        OnPropertyChanged(nameof(CheckoutPrimaryButtonText));
                        var checkoutDialog = new POS_UI.View.CheckoutDialog { DataContext = this };
                        MaterialDesignThemes.Wpf.DialogHost.Show(checkoutDialog, "AddItemDialogHost");
                        IsPlacingOrder = false; // release lock
                        return; // Don't proceed with order
                    }
                    
                    if (!cardTransactionResult.IsSuccess)
                    {
                        if (cardTransactionResult.IsCancelled)
                        {
                            //MessageBox.Show("Card payment was cancelled by the user.",  "Payment Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                            var vm1 = POS_UI.ViewModels.StatusDialogViewModel.CreateInfo("Payment Cancelled", "Card payment was cancelled by the user.");
                            var dlg1 = new POS_UI.View.StatusDialog { DataContext = vm1 };
                            MaterialDesignThemes.Wpf.DialogHost.Show(dlg1, "AddItemDialogHost");
                        }
                        else
                        {
                            //MessageBox.Show($"Card payment failed: {cardTransactionResult.ErrorMessage}","Payment Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                            var vm1 = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Payment Failed", $"Card payment failed: {cardTransactionResult.ErrorMessage}");
                            var dlg1 = new POS_UI.View.StatusDialog { DataContext = vm1 };
                            MaterialDesignThemes.Wpf.DialogHost.Show(dlg1, "AddItemDialogHost");
                        }
                        
                        // Show checkout dialog again after error
                        OnPropertyChanged(nameof(CheckoutPrimaryButtonText));
                        var checkoutDialog = new POS_UI.View.CheckoutDialog { DataContext = this };
                        MaterialDesignThemes.Wpf.DialogHost.Show(checkoutDialog, "AddItemDialogHost");
                        IsPlacingOrder = false; // release lock
                        return; // Don't proceed with order
                    }
                    
                    // Card payment successful - show success dialog
                    //MessageBox.Show($"Card payment successful!\n\nCard: {cardTransactionResult.CardPan}\nScheme: {cardTransactionResult.CardScheme}", 
                        //"Payment Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateInfo("Payment Successful", $"Card payment successful!\n\nCard: {cardTransactionResult.CardPan}\nScheme: {cardTransactionResult.CardScheme}");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    
                    // Store transaction details for the order
                    _lastCardTransactionResult = cardTransactionResult;
                }

                // Check if cash payment is selected and validate cash amount
                /*if (SelectedPaymentMethod == PaymentMethod.Cash)
                {
                    if (string.IsNullOrWhiteSpace(CashInputString))
                    {
                        var vmCash = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Cash Amount Required", "Please enter the cash amount before placing the order.");
                        var dlgCash = new POS_UI.View.StatusDialog { DataContext = vmCash };
                        await MaterialDesignThemes.Wpf.DialogHost.Show(dlgCash, "NestedModifiersDialogHost");
                        IsPlacingOrder = false; // release lock
                        return; // Stay in checkout dialog, don't proceed
                    }
                }
                */
                string shippingMethod = OrderType switch
                {
                    "Take Away" => "TAKEAWAY",
                    "Dine In" => "DINE-IN",
                    "Delivery" => "DELIVERY",
                    _ => throw new Exception("Invalid OrderType")
                };

                // Build payments list
                List<PaymentModel> paymentModels;
                if ((OrderType == "Dine In" && SelectedPaymentMethod == PaymentMethod.PAY_LATER) || (OrderType == "Delivery" && SelectedPaymentMethod == PaymentMethod.COD) || (OrderType == "Take Away" && SelectedPaymentMethod == PaymentMethod.COT))
                {
                    // For Dine In with PAY_LATER, COD Delivery, and COT Take Away orders, send empty payments array
                    // Dine In PAY_LATER: payments collected later at the table
                    // COD Delivery: payments collected later when order is delivered
                    // COT Take Away: payments collected on takeaway (collection)
                    paymentModels = new List<PaymentModel>();
                }
                else if (hasPendingSplitPayments)
                {
                    paymentModels = SplitPaymentItem.ToPaymentModelsForOrder(PendingSplitPayments);
                }
                else
                {
                    var pmUpper = SelectedPaymentMethod.ToString().ToUpper();
                    var isManualCard = pmUpper == "MANUALCARD";
                    var isCard = pmUpper == "CARD";
                    var apiPaymentMethod = isManualCard || isCard ? "CARD" : pmUpper;
                    var transactionId = pmUpper == "CASH" ? "" : (isManualCard ? "manualcard" : (_lastCardTransactionResult?.RetrievalReferenceNumber ?? "trc001"));

                    var payments = new[]
                    {
                        new {
                            payment_method = apiPaymentMethod,
                            // Backend requires paying_amount for both CASH and CARD
                            // Round to 2 decimal places to match currency precision
                            paying_amount = Math.Round(_cartService.EffectiveTotalForPayment, 2, MidpointRounding.AwayFromZero),
                            transaction_id = transactionId,
                            cash = pmUpper == "CASH" || isManualCard == true ? CashGiven : 0.0m,
                            balance = pmUpper == "CASH" || isManualCard == true ? Math.Max(CashBalance, 0.0m) : 0.0m,
                            auth_code = apiPaymentMethod == "CARD" ? (_lastCardTransactionResult?.AuthorisationCode ?? "") : "",
                            card_pan = apiPaymentMethod == "CARD" ? (_lastCardTransactionResult?.CardPan ?? "") : "",
                            card_scheme = apiPaymentMethod == "CARD" ? (_lastCardTransactionResult?.CardScheme ?? "") : "",
                            uti = apiPaymentMethod == "CARD" ? (_lastCardTransactionResult?.Uti ?? "") : ""
                        }
                    };

                    paymentModels = payments.Select(p => new PaymentModel
                    {
                        PaymentMethod = ((dynamic)p).payment_method,
                        PayingAmount = ((dynamic)p).paying_amount,
                        Cash = ((dynamic)p).cash,
                        Balance = ((dynamic)p).balance,
                        AuthCode = ((dynamic)p).auth_code,
                        CardPan = ((dynamic)p).card_pan,
                        CardScheme = ((dynamic)p).card_scheme,
                        TransactionId = ((dynamic)p).transaction_id
                    }).ToList();
                }


                // Create order model using the enhanced approach
                            var orderModel = _cartService.CreateOrderModel(DisplayOrderId, SelectedCustomer, DiscountPercent, SelectedAddress);
                orderModel.ShippingMethod = shippingMethod;
                orderModel.TableId = OrderType == "Dine In" ? (SelectedTable?.ApiId ?? 0) : 0;
                //orderModel.TableId = 0;
                orderModel.Payments = paymentModels;

                // FINISH FLOW: only update payment on existing order (no new placement)
                if (POS_UI.Services.GlobalDataService.Instance.IsFinishFlow && _cartService.CurrentOrderApiId.HasValue)
                {
                    try
                    {
                        // First, push latest order totals/discounts to API before payment
                        if (orderModel.ApiId <= 0 && _cartService.CurrentOrderApiId.HasValue)
                        {
                            orderModel.ApiId = _cartService.CurrentOrderApiId.Value;
                        }
                        var updatedOk = await orderModel.UpdateOrderAsync(_apiService);
                        if (!updatedOk)
                        {
                            MessageBox.Show("Failed to update order details before payment.", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            IsPlacingOrder = false;
                            return;
                        }

                        // Build payment list specifically for finish flow (always include selected payment)
                        var pmUpperFinish = SelectedPaymentMethod.ToString().ToUpper();
                        var isManualCardFinish = pmUpperFinish == "MANUALCARD";
                        var isTerminalCardFinish = pmUpperFinish == "CARD";
                        var isCashFinish = pmUpperFinish == "CASH";
                        var apiPaymentMethodFinish = (isManualCardFinish || isTerminalCardFinish) ? "CARD" : "CASH";
                        var transactionIdFinish = isCashFinish ? string.Empty : (isManualCardFinish ? "manualcard" : (_lastCardTransactionResult?.RetrievalReferenceNumber ?? string.Empty));

                        List<PaymentModel> finishPayments;
                        if (hasPendingSplitPayments)
                        {
                            finishPayments = SplitPaymentItem.ToPaymentModelsForOrder(PendingSplitPayments);
                        }
                        else
                        {
                            finishPayments = new List<PaymentModel>
                            {
                                new PaymentModel
                                {
                                    PaymentMethod = apiPaymentMethodFinish,
                                    // Use current cart subtotal to include latest discounts/coupons
                                    PayingAmount = _cartService.SubTotal,
                                    Cash = isCashFinish ? CashGiven : 0.0m,
                                    Balance = isCashFinish ? Math.Max(CashBalance, 0.0m) : 0.0m,
                                    TransactionId = transactionIdFinish,
                                    AuthCode = isTerminalCardFinish ? _lastCardTransactionResult?.AuthorisationCode : string.Empty,
                                    CardPan = isTerminalCardFinish ? _lastCardTransactionResult?.CardPan : string.Empty,
                                    CardScheme = isTerminalCardFinish ? _lastCardTransactionResult?.CardScheme : string.Empty
                                }
                            };
                        }

                        // Persist last cash payment context for Finish flow receipt printing
                        try
                        {
                            if (isCashFinish)
                            {
                                POS_UI.Services.GlobalDataService.Instance.LastCashGiven = CashGiven;
                                POS_UI.Services.GlobalDataService.Instance.LastCashBalance = Math.Max(CashBalance, 0.0m);
                            }
                            else
                            {
                                POS_UI.Services.GlobalDataService.Instance.LastCashGiven = null;
                                POS_UI.Services.GlobalDataService.Instance.LastCashBalance = null;
                            }
                        }
                        catch { }

                        await _apiService.UpdateOrderPaymentAsync(_cartService.CurrentOrderApiId.Value, finishPayments);
                        POS_UI.Services.GlobalDataService.Instance.IsFinishFlow = false; // reset flag after success

                        // Print main receipt on Finish flow for Dine In if enabled
                        try
                        {
                            if (OrderType == "Dine In")
                            {
                                var receiptPaymentMethod = hasPendingSplitPayments && PendingSplitPayments != null && PendingSplitPayments.Count > 0
                                    ? SplitPaymentItem.FormatReceiptPaymentSummary(PendingSplitPayments, GlobalDataService.Instance.ShopDetails?.Currency ?? "£")
                                    : SelectedPaymentMethod.ToString();
                                await ReceiptPrintingService.Instance.PrintMainReceiptAsync(_cartService, _lastCardTransactionResult, receiptPaymentMethod);
                            }
                        }
                        catch { /* non-fatal printing error */ }

                        // Close checkout dialog
                        if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("AddItemDialogHost"))
                        {
                            MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", true);
                            await Task.Delay(100);
                        }

                        //MessageBox.Show("Payment updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        try
                        {
                            var isCashPayment = string.Equals(SelectedPaymentMethod.ToString(), "Cash", System.StringComparison.OrdinalIgnoreCase);
                            var successVm = isCashPayment
                            ? POS_UI.ViewModels.StatusDialogViewModel.CreateCompletedPaymentSuccess("Payment Successful", CashGiven, _cartService.SubTotal, Math.Max(CashBalance, 0.0m), DisplayOrderId)
                            : POS_UI.ViewModels.StatusDialogViewModel.CreateSuccess("Payment Successful", $"Dine-in order {DisplayOrderId} completed");
                            // Open cash drawer immediately upon API success, before displaying the success dialog
                            if (isCashPayment)
                            {
                                try { OpenCashDrawer(); } catch { }
                            }
                            var dlg = new POS_UI.View.StatusDialog { DataContext = successVm };
                            await MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "NestedModifiersDialogHost");
                        }
                        catch { /* If dialog host is busy, ignore */ }

                        // Reset cart to new order mode
                        _cartService.ClearCart();
                        PendingSplitPayments = null;
                        _cartService.ResetCustomerHistory();
                        IsOrderLoadedForEdit = false;
                        IsCartEditable = true;
                        _originalItemIds.Clear();
                        _originalItemQuantities.Clear();
                        _wasKitchenLockedAtLoad = false;
                        _lockedLocalItemKeys.Clear();
                        POS_UI.Services.GlobalDataService.Instance.IsFinishFlow = false;
                        SelectedCustomer = FindOrCreateGuestCustomer();
                        OrderType = "Take Away";
                        SelectedTable = null;
                        SelectedOrderTime = null;
                        DisplayOrderId = GenerateOrderId();
                        await PersistDisplayOrderIdToOrderConfigAsync(DisplayOrderId).ConfigureAwait(true);
                        Note = null;
                        DiscountAmount = 0;
                        DiscountPercent = 0;
                        CouponCode = null;
                        CouponDescription = null;
                        CouponAmount = 0;
                        DeliveryCharge = 0;
                        ClearCashGiven();
                        UpdateEstimatedPickupTime();
                        OnPropertyChanged(nameof(ShowSavePlaceOrderButtons));
                        OnPropertyChanged(nameof(ShowUpdateOrderButton));
                        OnPropertyChanged(nameof(ShowFinishButtons));
                        OnPropertyChanged(nameof(CheckoutPrimaryButtonText));
                        OnPropertyChanged(nameof(SelectedCustomer));
                        
                    }
                    catch (Exception ex)
                    {
                        // Keep reference to your original dialog (the one you want to reopen later)
                        /*var originalDialog = new POS_UI.View.AddItemDialog
                        {
                            DataContext = this  // <-- pass back the original VM
                        };*/

                        string jsonPart = ex.Message;
                        int jsonStart = ex.Message.IndexOf("{");
                        if (jsonStart >= 0)
                        {
                            jsonPart = ex.Message.Substring(jsonStart);
                        }
                        
                        // Try to parse the error response as JSON using dynamic parsing
                        var jsonObject = JsonConvert.DeserializeObject<dynamic>(jsonPart);
                        string header = jsonObject?.message ?? "Payment Error";
                        string errorDetails = "An unexpected error occurred. Please try again.";
                        
                        // Handle different error formats
                        if (jsonObject?.errors != null)
                        {
                            if (jsonObject.errors is Newtonsoft.Json.Linq.JObject)
                            {
                                // Errors is an object/dictionary
                                var errorsDict = jsonObject.errors.ToObject<Dictionary<string, string>>();
                                if (errorsDict != null && errorsDict.Count > 0)
                                {
                                    errorDetails = string.Join("\n", errorsDict.Values);
                                }
                            }
                            else if (jsonObject.errors is Newtonsoft.Json.Linq.JValue)
                            {
                                // Errors is a JValue (primitive value)
                                errorDetails = jsonObject.errors.ToString();
                            }
                            /*else if (jsonObject.errors is string)
                            {
                                // Errors is a string
                                errorDetails = jsonObject.errors.ToString();
                            }
                            else if (jsonObject.errors is Newtonsoft.Json.Linq.JArray)
                            {
                                // Errors is an array
                                var errorsArray = jsonObject.errors.ToObject<string[]>();
                                if (errorsArray != null && errorsArray.Length > 0)
                                {
                                    errorDetails = string.Join("\n", errorsArray);
                                }
                            }*/
                        }

                        string friendlyText = Regex.Replace(errorDetails, "(\\B[A-Z])", " $1");
                        
                        // Use the API message as header and errors as message
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError(header, friendlyText);
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        
                
                        //var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Payment Error", $"Failed to update payment: {ex.Message}");
                        //var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "NestedModifiersDialogHost");

                        //MessageBox.Show($"Failed to update payment: {ex.Message}", "Payment Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        /*var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Payment Error", $"Failed to update payment: {ex.Message}");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        // Close the current dialog first, then show the error message
                        Application.Current.Dispatcher.Invoke(() =>
                        {
					if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("AddItemDialogHost"))
                        {
                            MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", null);
					}
                        });
                        
                        // Wait a moment for the dialog to close, then show error message
                        await Task.Delay(100);
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");*/
                        // After error dialog is closed by user → reopen the original dialog
                        //await MaterialDesignThemes.Wpf.DialogHost.Show(originalDialog, "AddItemDialogHost");
                        IsPlacingOrder = false;
                        return;
                    }

                    IsPlacingOrder = false;
                    return; // stop normal placement flow
                }

                // Place order using OrderModel
                string result;
                try
                {
                    result = await orderModel.PlaceOrderAsync(_apiService);

                    // On successful API placement for Dine In, save locally for modification tracking
                    if (OrderType == "Dine In" && !string.IsNullOrWhiteSpace(DisplayOrderId))
                    {
                        try
                        {
                            await Services.DineInOrderService.Instance.CreateDineInOrderFromCartAsync(DisplayOrderId);
                        }
                        catch { /* non-fatal */ }
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        // Extract JSON from the exception message
                        string jsonPart = ex.Message;
                        int jsonStart = ex.Message.IndexOf("{");
                        if (jsonStart >= 0)
                        {
                            jsonPart = ex.Message.Substring(jsonStart);
                        }
                        
                        // Try to parse the error response as JSON using dynamic parsing
                        var jsonObject = JsonConvert.DeserializeObject<dynamic>(jsonPart);
                        string header = jsonObject?.message ?? "Order Placement Failed";
                        string errorDetails = "An unexpected error occurred. Please try again.";
                        
                        // Handle different error formats
                        if (jsonObject?.errors != null)
                        {
                            if (jsonObject.errors is Newtonsoft.Json.Linq.JObject)
                            {
                                // Errors is an object/dictionary
                                var errorsDict = jsonObject.errors.ToObject<Dictionary<string, string>>();
                                if (errorsDict != null && errorsDict.Count > 0)
                                {
                                    errorDetails = string.Join("\n", errorsDict.Values);
                                }
                            }
                            else if (jsonObject.errors is Newtonsoft.Json.Linq.JValue)
                            {
                                // Errors is a JValue (primitive value)
                                errorDetails = jsonObject.errors.ToString();
                            }
                            else if (jsonObject.errors is string)
                            {
                                // Errors is a string
                                errorDetails = jsonObject.errors.ToString();
                            }
                            else if (jsonObject.errors is Newtonsoft.Json.Linq.JArray)
                            {
                                // Errors is an array
                                var errorsArray = jsonObject.errors.ToObject<string[]>();
                                if (errorsArray != null && errorsArray.Length > 0)
                                {
                                    errorDetails = string.Join("\n", errorsArray);
                                }
                            }
                        }

                        string friendlyText = Regex.Replace(errorDetails, "(\\B[A-Z])", " $1");
                        
                        // Use the API message as header and errors as message
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError(header, friendlyText);
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        
                        // Capture whether checkout dialog was open
                        //bool reopenCheckout = MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("AddItemDialogHost");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("AddItemDialogHost"))
                                {
                                    MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", null);
                                }
                            }
                            catch { /* ignore race: dialog may not be open */ }
                        });
                        
                        // Wait a moment for the dialog to close, then show error message
                        await Task.Delay(100);
                        await MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    }
                    catch (JsonException)
                    {
                        // If JSON parsing fails, fall back to original error handling
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Order Placement Failed", $"Order placement failed: {ex.Message}");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        
                    // Capture whether checkout dialog was open
                    //bool reopenCheckout = MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("AddItemDialogHost");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("AddItemDialogHost"))
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", null);
                            }
                        }
                        catch { /* ignore race: dialog may not be open */ }
                    });
                    
                    // Wait a moment for the dialog to close, then show error message
                    await Task.Delay(100);
                    await MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    }
                    
                    // After error dialog closes, reopen checkout dialog if it was open before
                    /*if (reopenCheckout)
                    {
                        //OnPropertyChanged(nameof(CheckoutPrimaryButtonText));
                        var checkoutDialog = new POS_UI.View.CheckoutDialog { DataContext = this };
                        await MaterialDesignThemes.Wpf.DialogHost.Show(checkoutDialog, "AddItemDialogHost");
                    }*/
                    
                    IsPlacingOrder = false; // release lock
                    return; // Don't proceed with order
                }
                
                // Set the order ID, creation time, pickup time, and table number in cart service for receipt printing
                _cartService.DisplayOrderId = DisplayOrderId;
                _cartService.OrderCreatedAt = DateTime.Now;
                
                // Set table number for dine-in orders
                if (OrderType == "Dine In" && SelectedTable != null)
                {
                    _cartService.TableNumber = SelectedTable.TableNumber;
                }
                
                // Calculate the correct pickup time: use SelectedOrderTime if set, otherwise use calculated prep time
                DateTime pickupTime;
                if (SelectedOrderTime.HasValue)
                {
                    pickupTime = SelectedOrderTime.Value;
                    Console.WriteLine($"Using SelectedOrderTime for pickup: {pickupTime:HH:mm}");
                }
                else if ((OrderType == "Take Away" || OrderType == "Delivery"))
                {
                    // Use the calculated prep time (same logic as TimeButtonText)
                    var shopDetails = GlobalDataService.Instance.ShopDetails;
                    if (shopDetails?.DeliveryPlatform?.PrepTime > 0)
                    {
                        pickupTime = DateTime.Now.AddMinutes(shopDetails.DeliveryPlatform.PrepTime);
                        Console.WriteLine($"Using calculated prep time for pickup: {pickupTime:HH:mm} (PrepTime: {shopDetails.DeliveryPlatform.PrepTime} minutes)");
                    }
                    else
                    {
                        pickupTime = DateTime.Now;
                        Console.WriteLine($"Using current time for pickup (no PrepTime): {pickupTime:HH:mm}");
                    }
                }
                else
                {
                    pickupTime = DateTime.Now;
                    Console.WriteLine($"Using current time for pickup (Dine In): {pickupTime:HH:mm}");
                }
                
                _cartService.PickupTime = pickupTime;
                
                // Print receipts per order type and settings
                try
                {
                    string paymentMethod;
                    if (PendingSplitPayments != null && PendingSplitPayments.Count > 0)
                    {
                        var currency = GlobalDataService.Instance.ShopDetails?.Currency ?? "£";
                        paymentMethod = SplitPaymentItem.FormatReceiptPaymentSummary(PendingSplitPayments, currency);
                    }
                    else
                    {
                        paymentMethod = SelectedPaymentMethod switch
                        {
                            PaymentMethod.Card => "CARD",
                            PaymentMethod.ManualCard => "CARD",
                            PaymentMethod.COD => "COD",
                            PaymentMethod.COT => "COT",
                            PaymentMethod.PAY_LATER => "PAY_LATER",
                            _ => "CASH"
                        };
                    }
                    if (OrderType == "Dine In")
                    {
                        // For Dine In: 
                        // - If paid immediately (not Pay Later): print both kitchen receipt AND main receipt
                        // - If Pay Later: print only kitchen receipt (main receipt will be printed at Finish flow)
                        
                        // Always print kitchen receipt
                        await ReceiptPrintingService.Instance.PrintKitchenReceiptAsync(_cartService);
                        
                        // Print main receipt only if payment is made immediately (not Pay Later)
                        if (SelectedPaymentMethod != PaymentMethod.PAY_LATER)
                        {
                            // Persist cash payment context for receipt printing
                            try
                            {
                                if (paymentMethod == "CASH")
                                {
                                    POS_UI.Services.GlobalDataService.Instance.LastCashGiven = CashGiven;
                                    POS_UI.Services.GlobalDataService.Instance.LastCashBalance = Math.Max(CashBalance, 0.0m);
                                }
                                else
                                {
                                    POS_UI.Services.GlobalDataService.Instance.LastCashGiven = null;
                                    POS_UI.Services.GlobalDataService.Instance.LastCashBalance = null;
                                }
                            }
                            catch { }
                            
                            await ReceiptPrintingService.Instance.PrintMainReceiptAsync(_cartService, _lastCardTransactionResult, paymentMethod);
                        }
                    }
                    else
                    {
                        // Take Away / Delivery: if both enabled, print both; otherwise print what is enabled
                        // Persist last cash payment context for receipt printing
                        try
                        {
                            if (paymentMethod == "CASH")
                            {
                                POS_UI.Services.GlobalDataService.Instance.LastCashGiven = CashGiven;
                                POS_UI.Services.GlobalDataService.Instance.LastCashBalance = Math.Max(CashBalance, 0.0m);
                            }
                            else
                            {
                                POS_UI.Services.GlobalDataService.Instance.LastCashGiven = null;
                                POS_UI.Services.GlobalDataService.Instance.LastCashBalance = null;
                            }
                        }
                        catch { }
                        
                        // Pass current CartService directly to receipt methods to ensure removed fees are respected
                        await ReceiptPrintingService.Instance.PrintMainReceiptAsync(_cartService, _lastCardTransactionResult, paymentMethod);
                        await ReceiptPrintingService.Instance.PrintKitchenReceiptAsync(_cartService);
                    }
                }
                catch (Exception ex)
                {
                    // Don't show error to user as order was successful
                }
                
                // Close the checkout dialog only if it's open
                if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("AddItemDialogHost"))
                {
                    MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", true);
                }
               
                // Store data needed for success modal before clearing cart
                var orderIdForModal = DisplayOrderId;
                var cashGivenForModal = CashGiven;
                var subTotalForModal = SubTotal;
                var cashBalanceForModal = Math.Max(CashBalance, 0.0m);
                var orderTypeForModal = OrderType;
                var paymentMethodForModal = SelectedPaymentMethod;

                DisplayOrderId = GenerateOrderId();
                await PersistDisplayOrderIdToOrderConfigAsync(DisplayOrderId).ConfigureAwait(true);
                _cartService.ClearCart();
                _cartService.ResetCustomerHistory();
                IsOrderLoadedForEdit = false;
                IsCartEditable = true;
                _originalItemIds.Clear();
                _originalItemQuantities.Clear();
                _wasKitchenLockedAtLoad = false;
                _lockedLocalItemKeys.Clear();
                POS_UI.Services.GlobalDataService.Instance.IsFinishFlow = false;
                // Load the first customer again for the next order
                try
                {
                    if (Customers != null && Customers.Count > 0)
                    {
                        SelectedCustomer = Customers.FirstOrDefault();
                        OnPropertyChanged(nameof(SelectedCustomer));
                        _cartService.CustomerName = ($"{SelectedCustomer?.FirstName} {SelectedCustomer?.LastName}").Trim();
                        _cartService.CustomerPhone = SelectedCustomer?.Phone;
                    }
                }
                catch { }
                Note = null;
                OnPropertyChanged(nameof(Note));
                OnPropertyChanged(nameof(CanAddNote));
                DiscountAmount = 0;
                DiscountPercent = 0;
                CouponCode = null;
                CouponDescription = null;
                CouponAmount = 0;
                DeliveryCharge = 0;
                ClearCashGiven();
                PendingSplitPayments = null;

                // Reset payment method to Card for next order
                SelectedPaymentMethod = PaymentMethod.Card;
                OnPropertyChanged(nameof(SelectedPaymentMethod));
                OnPropertyChanged(nameof(IsCashPaymentSelected));
                OnPropertyChanged(nameof(IsCODPaymentSelected));
                
                // Reset timer to current time + prep time for next order
                UpdateEstimatedPickupTime();
                
                OnPropertyChanged(nameof(DiscountAmount));
                OnPropertyChanged(nameof(DiscountDescription));
                OnPropertyChanged(nameof(DiscountPercent));
                OnPropertyChanged(nameof(HasDiscount));
                OnPropertyChanged(nameof(CouponCode));
                OnPropertyChanged(nameof(CouponDescription));
                OnPropertyChanged(nameof(CouponAmount));    
                OnPropertyChanged(nameof(HasCoupon));
                OnPropertyChanged(nameof(DeliveryCharge));
                OnPropertyChanged(nameof(CanAddCoupon));

                // Show success confirmation for all order types using stored data
                if (orderTypeForModal == "Dine In" || orderTypeForModal == "Take Away" || orderTypeForModal == "Delivery")
                {
                    try
                    {
                        string orderKind = orderTypeForModal == "Dine In" ? "Dine-in" : (orderTypeForModal == "Take Away" ? "Takeaway" : "Delivery");
                        // If payment method is Cash, show cash breakdown success dialog
                        var isCashPayment = string.Equals(paymentMethodForModal.ToString(), "Cash", System.StringComparison.OrdinalIgnoreCase) && (orderTypeForModal != "Dine In");
                        var successVm = isCashPayment
                            ? POS_UI.ViewModels.StatusDialogViewModel.CreateCashSuccess("Order Placed", cashGivenForModal, subTotalForModal, cashBalanceForModal, orderKind, orderIdForModal)
                            : POS_UI.ViewModels.StatusDialogViewModel.CreateSuccess("Order Placed", $"{orderKind} order {orderIdForModal} has been placed successfully.");
                        var successDlg = new POS_UI.View.StatusDialog { DataContext = successVm };
                        // Ensure any open dialogs are closed before showing success
                        if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("NestedModifiersDialogHost"))
                        {
                            MaterialDesignThemes.Wpf.DialogHost.Close("NestedModifiersDialogHost");
                        }
                        if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("AddItemDialogHost"))
                        {
                            MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", true);
                            await Task.Delay(100);
                        }
                        // Open cash drawer for any cash payment (including Dine In)
                        var isCashPaymentForDrawer = string.Equals(paymentMethodForModal.ToString(), "Cash", System.StringComparison.OrdinalIgnoreCase);
                        if (isCashPaymentForDrawer)
                        {
                            try { OpenCashDrawer(); } catch { }
                        }
                        // If this order was created from a draft, keep other drafts and just reset flags
                        try
                        {
                            if (_isDraftLoadedIntoCart)
                            {
                                _isDraftLoadedIntoCart = false;
                                _activeLoadedDraft = null;
                                // Persist current drafts unchanged
                                _draftStorageService.SaveDrafts(DraftOrders);
                                OnPropertyChanged(nameof(DraftCount));
                            }
                        }
                        catch { }

                        await MaterialDesignThemes.Wpf.DialogHost.Show(successDlg, "AddItemDialogHost");
                        if (orderTypeForModal == "Dine In")
                        {
                        SelectedTable = null; // setter updates _cartService.TableNumber/TableName
                        }
                    }
                    catch { /* non-fatal */ }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error placing order: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Placing Order", $"Error placing order: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", null);
                });
                
                // Wait a moment for the dialog to close, then show error message
                await Task.Delay(100);
                //MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                IsPlacingOrder = false; // release lock
            }
            finally
            {
                IsPlacingOrder = false;
            }
        }

        public void LoadOrder(POS_UI.Models.OrderModel order)
        {
            // Clear ViewModel discount values before loading new order
            DiscountPercent = 0;
            
            // Use the new enhanced approach to load order into cart
            _suppressCartInteractionFlag = true;
            _cartService.LoadFromOrderModel(order);
            RehydrateLoadedOrderItemsWithCatalog();
            
            // Restore order-level discount percentage if it exists
            if (order.DiscountPercentage > 0)
            {
                DiscountPercent = order.DiscountPercentage;
            }
            // DiscountPercent is already 0 if no percentage discount
                
                // Handle discount mode - if it's amount-based, we don't set DiscountPercent
                // The discount amount will be handled separately in the cart
            
            // Prevent background customer loading from overriding this loaded order's customer
            // If customers list is not yet loaded, defer but do not auto-select a different customer
            // Keep the customer name/phone from the order until the user explicitly changes it

            // Force UI to reflect API-provided totals and discounts
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(Discount));
            OnPropertyChanged(nameof(DiscountAmount));
            OnPropertyChanged(nameof(HasDiscount));
            OnPropertyChanged(nameof(DiscountDescription));
            OnPropertyChanged(nameof(DiscountPercent));
            OnPropertyChanged(nameof(VoucherDiscount));
            OnPropertyChanged(nameof(HasVoucherDiscount));
            OnPropertyChanged(nameof(CouponCode));
            OnPropertyChanged(nameof(CouponAmount));
            OnPropertyChanged(nameof(HasCoupon));
            OnPropertyChanged(nameof(CouponDiscount));
            OnPropertyChanged(nameof(CouponDescription));
            OnPropertyChanged(nameof(CouponDescriptionWithAmount));
            OnPropertyChanged(nameof(Note));
            OnPropertyChanged(nameof(HasNote));
            
            // Set flag to indicate this order is loaded for editing
            IsOrderLoadedForEdit = true;

            // Track original items loaded from order
            _originalItemIds.Clear();
            _originalItemQuantities.Clear();
            foreach (var item in order.Items)
            {
                _originalItemIds.Add(item.Id);
                _originalItemQuantities[item.Id] = Math.Max(1, item.Quantity);
            }

            // Ensure category snapshot is available for grouping/printing (e.g., starters) during modify flow
            try
            {
                foreach (var ci in OrderItems)
                {
                    if (string.IsNullOrWhiteSpace(ci.CategoryName))
                    {
                        string resolved = ci.Product?.Category;
                        if (string.IsNullOrWhiteSpace(resolved))
                        {
                            // Try resolve from known product catalog by id or name
                            var match = AllProducts?.FirstOrDefault(p =>
                                (ci.Product != null && p.Id == ci.Product.Id) ||
                                string.Equals(p.ItemName, ci.Product?.ItemName ?? ci.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                            if (match != null)
                            {
                                resolved = match.Category;
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(resolved))
                        {
                            ci.CategoryName = resolved;
                        }
                    }
                }
            }
            catch { }

            // Reset interaction flag after load
            _hasCartInteractionSinceLoad = false;
            _suppressCartInteractionFlag = false;

            // Lock cart editability for kitchen-progressing orders
            // Use API raw status string when available (QUEUE, ACCEPTED, PREPARING, READY, SERVED, COMPLETED)
            var isKitchenLockedNow = IsKitchenLockedStatus(order.ApiStatus);
            IsCartEditable = !isKitchenLockedNow;
            _wasKitchenLockedAtLoad = isKitchenLockedNow && !POS_UI.Services.GlobalDataService.Instance.IsFinishFlow;

            // If the order is already in kitchen-progress (PREPARING/READY/SERVED), lock all originally loaded items.
            // Only items added after this point should be editable.
            if (_wasKitchenLockedAtLoad)
            {
                foreach (var existingItem in OrderItems)
                {
                    existingItem.IsReadOnly = true;
                }
            }

            // If arrived via Finish flow from Kitchen, keep cart in locked mode but only lock existing items
            if (POS_UI.Services.GlobalDataService.Instance.IsFinishFlow)
            {
                // In finish flow, we want only the already-loaded items to be non-editable,
                // while still allowing adding new items and applying coupon/discount/notes.
                IsCartEditable = false;
                foreach (var existingItem in OrderItems)
                {
                    existingItem.IsReadOnly = true;
                }
            }
            
            // Set the DisplayOrderId from the loaded order (this is the key fix)
            DisplayOrderId = order.OrderNumber ?? order.DisplayOrderId;

            // Persist order-level kitchen status to local dine-in items so JSON reflects PREPARE/READY/SERVED
            if (OrderType == "Dine In" && !string.IsNullOrWhiteSpace(DisplayOrderId))
            {
                var s = (order.ApiStatus ?? string.Empty).Trim().ToUpperInvariant();
                string localItemStatus = null;
                if (s == "PREPARING") localItemStatus = POS_UI.Models.DineInOrderItemStatus.PREPARE;
                else if (s == "READY") localItemStatus = POS_UI.Models.DineInOrderItemStatus.READY;
                else if (s == "SERVED" || s == "DELIVERED") localItemStatus = POS_UI.Models.DineInOrderItemStatus.SERVED;
                if (!string.IsNullOrEmpty(localItemStatus))
                {
                    // Persist, then overlay
                    _ = Task.Run(async () =>
                    {
                        await POS_UI.Services.DineInOrderService.Instance.UpdateAllItemsStatusAsync(DisplayOrderId, localItemStatus);
                        await RefreshLocalOverlayAsync();
                    });
                }
            }

            // For Dine In modifications, overlay local per-item statuses (QUEUE/PREPARE/READY/SERVED)
            // and mark non-QUEUE items as read-only. Skip this during Finish flow to preserve
            // the per-item read-only flags applied above to all existing items.
            if (OrderType == "Dine In" && !string.IsNullOrWhiteSpace(DisplayOrderId)
                && !POS_UI.Services.GlobalDataService.Instance.IsFinishFlow)
            {
                _ = RefreshLocalOverlayAsync();
            }

            if (!string.IsNullOrWhiteSpace(DisplayOrderId) && OrderItems.Count > 0)
                _ = ApplyChargedSplitPaymentReadOnlyFromApiIfNeededAsync();
            
            // Set order type
            OrderType = order.OrderType switch
            {
                Models.OrderType.DineIn => "Dine In",
                Models.OrderType.TakeAway => "Take Away",
                Models.OrderType.Delivery => "Delivery",
                _ => "Take Away"
            };
            
            // Debug: Log the order details
            Console.WriteLine($"LoadOrder Debug:");
            Console.WriteLine($"  Order Type: {order.OrderType}");
            Console.WriteLine($"  Table Number from API: {order.TableNumber}");
            Console.WriteLine($"  Tables collection count: {Tables?.Count ?? 0}");
            
            // Set selected table for dine-in orders; match by table_id from API
            if (order.OrderType == Models.OrderType.DineIn && order.TableNumber.HasValue)
            {
                Console.WriteLine($"  Looking for table with table_id: {order.TableNumber.Value}");
                Console.WriteLine($"  Available tables: {string.Join(", ", Tables?.Select(t => $"ApiId:{t.ApiId}, TableNumber:{t.TableNumber}, Name:{t.Name}") ?? new string[0])}");
                
                // First, try to match by ApiId (table_id from API) - this is the primary matching method
                var foundTable = Tables?.FirstOrDefault(t => t.ApiId == order.TableNumber.Value);
                
                if (foundTable != null)
                {
                    Console.WriteLine($"  Found table by ApiId: {foundTable.Name} (ApiId: {foundTable.ApiId}, TableNumber: {foundTable.TableNumber})");
                    SelectedTable = foundTable;
                }
                else
                {
                    // Fallback: try to match by TableNumber
                    //foundTable = Tables?.FirstOrDefault(t => t.TableNumber == order.TableNumber.Value);
                    foundTable = Tables?.FirstOrDefault(t => t.ApiId == 0);
                    if (foundTable != null)
                    {
                        Console.WriteLine($"  Found table by TableNumber: {foundTable.Name} (ApiId: {foundTable.ApiId}, TableNumber: {foundTable.TableNumber})");
                        SelectedTable = foundTable;
                    }
                    else
                    {
                        Console.WriteLine($"  WARNING: Table with table_id {order.TableNumber.Value} not found in Tables collection!");
                        
                        // Create a temporary table object if not found
                        SelectedTable = new TableModel
                        {
                            // Use table_id from API as both ApiId and TableNumber
                            ApiId = order.TableNumber.Value,
                            TableNumber = order.TableNumber.Value,
                            Name = $"T{order.TableNumber.Value}",
                            Status = TableStatus.Drafted
                        };
                        Console.WriteLine($"  Created temporary table: {SelectedTable.Name} (ApiId: {SelectedTable.ApiId})");
                    }
                }
            }
            else
            {
                SelectedTable = null;
                Console.WriteLine($"  No table selection needed (OrderType: {order.OrderType})");
            }
            
            // Set SelectedCustomer based on the customer name from the order
            if (!string.IsNullOrEmpty(order.CustomerName))
            {
                // Try to find the customer in the existing customers list (prefer by id)
                CustomerModel customer = null;
                if (order.CustomerId.HasValue && order.CustomerId.Value > 0)
                {
                    customer = Customers?.FirstOrDefault(c => c.CustomerId == order.CustomerId.Value);
                }
                if (customer == null)
                {
                    customer = Customers?.FirstOrDefault(c => 
                        (c.FirstName + " " + c.LastName).Trim() == order.CustomerName.Trim() ||
                        c.Phone == order.CustomerPhone);
                }
                
                if (customer != null)
                {
                    SelectedCustomer = customer;
                }
                else
                {
                    // If customer not found in list, create a temporary customer object
                    // This ensures the customer name displays correctly in the UI
                    SelectedCustomer = new CustomerModel
                    {
                        FirstName = order.CustomerName,
                        LastName = "",
                        Phone = order.CustomerPhone ?? "",
                        CustomerId = order.CustomerId ?? 0
                    };
                }
            }
            else
            {
                SelectedCustomer = null;
            }
            
            // Set coupon description based on voucher details if available, otherwise use coupon code
            // CartService.LoadFromOrderModel may have already set CouponDescription from voucher details
            if (string.IsNullOrWhiteSpace(CouponDescription))
            {
                // If CartService didn't set description, try to set it from order
                if (!string.IsNullOrEmpty(order.CouponCode))
                {
                    if (decimal.TryParse(order.CouponCode, out decimal percent) && percent > 0 && percent <= 100)
                    {
                        CouponDescription = $"Coupon ({percent}%)";
                    }
                    else
                    {
                        CouponDescription = $"Coupon ({order.CouponCode})";
                    }
                }
                else if (order.Vouchers != null && order.Vouchers.Count > 0)
                {
                    // Try to get description from first voucher
                    var firstVoucher = order.Vouchers.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.VoucherCode));
                    if (firstVoucher != null)
                    {
                        string couponDescription = $"Coupon ({firstVoucher.VoucherCode})";
                        if (!string.IsNullOrEmpty(firstVoucher.VoucherValue) && decimal.TryParse(firstVoucher.VoucherValue, out decimal voucherValue))
                        {
                            if (firstVoucher.ValueType?.ToLower() == "percentage")
                            {
                                couponDescription = $"Coupon ({firstVoucher.VoucherCode}) - {voucherValue}%";
                            }
                            else
                            {
                                couponDescription = $"Coupon ({firstVoucher.VoucherCode}) - {voucherValue:C}";
                            }
                        }
                        CouponDescription = couponDescription;
                    }
                }
                else
                {
                    CouponDescription = null;
                }
            }
            
            // Update timer based on loaded order
            UpdateEstimatedPickupTime();
            
            // Debug: Log the final state
            Console.WriteLine($"  Final SelectedTable: {SelectedTable?.TableNumber}");
            Console.WriteLine($"  Final TimeButtonText: {TimeButtonText}");
            
            // Trigger property change notifications
            OnPropertyChanged(nameof(OrderItems));
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(CouponCode));
            OnPropertyChanged(nameof(CouponDescription));
            OnPropertyChanged(nameof(CouponAmount));
            OnPropertyChanged(nameof(HasCoupon));
            OnPropertyChanged(nameof(SelectedCustomer)); // Ensure UI updates
            OnPropertyChanged(nameof(TimeButtonText)); // Ensure TimeButtonText updates
        }

        private async void EditOrderItem(OrderItem orderItem)
        {
            if (orderItem == null) return;
            var product = orderItem.Product;

            Dictionary<int, List<string>> selectedModifiersMultiple = orderItem.SelectedModifiers != null
                ? new Dictionary<int, List<string>>(orderItem.SelectedModifiers)
                : new Dictionary<int, List<string>>();
            Dictionary<string, List<string>> nestedModifierDetails = orderItem.NestedModifierDetails != null
                ? new Dictionary<string, List<string>>(orderItem.NestedModifierDetails)
                : new Dictionary<string, List<string>>();

            var dialogBasePrice = orderItem.BaseUnitPrice > 0m
                ? orderItem.BaseUnitPrice
                : (product.PricePerItem > 0m
                    ? product.PricePerItem
                    : (product.Price > 0m ? product.Price : orderItem.Price));

            if (dialogBasePrice <= 0m)
            {
                var customPriceVm = new CustomPriceDialogViewModel(product.ItemName, null);
                var customPriceDialog = new POS_UI.View.CustomPriceDialog { DataContext = customPriceVm };
                var customPriceResult = await MaterialDesignThemes.Wpf.DialogHost.Show(customPriceDialog, "AddItemDialogHost");

                if (customPriceResult is decimal manualPrice && manualPrice > 0m)
                {
                    dialogBasePrice = manualPrice;
                }
                else
                {
                    return;
                }
            }

            // Recover discount percentage when it was baked into price (modify flow sets DiscountPercent=0)
            var effectiveDiscountPercent = orderItem.DiscountPercent;
            if (effectiveDiscountPercent == 0 && orderItem.UnitDiscountAmount > 0 && orderItem.Price > 0)
            {
                var priceBeforeDiscount = orderItem.Price + orderItem.UnitDiscountAmount;
                if (priceBeforeDiscount > 0)
                {
                    effectiveDiscountPercent = Math.Round(orderItem.UnitDiscountAmount / priceBeforeDiscount * 100, 2, MidpointRounding.AwayFromZero);
                }
            }

            // Show AddItemDialog pre-filled with updated modifiers
            var dialogVm = new AddItemDialogViewModel(
                product.ItemName,
                dialogBasePrice,
                product,
                selectedModifiersMultiple,
                nestedModifierDetails
            )
            {
                Quantity = orderItem.Quantity,
                Note = orderItem.Note,
                DiscountPercent = effectiveDiscountPercent,
                IsEditMode = true
            };

            if (effectiveDiscountPercent > 0)
            {
                var matchingPreset = dialogVm.DiscountPresets.FirstOrDefault(p => Math.Abs(p.Value - effectiveDiscountPercent) < 0.1m);
                if (matchingPreset != null)
                    dialogVm.SelectPresetDiscountCommand.Execute(matchingPreset.Value);
                else
                    dialogVm.CustomDiscountText = effectiveDiscountPercent.ToString("G29");
            }

            var dialog = new POS_UI.View.AddItemDialog { DataContext = dialogVm };
            bool itemConfirmed = false;
            dialogVm.ItemAdded += (itemVm) =>
            {
                orderItem.Quantity = itemVm.Quantity;
                orderItem.Note = itemVm.Note;
                orderItem.SelectedModifiers = CloneModifierSelections(itemVm.SelectedModifiersMultiple);
                orderItem.NestedModifierDetails = CloneNestedDetails(itemVm.NestedModifierDetails);
                orderItem.DiscountPercent = itemVm.DiscountPercent;
                orderItem.Price = Math.Round(itemVm.FinalPrice / itemVm.Quantity, 2, MidpointRounding.AwayFromZero);

                orderItem.ApiDiscountAmount = itemVm.DiscountAmount;
                orderItem.DisAmount = itemVm.DiscountAmount;
                orderItem.UnitDiscountAmount = itemVm.Quantity > 0
                    ? Math.Round(itemVm.DiscountAmount / itemVm.Quantity, 2, MidpointRounding.AwayFromZero)
                    : 0m;
                orderItem.VisibleDiscountAmount = itemVm.DiscountAmount;

                if (orderItem.Product != null)
                {
                    orderItem.Product.Price = orderItem.Price;
                    var baseForProduct = itemVm.BasePrice > 0m
                        ? itemVm.BasePrice
                        : (orderItem.BaseUnitPrice > 0m ? orderItem.BaseUnitPrice : orderItem.Price);
                    orderItem.Product.PricePerItem = baseForProduct;
                }
                orderItem.BaseUnitPrice = itemVm.BasePrice > 0 ? itemVm.BasePrice : (orderItem.BaseUnitPrice > 0m ? orderItem.BaseUnitPrice : orderItem.Price);
                orderItem.TaxComponents = OrderItemTaxComponentBuilder.Build(orderItem.Product, orderItem.BaseUnitPrice, orderItem.SelectedModifiers, orderItem.NestedModifierDetails);
                RecalculateDiscount();
                RecalculateCoupon();
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(SubTotal));
                itemConfirmed = true;
            };
            dialogVm.DialogClosed += () =>
            {
                try
                {
                    DialogHost.CloseDialogCommand.Execute(null, null);
                }
                catch (Exception ex)
                {
                    //System.Windows.MessageBox.Show($"Error closing dialog: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Closing Dialog", $"Error closing dialog: {ex.Message}");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                }
            };
            await DialogHost.Show(dialog, "AddItemDialogHost");
            // If itemConfirmed is false, user cancelled
        }

        private void RecalculateDiscount()
        {
            // When editing an order loaded from Tables, keep API discount as-is
            if (IsOrderLoadedForEdit && DiscountPercent <= 0)
            {
                return;
            }

            if (DiscountPercent > 0)
            {
                DiscountAmount = Math.Round(Total * DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
                // Ensure UI updates for discount-related properties
                OnPropertyChanged(nameof(DiscountAmount));
                OnPropertyChanged(nameof(DiscountDescription));
                OnPropertyChanged(nameof(HasDiscount));
            }
            // If you have a fixed discount, handle that here as well
        }

        private void RecalculateCoupon()
        {
            // Calculate base amount for coupon: subtotal minus discount amount
            var baseAmount = Total - DiscountAmount;
            if (baseAmount < 0) baseAmount = 0;
            
            // First, check if CouponCode is a numeric percentage (legacy support)
            if (!string.IsNullOrWhiteSpace(CouponCode) && decimal.TryParse(CouponCode, out var percent) && percent > 0 && percent <= 100)
            {
                CouponAmount = Math.Round(baseAmount * percent / 100m, 2, MidpointRounding.AwayFromZero);
                return;
            }
            
            // Check if there's a voucher with percentage type stored in CartService
            if (!string.IsNullOrWhiteSpace(CouponCode) && _cartService.Vouchers != null && _cartService.Vouchers.Count > 0)
            {
                var voucher = _cartService.Vouchers.FirstOrDefault(v => 
                    !string.IsNullOrWhiteSpace(v.VoucherCode) && 
                    v.VoucherCode.Equals(CouponCode, StringComparison.OrdinalIgnoreCase));
                
                if (voucher != null && 
                    !string.IsNullOrWhiteSpace(voucher.ValueType) && 
                    voucher.ValueType.Equals("percentage", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(voucher.VoucherValue) &&
                    decimal.TryParse(voucher.VoucherValue, out var voucherPercent) && 
                    voucherPercent > 0 && voucherPercent <= 100)
                {
                    // Recalculate coupon amount based on (subtotal - discount amount) and voucher percentage
                    var newCouponAmount = Math.Round(baseAmount * voucherPercent / 100m, 2, MidpointRounding.AwayFromZero);
                    CouponAmount = newCouponAmount;
                    // Update the voucher model to keep it in sync
                    voucher.VoucherDiscount = newCouponAmount;
                    return;
                }
            }
            
            // If you have a fixed coupon, handle that here as well
        }

        private void OrderItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OrderItem.Quantity) ||
                e.PropertyName == nameof(OrderItem.DiscountAmount) ||
                e.PropertyName == nameof(OrderItem.Total))
            {
                RecalculateDiscount();
                RecalculateCoupon();
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(Discount));
                OnPropertyChanged(nameof(CouponDiscount));
                OnPropertyChanged(nameof(SubTotal));
            }
        }

        private bool _isPlacingOrder;
        public bool IsPlacingOrder
        {
            get => _isPlacingOrder;
            set
            {
                if (_isPlacingOrder != value)
                {
                    _isPlacingOrder = value;
                    OnPropertyChanged(nameof(IsPlacingOrder));
                    OnPropertyChanged(nameof(CanPlaceOrder));
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private bool _isSavingOrder;
        public bool IsSavingOrder
        {
            get => _isSavingOrder;
            set
            {
                if (_isSavingOrder != value)
                {
                    _isSavingOrder = value;
                    OnPropertyChanged(nameof(IsSavingOrder));
                    OnPropertyChanged(nameof(IsCartActionProcessing));
                    OnPropertyChanged(nameof(IsCartActionNotProcessing));
                }
            }
        }

        private bool _isUpdatingOrder;
        public bool IsUpdatingOrder
        {
            get => _isUpdatingOrder;
            set
            {
                if (_isUpdatingOrder != value)
                {
                    _isUpdatingOrder = value;
                    OnPropertyChanged(nameof(IsUpdatingOrder));
                    OnPropertyChanged(nameof(IsCartActionProcessing));
                    OnPropertyChanged(nameof(IsCartActionNotProcessing));
                }
            }
        }

        public bool IsCartActionProcessing => _isSavingOrder || _isUpdatingOrder;
        public bool IsCartActionNotProcessing => !IsCartActionProcessing;

        // Shift status properties
        private bool _isShiftActive = false;
        private bool _isShiftStatusChecked = false;
        
        public bool IsShiftActive
        {
            get => _isShiftActive;
            set
            {
                if (_isShiftActive != value)
                {
                    _isShiftActive = value;
                    OnPropertyChanged(nameof(IsShiftActive));
                    OnPropertyChanged(nameof(IsShiftInactive));
                    OnPropertyChanged(nameof(CanUseCashier));
                }
            }
        }

        public bool IsShiftStatusChecked
        {
            get => _isShiftStatusChecked;
            set
            {
                if (_isShiftStatusChecked != value)
                {
                    _isShiftStatusChecked = value;
                    OnPropertyChanged(nameof(IsShiftStatusChecked));
                    OnPropertyChanged(nameof(IsShiftInactive));
                }
            }
        }

        public bool IsShiftInactive => _isShiftStatusChecked && !_isShiftActive;
        public bool CanUseCashier => _isShiftActive;

        public ICommand StartShiftCommand { get; }

        private static string BuildDraftItemKey(string name, int quantity, decimal unitPrice)
        {
            // Use invariant culture and 2-decimal formatting to ensure stable keys
            var priceKey = unitPrice.ToString("0.00", CultureInfo.InvariantCulture);
            return $"{name}_{quantity}_{priceKey}";
        }

        private static Dictionary<string, Dictionary<int, List<string>>> BuildDraftModifiersMap(IEnumerable<OrderItem> items)
        {
            var map = new Dictionary<string, Dictionary<int, List<string>>>();
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var it in items)
            {
                var baseKey = BuildDraftItemKey(it.Product?.ItemName ?? it.Name, it.Quantity, it.Price);
                counts.TryGetValue(baseKey, out var n);
                var key = n == 0 ? baseKey : ($"{baseKey}#{n}");
                counts[baseKey] = n + 1;
                map[key] = it.SelectedModifiers != null ? new Dictionary<int, List<string>>(it.SelectedModifiers) : new Dictionary<int, List<string>>();
            }
            return map;
        }

        private static Dictionary<string, Dictionary<string, List<string>>> BuildDraftNestedModifiersMap(IEnumerable<OrderItem> items)
        {
            var map = new Dictionary<string, Dictionary<string, List<string>>>();
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var it in items)
            {
                var baseKey = BuildDraftItemKey(it.Product?.ItemName ?? it.Name, it.Quantity, it.Price);
                counts.TryGetValue(baseKey, out var n);
                var key = n == 0 ? baseKey : ($"{baseKey}#{n}");
                counts[baseKey] = n + 1;
                map[key] = it.NestedModifierDetails != null ? new Dictionary<string, List<string>>(it.NestedModifierDetails) : new Dictionary<string, List<string>>();
            }
            return map;
        }

        private static Dictionary<int, List<string>> CloneModifierSelections(Dictionary<int, List<string>> source)
        {
            if (source == null) return new Dictionary<int, List<string>>();
            var clone = new Dictionary<int, List<string>>();
            foreach (var kvp in source)
            {
                clone[kvp.Key] = kvp.Value != null ? new List<string>(kvp.Value) : new List<string>();
            }
            return clone;
        }

        private static Dictionary<string, List<string>> CloneNestedDetails(Dictionary<string, List<string>> source)
        {
            if (source == null) return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var clone = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in source)
            {
                clone[kvp.Key] = kvp.Value != null ? new List<string>(kvp.Value) : new List<string>();
            }
            return clone;
        }

        private static string ResolveDraftKeyVariant(IEnumerable<string> keys, string baseKey)
        {
            if (keys == null) return null;
            // Exact match first
            if (keys.Contains(baseKey)) return baseKey;
            // Try sequenced variants base#0, base#1, base#2 ... up to a reasonable bound
            for (int i = 0; i < 100; i++)
            {
                var k = $"{baseKey}#{i}";
                if (keys.Contains(k)) return k;
            }
            return null;
        }

        private static decimal CalculateModifierUnitPrice(ProductItemModel product, Dictionary<int, List<string>> selectedModifiers, Dictionary<string, List<string>> nestedModifierDetails)
        {
            if (product?.Modifiers == null || selectedModifiers == null) return 0m;

            decimal total = 0m;
            foreach (var group in product.Modifiers)
            {
                if (group?.ModifierItems == null) continue;
                if (!selectedModifiers.TryGetValue(group.Id, out var names) || names == null || names.Count == 0) continue;

                foreach (var name in names)
                {
                    var modifierItem = group.ModifierItems.FirstOrDefault(mi => mi.ItemName == name);
                    if (modifierItem != null)
                    {
                        total += modifierItem.ItemPrice;

                        if (modifierItem.HasNestedModifiers && nestedModifierDetails != null && nestedModifierDetails.TryGetValue(name, out var nestedList) && nestedList != null)
                        {
                            foreach (var nested in nestedList)
                            {
                                int dollarIndex = nested.LastIndexOf('$');
                                if (dollarIndex >= 0 && dollarIndex < nested.Length - 1)
                                {
                                    string pricePart = nested.Substring(dollarIndex + 1).Trim();
                                    if (decimal.TryParse(pricePart, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal nestedPrice))
                                    {
                                        total += nestedPrice;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return Math.Round(total, 2, MidpointRounding.AwayFromZero);
        }

        // Shift management methods
        private async Task CheckShiftStatusAsync()
        {
            try
            {
                var activeSession = await _apiService.GetActiveCashDrawerSessionAsync();
                IsShiftActive = activeSession != null;
            }
            catch (Exception ex)
            {
                // If there's an error checking shift status, assume shift is inactive
                IsShiftActive = false;
                System.Diagnostics.Debug.WriteLine($"Error checking shift status: {ex.Message}");
            }
            finally
            {
                // Mark that shift status has been checked, so overlay can be shown if needed
                IsShiftStatusChecked = true;
            }
        }

        private async Task StartShiftAsync()
        {
            try
            {
                // Show opening balance dialog - it will handle the API call internally
                var openingBalanceDialog = new OpeningBalanceDialog { DialogHostIdentifier = "AddItemDialogHost" };
                await MaterialDesignThemes.Wpf.DialogHost.Show(openingBalanceDialog, "AddItemDialogHost");
                
                // Check if shift is now active after dialog closes
                try
                {
                    var session = await _apiService.GetActiveCashDrawerSessionAsync();
                    IsShiftActive = session != null;
                }
                catch
                {
                    // If we can't check, assume shift might be active
                    IsShiftActive = true;
                }
            }
            catch (Exception ex)
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Starting Session", $"Failed to start session: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                await MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
            }
        }

        private void RehydrateLoadedOrderItemsWithCatalog()
        {
            try
            {
                if (OrderItems == null || OrderItems.Count == 0) return;

                bool componentsUpdated = false;
                foreach (var cartItem in OrderItems)
                {
                    if (cartItem == null) continue;

                    var previousProduct = cartItem.Product;
                    var matchedProduct = FindMatchingProductForOrderItem(cartItem);
                    if (matchedProduct != null)
                    {
                        var preparedProduct = PrepareProductWithSelectedModifiers(
                            matchedProduct,
                            previousProduct,
                            cartItem.SelectedModifiers,
                            cartItem.NestedModifierDetails);
                        if (preparedProduct != null)
                        {
                            cartItem.Product = preparedProduct;
                        }
                    }
                    else if (cartItem.Product == null)
                    {
                        cartItem.Product = new ProductItemModel
                        {
                            Id = cartItem.ApiItemId > 0 ? cartItem.ApiItemId : 0,
                            ItemName = cartItem.Name,
                            PricePerItem = cartItem.BaseUnitPrice > 0m ? cartItem.BaseUnitPrice : cartItem.Price
                        };
                    }
                    else
                    {
                        EnsureSelectedModifierCoverage(
                            cartItem.Product,
                            previousProduct,
                            cartItem.SelectedModifiers,
                            cartItem.NestedModifierDetails);
                    }

                    if (cartItem.Product != null && !cartItem.Product.TaxProfileId.HasValue)
                    {
                        if (previousProduct?.TaxProfileId.HasValue == true)
                        {
                            cartItem.Product.TaxProfileId = previousProduct.TaxProfileId;
                        }

                        var fallbackProfileId = cartItem.TaxDetails?.FirstOrDefault()?.TaxProfileId;
                        if (fallbackProfileId.HasValue)
                        {
                            cartItem.Product.TaxProfileId = fallbackProfileId;
                        }
                    }

                    cartItem.TaxComponents = null;
                    OrderItemTaxComponentBuilder.EnsureComponents(cartItem);
                    componentsUpdated = true;
                }

                if (componentsUpdated)
                {
                    _cartService.ForceRecalculateTaxes();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to rehydrate order items: {ex.Message}");
            }
        }

        private static ProductItemModel PrepareProductWithSelectedModifiers(
            ProductItemModel catalogProduct,
            ProductItemModel existingProduct,
            Dictionary<int, List<string>> selectedModifiers,
            Dictionary<string, List<string>> nestedModifierDetails)
        {
            var baseProduct = catalogProduct ?? existingProduct;
            if (baseProduct == null) return null;

            var prepared = CloneProduct(baseProduct);
            EnsureSelectedModifierCoverage(
                prepared,
                existingProduct ?? baseProduct,
                selectedModifiers,
                nestedModifierDetails);
            return prepared;
        }

        private static ProductItemModel CloneProduct(ProductItemModel source)
        {
            if (source == null) return null;

            return new ProductItemModel
            {
                Id = source.Id,
                ItemName = source.ItemName,
                Price = source.Price,
                PricePerItem = source.PricePerItem,
                Category = source.Category,
                CategoryId = source.CategoryId,
                ImageUrl = source.ImageUrl,
                TaxProfileId = source.TaxProfileId,
                Modifiers = CloneModifierGroups(source.Modifiers),
                PrinterGroups = source.PrinterGroups != null ? new List<POS_UI.Models.PrinterGroupModel>(source.PrinterGroups) : new List<POS_UI.Models.PrinterGroupModel>()
            };
        }

        private static void EnsureSelectedModifierCoverage(
            ProductItemModel target,
            ProductItemModel fallbackSource,
            Dictionary<int, List<string>> selectedModifiers,
            Dictionary<string, List<string>> nestedModifierDetails)
        {
            if (target == null || selectedModifiers == null || selectedModifiers.Count == 0) return;

            foreach (var kvp in selectedModifiers)
            {
                var groupId = kvp.Key;
                var selections = kvp.Value;
                if (selections == null || selections.Count == 0) continue;

                var fallbackGroup = FindModifierGroup(fallbackSource, groupId, selections);
                var targetGroup = FindModifierGroup(target, groupId, selections);

                if (targetGroup == null)
                {
                    targetGroup = fallbackGroup != null
                        ? CloneModifierGroup(fallbackGroup)
                        : new ModifierModel
                        {
                            Id = groupId,
                            Title = fallbackGroup?.Title ?? "Modifier",
                            ModifierItems = new List<ModifierItemModel>()
                        };
                    target.Modifiers ??= new List<ModifierModel>();
                    target.Modifiers.Add(targetGroup);
                }
                else if (fallbackGroup != null && string.IsNullOrWhiteSpace(targetGroup.Title))
                {
                    targetGroup.Title = fallbackGroup.Title;
                }

                targetGroup.ModifierItems ??= new List<ModifierItemModel>();

                foreach (var selection in selections)
                {
                    if (string.IsNullOrWhiteSpace(selection)) continue;

                    var targetItem = targetGroup.ModifierItems.FirstOrDefault(i =>
                        string.Equals(i?.ItemName, selection, StringComparison.OrdinalIgnoreCase));
                    var fallbackItem = FindModifierItem(fallbackSource, groupId, selection);

                    if (targetItem == null)
                    {
                        targetItem = fallbackItem != null
                            ? CloneModifierItem(fallbackItem)
                            : new ModifierItemModel
                            {
                                ItemName = selection,
                                ItemPrice = fallbackItem?.ItemPrice ?? 0m
                            };
                        targetGroup.ModifierItems.Add(targetItem);
                    }
                    else
                    {
                        if (targetItem.ItemPrice <= 0m && fallbackItem?.ItemPrice > 0m)
                        {
                            targetItem.ItemPrice = fallbackItem.ItemPrice;
                        }
                        if (!targetItem.TaxProfileId.HasValue && fallbackItem?.TaxProfileId.HasValue == true)
                        {
                            targetItem.TaxProfileId = fallbackItem.TaxProfileId;
                        }
                        if ((targetItem.NestedModifiers == null || targetItem.NestedModifiers.Count == 0) &&
                            fallbackItem?.NestedModifiers?.Count > 0)
                        {
                            targetItem.NestedModifiers = CloneModifierGroups(fallbackItem.NestedModifiers);
                        }
                    }

                    EnsureNestedModifierCoverage(targetItem, fallbackItem, nestedModifierDetails);
                }
            }
        }

        private static void EnsureNestedModifierCoverage(
            ModifierItemModel targetItem,
            ModifierItemModel fallbackItem,
            Dictionary<string, List<string>> nestedModifierDetails)
        {
            if (targetItem == null || nestedModifierDetails == null || nestedModifierDetails.Count == 0) return;
            if (!nestedModifierDetails.TryGetValue(targetItem.ItemName ?? string.Empty, out var nestedSelections) ||
                nestedSelections == null || nestedSelections.Count == 0)
            {
                return;
            }

            targetItem.NestedModifiers ??= new List<ModifierModel>();

            foreach (var detail in nestedSelections)
            {
                var parsed = ParseNestedDetailForViewModel(detail);
                if (string.IsNullOrWhiteSpace(parsed.GroupTitle) || string.IsNullOrWhiteSpace(parsed.ItemName)) continue;

                var fallbackGroup = fallbackItem?.NestedModifiers?.FirstOrDefault(g =>
                    string.Equals(g?.Title?.Trim(), parsed.GroupTitle, StringComparison.OrdinalIgnoreCase));
                var targetGroup = targetItem.NestedModifiers.FirstOrDefault(g =>
                    string.Equals(g?.Title?.Trim(), parsed.GroupTitle, StringComparison.OrdinalIgnoreCase));

                if (targetGroup == null)
                {
                    targetGroup = fallbackGroup != null
                        ? CloneModifierGroup(fallbackGroup)
                        : new ModifierModel
                        {
                            Id = fallbackGroup?.Id ?? 0,
                            Title = parsed.GroupTitle,
                            ModifierItems = new List<ModifierItemModel>()
                        };
                    targetItem.NestedModifiers.Add(targetGroup);
                }

                targetGroup.ModifierItems ??= new List<ModifierItemModel>();

                var targetNested = targetGroup.ModifierItems.FirstOrDefault(i =>
                    string.Equals(i?.ItemName?.Trim(), parsed.ItemName, StringComparison.OrdinalIgnoreCase));
                var fallbackNested = fallbackGroup?.ModifierItems?.FirstOrDefault(i =>
                    string.Equals(i?.ItemName?.Trim(), parsed.ItemName, StringComparison.OrdinalIgnoreCase));

                if (targetNested == null)
                {
                    targetNested = fallbackNested != null
                        ? CloneModifierItem(fallbackNested)
                        : new ModifierItemModel
                        {
                            Id = 0,
                            ItemName = parsed.ItemName,
                            ItemPrice = parsed.Price ?? fallbackNested?.ItemPrice ?? 0m
                        };
                    targetGroup.ModifierItems.Add(targetNested);
                }
                else if (targetNested.ItemPrice <= 0m && parsed.Price.HasValue)
                {
                    targetNested.ItemPrice = parsed.Price.Value;
                }

                if (!targetNested.TaxProfileId.HasValue && fallbackNested?.TaxProfileId.HasValue == true)
                {
                    targetNested.TaxProfileId = fallbackNested.TaxProfileId;
                }
            }
        }

        private static (string GroupTitle, string ItemName, decimal? Price) ParseNestedDetailForViewModel(string detail)
        {
            var text = detail?.Trim() ?? string.Empty;
            decimal? price = null;
            var dollarIndex = text.LastIndexOf('$');
            if (dollarIndex > 0 && dollarIndex < text.Length - 1)
            {
                var pricePart = text.Substring(dollarIndex + 1).Trim();
                if (decimal.TryParse(pricePart, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedPrice))
                {
                    price = parsedPrice;
                    text = text.Substring(0, dollarIndex).Trim();
                }
            }

            var colonIndex = text.IndexOf(':');
            if (colonIndex <= 0)
            {
                return (text, text, price);
            }

            var groupTitle = text.Substring(0, colonIndex).Trim();
            var itemName = text.Substring(colonIndex + 1).Trim();
            return (groupTitle, itemName, price);
        }

        private static ModifierModel FindModifierGroup(ProductItemModel product, int groupId, List<string> selections)
        {
            var groups = product?.Modifiers;
            if (groups == null || groups.Count == 0) return null;

            if (groupId > 0)
            {
                var match = groups.FirstOrDefault(g => g?.Id == groupId);
                if (match != null) return match;
            }

            if (selections != null && selections.Count > 0)
            {
                foreach (var group in groups)
                {
                    if (group?.ModifierItems == null) continue;
                    if (group.ModifierItems.Any(mi =>
                            mi != null && selections.Any(sel =>
                                string.Equals(mi.ItemName, sel, StringComparison.OrdinalIgnoreCase))))
                    {
                        return group;
                    }
                }
            }

            return null;
        }

        private static ModifierItemModel FindModifierItem(ProductItemModel product, int groupId, string selection)
        {
            if (product == null || string.IsNullOrWhiteSpace(selection)) return null;
            var group = FindModifierGroup(product, groupId, new List<string> { selection });
            return group?.ModifierItems?.FirstOrDefault(mi =>
                string.Equals(mi?.ItemName, selection, StringComparison.OrdinalIgnoreCase));
        }

        private static ModifierModel CloneModifierGroup(ModifierModel source)
        {
            if (source == null) return null;
            return new ModifierModel
            {
                Id = source.Id,
                Title = source.Title,
                MinPermitted = source.MinPermitted,
                MaxPermitted = source.MaxPermitted,
                DefaultQuantity = source.DefaultQuantity,
                IsTaxInherited = source.IsTaxInherited,
                ModifierItems = CloneModifierItems(source.ModifierItems)
            };
        }

        private static List<ModifierModel> CloneModifierGroups(List<ModifierModel> source)
        {
            if (source == null || source.Count == 0) return new List<ModifierModel>();

            return source
                .Where(g => g != null)
                .Select(CloneModifierGroup)
                .ToList();
        }

        private static ModifierItemModel CloneModifierItem(ModifierItemModel source)
        {
            if (source == null) return null;
            return new ModifierItemModel
            {
                Id = source.Id,
                ItemName = source.ItemName,
                ItemPrice = source.ItemPrice,
                IsSelected = source.IsSelected,
                TaxProfileId = source.TaxProfileId,
                ExternalItemId = source.ExternalItemId,
                NestedModifiers = CloneModifierGroups(source.NestedModifiers)
            };
        }

        private static List<ModifierItemModel> CloneModifierItems(List<ModifierItemModel> source)
        {
            if (source == null || source.Count == 0) return new List<ModifierItemModel>();

            return source
                .Where(item => item != null)
                .Select(CloneModifierItem)
                .ToList();
        }

        private ProductItemModel FindMatchingProductForOrderItem(OrderItem cartItem)
        {
            if (cartItem == null || AllProducts == null || AllProducts.Count == 0) return null;

            ProductItemModel match = null;

            if (cartItem.ApiItemId > 0)
            {
                match = AllProducts.FirstOrDefault(p => p.Id == cartItem.ApiItemId);
            }

            if (match == null && cartItem.Product?.Id > 0)
            {
                match = AllProducts.FirstOrDefault(p => p.Id == cartItem.Product.Id);
            }

            if (match == null)
            {
                var candidates = new[]
                {
                    cartItem.Product?.ItemName,
                    cartItem.Name,
                    cartItem.DisplayName
                };

                foreach (var candidate in candidates)
                {
                    if (string.IsNullOrWhiteSpace(candidate)) continue;
                    match = AllProducts.FirstOrDefault(p => string.Equals(p.ItemName, candidate, StringComparison.OrdinalIgnoreCase));
                    if (match != null) break;
                }
            }

            return match;
        }

        // ============================================
        // MENU TAB METHODS
        // ============================================

        /// <summary>
        /// Loads menu tabs configuration from cache or API
        /// </summary>
        private async Task LoadMenuTabsAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[CashierVM] ========== Loading menu tabs ==========");
                
                // Try to use cached config from GlobalDataService first
                var globalData = GlobalDataService.Instance;
                MenuConfigModel config = null;
                
                if (globalData.CachedMenuConfig != null)
                {
                    System.Diagnostics.Debug.WriteLine("[CashierVM] Using cached menu config from GlobalDataService");
                    config = globalData.CachedMenuConfig;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[CashierVM] Cache not available, loading from API...");
                    config = await MenuConfigService.Instance.LoadMenuConfigAsync();
                }
                
                System.Diagnostics.Debug.WriteLine($"[CashierVM] Got config with {config.Tabs.Count} tabs");
                
                // Clear and populate menu tabs
                MenuTabs.Clear();
                foreach (var tab in config.Tabs.OrderBy(t => t.Order))
                {
                    MenuTabs.Add(tab);
                    System.Diagnostics.Debug.WriteLine($"[CashierVM] Added tab: '{tab.Name}' (ID={tab.Id}, Order={tab.Order}, Type={tab.ContentType})");
                }
                
                // Auto-select the FIRST tab by Order (not by IsDefault)
                var tabToSelect = MenuTabs.OrderBy(t => t.Order).FirstOrDefault();
                if (tabToSelect != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[CashierVM] Auto-selecting FIRST tab by order: '{tabToSelect.Name}' (Order={tabToSelect.Order})");
                    SelectedMenuTab = tabToSelect;
                    
                    // Explicitly trigger filtering to ensure the tab loads properly
                    FilterByMenuTab();
                    
                    System.Diagnostics.Debug.WriteLine($"[CashierVM] ✓ Tab '{tabToSelect.Name}' loaded and filtered");
                }
                
                System.Diagnostics.Debug.WriteLine($"[CashierVM] ========== Loaded {MenuTabs.Count} menu tabs ==========");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CashierVM] ========== Error loading menu tabs: {ex.Message} ==========");
                System.Diagnostics.Debug.WriteLine($"[CashierVM] Stack trace: {ex.StackTrace}");
                
                // Fallback: Create default "All Items" tab if loading fails
                MenuTabs.Clear();
                var defaultTab = new MenuTabModel
                {
                    Id = 1,
                    Name = "All Items",
                    Order = 1,
                    IsDefault = true,
                    ContentType = "categories",
                    CategoryIds = new List<int>(),
                    ItemIds = new List<int>()
                };
                MenuTabs.Add(defaultTab);
                SelectedMenuTab = defaultTab;
                FilterByMenuTab(); // Ensure fallback tab is also filtered
                
                System.Diagnostics.Debug.WriteLine($"[CashierVM] Using fallback default tab");
            }
        }

        /// <summary>
        /// Filters categories and products based on selected menu tab
        /// </summary>
        private void FilterByMenuTab()
        {
            try
            {
                if (SelectedMenuTab == null)
                {
                    System.Diagnostics.Debug.WriteLine("[CashierVM] No menu tab selected, showing all");
                    FilterProducts();
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[CashierVM] Filtering by menu tab: {SelectedMenuTab.Name} ({SelectedMenuTab.ContentType})");

                if (SelectedMenuTab.ContentType == "categories")
                {
                    ShowCategoryView = true;
                    ShowMixedView = false;
                    
                    // If specific categories are selected, filter Categories collection
                    if (SelectedMenuTab.CategoryIds != null && SelectedMenuTab.CategoryIds.Any())
                    {
                        System.Diagnostics.Debug.WriteLine($"[CashierVM] Custom menu - Filtering to {SelectedMenuTab.CategoryIds.Count} specific categories (NO 'All Items')");
                        
                        // Build category ID to name mapping
                        var categoryIdToName = AllProducts
                            .GroupBy(p => p.CategoryId)
                            .ToDictionary(g => g.Key, g => g.First().Category);
                        
                        // Show categories in the order they appear in CategoryIds list
                        Categories.Clear();
                        foreach (var categoryId in SelectedMenuTab.CategoryIds)
                        {
                            if (categoryIdToName.ContainsKey(categoryId))
                            {
                                Categories.Add(categoryIdToName[categoryId]);
                            }
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[CashierVM] Showing {Categories.Count} filtered categories in custom order (custom menu)");
                    }
                    else
                    {
                        // Empty CategoryIds means show ALL categories (DEFAULT MENU)
                        System.Diagnostics.Debug.WriteLine($"[CashierVM] Default menu - showing all categories WITH 'All Items'");
                        
                        // Rebuild categories from all products
                        var allCategories = AllProducts
                            .Select(p => p.Category)
                            .Distinct()
                            .OrderBy(c => c)
                            .ToList();
                        
                        Categories.Clear();
                        Categories.Add("All Items"); // Add "All Items" ONLY for default menu
                        foreach (var cat in allCategories.Where(c => c != "All Items"))
                        {
                            Categories.Add(cat);
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[CashierVM] Showing all {Categories.Count} categories (default menu)");
                    }
                    
                    OnPropertyChanged(nameof(CategoriesWithCount));
                }
                else if (SelectedMenuTab.ContentType == "items")
                {
                    ShowCategoryView = false;
                    ShowMixedView = false;
                    
                    if (SelectedMenuTab.ItemIds != null && SelectedMenuTab.ItemIds.Any())
                    {
                        System.Diagnostics.Debug.WriteLine($"[CashierVM] Filtering to {SelectedMenuTab.ItemIds.Count} specific items");
                        
                        IEnumerable<POS_UI.Models.ProductItemModel> filteredProducts;
                        
                        // If no sort applied (None), display in custom order from menu
                        if (SelectedSortOption == ProductSortOption.None)
                        {
                            // Create a dictionary for quick lookup
                            var productDict = AllProducts.Where(p => SelectedMenuTab.ItemIds.Contains(p.Id))
                                .ToDictionary(p => p.Id, p => p);
                            
                            // Display in the order specified by ItemIds list
                            var orderedProducts = new List<POS_UI.Models.ProductItemModel>();
                            foreach (var itemId in SelectedMenuTab.ItemIds)
                            {
                                if (productDict.ContainsKey(itemId))
                                {
                                    orderedProducts.Add(productDict[itemId]);
                                }
                            }
                            filteredProducts = orderedProducts;
                            System.Diagnostics.Debug.WriteLine($"[CashierVM] Displaying items in custom menu order");
                        }
                        else
                        {
                            // Apply sort option
                            filteredProducts = AllProducts.Where(p => SelectedMenuTab.ItemIds.Contains(p.Id));
                            
                            switch (SelectedSortOption)
                            {
                                case ProductSortOption.AZ:
                                    filteredProducts = filteredProducts.OrderBy(p => p.ItemName);
                                    break;
                                case ProductSortOption.ZA:
                                    filteredProducts = filteredProducts.OrderByDescending(p => p.ItemName);
                                    break;
                                case ProductSortOption.PriceLowHigh:
                                    filteredProducts = filteredProducts.OrderBy(p => p.Price);
                                    break;
                                case ProductSortOption.PriceHighLow:
                                    filteredProducts = filteredProducts.OrderByDescending(p => p.Price);
                                    break;
                            }
                            System.Diagnostics.Debug.WriteLine($"[CashierVM] Displaying items with sort: {SelectedSortOption}");
                        }
                        
                        Products.Clear();
                        foreach (var product in filteredProducts)
                        {
                            Products.Add(product);
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[CashierVM] Showing {Products.Count} filtered items");
                    }
                    else
                    {
                        // Empty ItemIds means show all items
                        System.Diagnostics.Debug.WriteLine($"[CashierVM] No item filter - showing all items");
                        FilterProducts();
                    }
                }
                else if (SelectedMenuTab.ContentType == "mixed" && SelectedMenuTab.Slots != null && SelectedMenuTab.Slots.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"[CashierVM] Mixed menu tab with {SelectedMenuTab.Slots.Count} slots");
                    
                    var categorySlots = SelectedMenuTab.Slots.Where(s => s.Type == "category").ToList();
                    var itemSlots = SelectedMenuTab.Slots.Where(s => s.Type == "item").ToList();
                    
                    if (categorySlots.Any() && !itemSlots.Any())
                    {
                        ShowCategoryView = true;
                        ShowMixedView = false;
                        var categoryIdToName = AllProducts
                            .GroupBy(p => p.CategoryId)
                            .ToDictionary(g => g.Key, g => g.First().Category);
                        
                        Categories.Clear();
                        foreach (var slot in categorySlots)
                        {
                            if (categoryIdToName.ContainsKey(slot.Id))
                                Categories.Add(categoryIdToName[slot.Id]);
                        }
                        OnPropertyChanged(nameof(CategoriesWithCount));
                    }
                    else if (itemSlots.Any() && !categorySlots.Any())
                    {
                        ShowCategoryView = false;
                        ShowMixedView = false;
                        var productDict = AllProducts.ToDictionary(p => p.Id, p => p);
                        Products.Clear();
                        foreach (var slot in itemSlots)
                        {
                            if (productDict.ContainsKey(slot.Id))
                                Products.Add(productDict[slot.Id]);
                        }
                    }
                    else
                    {
                        // Both categories and items — show unified mixed grid
                        ShowCategoryView = false;
                        ShowMixedView = true;
                        OnPropertyChanged(nameof(ShowItemsView));

                        var productDict = AllProducts.ToDictionary(p => p.Id, p => p);
                        var categoryItemCounts = AllProducts.GroupBy(p => p.CategoryId)
                            .ToDictionary(g => g.Key, g => g.Count());
                        var categoryIdToName = AllProducts
                            .GroupBy(p => p.CategoryId)
                            .ToDictionary(g => g.Key, g => g.First().Category);

                        var currency = Services.GlobalDataService.Instance?.ShopDetails?.Currency ?? "£";

                        MixedMenuItems.Clear();
                        foreach (var slot in SelectedMenuTab.Slots)
                        {
                            if (slot.Type == "category" && categoryIdToName.ContainsKey(slot.Id))
                            {
                                var catName = categoryIdToName[slot.Id];
                                var count = categoryItemCounts.ContainsKey(slot.Id) ? categoryItemCounts[slot.Id] : 0;
                                MixedMenuItems.Add(new POS_UI.Models.MenuDisplayItem
                                {
                                    DisplayType = "category",
                                    Name = catName,
                                    SecondLine = count == 1 ? "1 item" : $"{count} items",
                                    CategoryId = slot.Id,
                                    CategoryName = catName,
                                    BackgroundColor = Helpers.ColorPalette.GetBackgroundColor(catName),
                                    TextColor = Helpers.ColorPalette.GetTextColor()
                                });
                            }
                            else if (slot.Type == "item" && productDict.ContainsKey(slot.Id))
                            {
                                var product = productDict[slot.Id];
                                MixedMenuItems.Add(new POS_UI.Models.MenuDisplayItem
                                {
                                    DisplayType = "item",
                                    Name = product.ItemName,
                                    SecondLine = product.Price > 0 ? $"{currency} {product.Price:F2}" : "-",
                                    Product = product,
                                    BackgroundColor = product.BackgroundColor,
                                    TextColor = product.TextColor
                                });
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"[CashierVM] Mixed grid: {MixedMenuItems.Count} items ({categorySlots.Count} cats, {itemSlots.Count} items)");
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[CashierVM] Mixed tab loaded: {Products.Count} products, {Categories.Count} categories");
                }
                
                // Clear search and reset sort when switching tabs
                SearchText = string.Empty;
                SelectedCategory = null;
                CanGoBack = false;
                SelectedSortOption = ProductSortOption.None; // Reset to custom menu order
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CashierVM] Error filtering by menu tab: {ex.Message}");
                FilterProducts();
            }
        }
    }
    public class CashierRelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        public CashierRelayCommand(Action execute, Func<bool> canExecute = null) { _execute = execute; _canExecute = canExecute; }
        public event EventHandler CanExecuteChanged { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
        public void Execute(object parameter) => _execute();
    }
    public class CashierRelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;
        public CashierRelayCommand(Action<T> execute, Func<T, bool> canExecute = null) { _execute = execute; _canExecute = canExecute; }
        public event EventHandler CanExecuteChanged { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute((T)parameter);
        public void ExecuteCommand(object parameter) => _execute((T)parameter);
        void ICommand.Execute(object parameter) => ExecuteCommand(parameter);
    }
    public class ShopFeeDisplayModel
    {
        public string Name { get; set; }
        public int ShopFeeId { get; set; }
        public string Label { get; set; }
        public decimal Amount { get; set; }
        public bool IsMandatory { get; set; }
        /// <summary>Optional fee taken out of totals but still shown faded in the cart.</summary>
        public bool IsRemoved { get; set; }
        public ICommand RemoveCommand { get; set; }
        public ICommand AddCommand { get; set; }
        public bool IsRemovable => !IsMandatory;
        public bool ShowRemoveCross => !IsRemoved && IsRemovable;
        public bool ShowAddButton => IsRemoved && IsRemovable;
    }
} 