using MaterialDesignThemes.Wpf;
using POS_UI.Models;
using POS_UI.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Drawing.Printing;
using System.Windows.Input;
using System.Windows;

namespace POS_UI.ViewModels
{
    public class KitchenCheckoutViewModel : BaseViewModel
    {
        /// <summary>Second DialogHost on kitchen and live-orders pages; use when the main host already shows split or checkout.</summary>
        private const string KitchenSplitOverlayHostId = "KitchenSplitOverlayDialogHost";

        private readonly ApiService _apiService = new ApiService();
        private readonly CardMachineApiService _cardMachineApiService = new CardMachineApiService();
        private readonly OrderModel _order;
        private readonly string _dialogHostId;

        public KitchenCheckoutViewModel(OrderModel order, string dialogHostId = "RootDialog")
        {
            _order = order ?? throw new System.ArgumentNullException(nameof(order));
            _dialogHostId = dialogHostId ?? "RootDialog";
            SelectedPaymentMethod = PaymentMethod.Card;
            // Initialize cash input to subtotal so balance is zero by default
            CashInputString = SubTotal.ToString("F2");
            ConfirmOrderCommand = new RelayCommand(async () => await FinishOrderAsync());
            // For platform 8 (table order), load session total so paying amount = session total
            if (IsTableOrderPlatform && _order.OrderSessionId.HasValue && _order.OrderSessionId.Value > 0)
                _ = EnsureSessionTotalLoadedAsync();
        }

        public ICommand ConfirmOrderCommand { get; }

        public PaymentMethod SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set
            {
                _selectedPaymentMethod = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CheckoutPrimaryButtonText));
                OnPropertyChanged(nameof(IsCashPaymentSelected));
                OnPropertyChanged(nameof(IsCODPaymentSelected));
                OnPropertyChanged(nameof(IsCashPaymentValid));
            }
        }
        private PaymentMethod _selectedPaymentMethod;

        public string CheckoutPrimaryButtonText => "Finish Order";

        // Do not show COD in kitchen checkout
        public bool IsDeliveryOrder => false;
        public bool IsTakeAwayOrder => false;
        public bool IsCashPaymentSelected => SelectedPaymentMethod == PaymentMethod.Cash;
        public bool IsCODPaymentSelected => SelectedPaymentMethod == PaymentMethod.COD;
        // Round SubTotal to 2 decimal places to avoid precision issues in comparison
        public bool IsCashPaymentValid => !IsCashPaymentSelected || CashGiven >= Math.Round(SubTotal, 2, MidpointRounding.AwayFromZero);
        
        // Do not show Pay Later option in Orders page checkout (only available in Cashier page for Dine In)
        public bool ShowPayLaterOption => false;
        public bool IsDininOrder => false;

        /// <summary>When platform is 8 (table order) and session total is loaded, returns session total; otherwise order total.</summary>
        public decimal SubTotal
        {
            get
            {
                if (_order == null) return 0m;
                // For table order (platform 8), use session total as paying amount when available
                if (IsTableOrderPlatform && _sessionTotalAmount.HasValue)
                    return _sessionTotalAmount.Value;
                if (_order.ApiTotal.HasValue && _order.ApiTotal.Value > 0) return _order.ApiTotal.Value;
                if (_order.Items != null && _order.Items.Count > 0) return _order.Total;
                return _order.DisplayTotal;
            }
        }

        private bool IsTableOrderPlatform => _order != null && (_order.PlatformId == 8 || _order.PlatformId2 == 8);
        private decimal? _sessionTotalAmount;

        /// <summary>Loads session total for platform 8 table orders. No-op otherwise. Safe to call multiple times.</summary>
        private async Task EnsureSessionTotalLoadedAsync()
        {
            if (!IsTableOrderPlatform || !_order.OrderSessionId.HasValue || _order.OrderSessionId.Value <= 0)
                return;
            if (_sessionTotalAmount.HasValue)
                return;
            try
            {
                var response = await _apiService.GetSessionOrdersAsync(_order.OrderSessionId.Value);
                if (response?.Data != null && !string.IsNullOrEmpty(response.Data.TotalAmount)
                    && decimal.TryParse(response.Data.TotalAmount, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var total))
                {
                    _sessionTotalAmount = total;
                    OnPropertyChanged(nameof(SubTotal));
                    OnPropertyChanged(nameof(CashBalance));
                    OnPropertyChanged(nameof(IsCashPaymentValid));
                    // Default cash input to session total so balance is zero
                    CashInputString = total.ToString("F2");
                }
            }
            catch
            {
                // Keep order total as fallback; non-fatal
            }
        }

        /// <summary>
        /// For table orders (platform 8) with a session, success text lists every order's DisplayOrderId in that session.
        /// Otherwise uses this order's DisplayOrderId 
        /// </summary>
        private async Task<string> BuildSuccessDialogOrderRefAsync(SessionOrdersResponse cachedSessionResponse)
        {
            string SingleOrderRef() =>
                !string.IsNullOrWhiteSpace(_order.DisplayOrderId)
                    ? _order.DisplayOrderId
                    : (_order.OrderNumber ?? _order.ApiId.ToString());

            if (!IsTableOrderPlatform || !_order.OrderSessionId.HasValue || _order.OrderSessionId.Value <= 0)
                return SingleOrderRef();

            SessionOrdersResponse response = cachedSessionResponse;
            if (response?.Data?.OrderDetails == null || response.Data.OrderDetails.Count == 0)
            {
                try
                {
                    response = await _apiService.GetSessionOrdersAsync(_order.OrderSessionId.Value);
                }
                catch
                {
                    response = null;
                }
            }

            if (response?.Data?.OrderDetails == null || response.Data.OrderDetails.Count == 0)
                return SingleOrderRef();

            var parts = response.Data.OrderDetails
                .Select(d =>
                {
                    if (!string.IsNullOrWhiteSpace(d.DisplayOrderId))
                        return d.DisplayOrderId.Trim();
                    return d.OrderApiId > 0 ? d.OrderApiId.ToString() : null;
                })
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return parts.Count > 0 ? string.Join(", ", parts) : SingleOrderRef();
        }

        /// <summary>Loads full order if needed and prints a single main receipt (used when no session or as fallback).</summary>
        private async Task PrintSingleOrderReceiptAsync(string paymentMethod)
        {
            OrderModel orderForPrinting = _order;
            if (_order.ApiId > 0 && (_order.Items == null || _order.Items.Count == 0))
            {
                try
                {
                    var fullOrder = await _apiService.GetOrderByIdAsync(_order.ApiId);
                    if (fullOrder != null && fullOrder.Items != null && fullOrder.Items.Count > 0)
                    {
                        orderForPrinting = fullOrder;
                        orderForPrinting.PaymentMethod = paymentMethod;
                    }
                }
                catch
                {
                    orderForPrinting = _order;
                }
            }
            // Order completion: treat this as an explicit main receipt print.
            // Respect MainReceipt flag, but do NOT require "Print main receipt on order" (MainReceiptOnOrder),
            // so printers that are configured for main receipts still print when the order is completed.
            await POS_UI.Services.ReceiptPrintingService.Instance.PrintIncomingMainReceiptAsync(orderForPrinting, paymentMethod, onlyWhenMainReceiptOnOrder: false);
        }

        private string _cashInputString = string.Empty;
        public string CashInputString
        {
            get => _cashInputString;
            set
            {
                _cashInputString = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CashGiven));
                OnPropertyChanged(nameof(CashGivenString));
                OnPropertyChanged(nameof(CashBalance));
                OnPropertyChanged(nameof(IsCashPaymentValid));
            }
        }

        public decimal CashGiven
        {
            get
            {
                if (decimal.TryParse(CashInputString, out var amount))
                {
                    return amount < 0 ? 0m : amount;
                }
                return 0m;
            }
        }

        public string CashGivenString => CashGiven.ToString("F2");
        public decimal CashBalance => CashGiven - SubTotal;
        public bool ShouldFocusCashInput { get; set; }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public ICommand SelectPaymentMethodCommand => new RelayCommand<object>(param =>
        {
            if (param is PaymentMethod enumValue)
            {
                SelectedPaymentMethod = enumValue;
                return;
            }
            if (param is string method && System.Enum.TryParse<PaymentMethod>(method, out var parsed))
            {
                SelectedPaymentMethod = parsed;
            }
        });

        private List<SplitPaymentItem> _pendingSplitPayments;
        private string _activeSplitDialogHostId = "SplitPaymentDialogHost";
        public List<SplitPaymentItem> PendingSplitPayments { get => _pendingSplitPayments; private set { _pendingSplitPayments = value; OnPropertyChanged(); } }

        public ICommand EnableSplitPaymentCommand => new RelayCommand(async () => await OpenSplitPaymentDialogAsync());

        public Task OpenSplitPaymentDialogFromCompletionAsync(string hostId) =>
            OpenSplitPaymentDialogAsync(hostId);

        private async Task OpenSplitPaymentDialogAsync(string hostId = "SplitPaymentDialogHost")
        {
            try
            {
                // For session-based orders, force-load session total before opening split
                // so split total always matches the session payable amount.
                if (_order?.OrderSessionId.HasValue == true && _order.OrderSessionId.Value > 0)
                {
                    await EnsureSessionTotalLoadedAsync().ConfigureAwait(true);
                }

                var splitDialogHostId = string.IsNullOrWhiteSpace(hostId) ? "SplitPaymentDialogHost" : hostId;

                // If split is requested from inside CheckoutDialog, close checkout first
                // and reopen split on the parent/root host.
                if (string.Equals(splitDialogHostId, "SplitPaymentDialogHost", StringComparison.Ordinal)
                    && DialogHost.IsDialogOpen(_dialogHostId))
                {
                    DialogHost.Close(_dialogHostId, null);
                    await Task.Delay(50).ConfigureAwait(true);
                    splitDialogHostId = _dialogHostId;
                }

                _activeSplitDialogHostId = splitDialogHostId;
                var displayId = !string.IsNullOrWhiteSpace(_order?.DisplayOrderId)
                    ? _order.DisplayOrderId
                    : (_order?.OrderNumber ?? _order?.ApiId.ToString() ?? "");
                var hasSessionId = _order?.OrderSessionId.HasValue == true && _order.OrderSessionId.Value > 0;
                var tempPaymentTypeId = hasSessionId ? _order.OrderSessionId.Value.ToString() : displayId;
                var tempPaymentType = hasSessionId ? "SESSION" : "ORDER";
                var splitOrderTotal = hasSessionId && _sessionTotalAmount.HasValue
                    ? _sessionTotalAmount.Value
                    : SubTotal;
                var vm = new SplitPaymentDialogViewModel(
                    splitOrderTotal,
                    splitDialogHostId,
                    runCardPaymentAsync: RunCardPaymentForSplitAsync,
                    onCardPaymentError: (title, message) =>
                    {
                        var errVm = StatusDialogViewModel.CreateError(title, message);
                        _ = ShowKitchenSplitOverlayDialogAsync(errVm);
                    },
                    openCashDrawerOnSplitCashCharge: () => { try { OpenCashDrawer(); } catch { } },
                    orderDisplayOrderId: displayId,
                    tempPaymentTypeId: tempPaymentTypeId,
                    tempPaymentType: tempPaymentType);
                await vm.LoadExistingTempPaymentsAsync();
                var view = new POS_UI.View.SplitPaymentDialog { DataContext = vm };
                var result = await DialogHost.Show(view, splitDialogHostId);
                if (result is SplitPaymentDialogResult splitResult)
                {
                    if (splitResult.Confirmed && splitResult.Payments != null && splitResult.Payments.Count > 0)
                    {
                        PendingSplitPayments = splitResult.Payments;
                        await Task.Delay(50);
                        await FinishOrderAsync();
                    }
                    else if (!splitResult.Confirmed && splitResult.Payments != null && splitResult.Payments.Count > 0)
                    {
                        PendingSplitPayments = splitResult.Payments;
                        var msg = StatusDialogViewModel.CreateInfo("Split payment", $"{splitResult.Payments.Count} paid split(s) were saved and will be included when you complete the order.");
                        DialogHost.Show(new POS_UI.View.StatusDialog { DataContext = msg }, "RootDialog");
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
                var errVm = StatusDialogViewModel.CreateError("Split Payment", $"Failed to open: {ex.Message}");
                DialogHost.Show(new POS_UI.View.StatusDialog { DataContext = errVm }, _dialogHostId);
            }
        }

        private async Task<CardTransactionResult> RunCardPaymentForSplitAsync(decimal amount, string reference)
        {
            var cardMachines = CardMachineService.Instance.CardMachines;
            var availableCardMachines = cardMachines.Where(cm => cm.IsActive).ToList();
            if (!availableCardMachines.Any())
            {
                var errVm = StatusDialogViewModel.CreateWarning("Card Machine Not Available", "No active card machines available. Please activate a card machine in settings or select a different payment method.");
                await ShowKitchenSplitOverlayDialogAsync(errVm).ConfigureAwait(true);
                return new CardTransactionResult { IsSuccess = false, UserAlreadyNotifiedOfFailure = true, ErrorMessage = "No active card machines available." };
            }
            var machinesWithAuth = availableCardMachines.Where(cm => !string.IsNullOrEmpty(cm.AuthToken)).ToList();
            if (!machinesWithAuth.Any())
            {
                var errVm = StatusDialogViewModel.CreateWarning("Card Machine Not Authorized", "No authorized card machines found. Please authorize a card machine in settings.");
                await ShowKitchenSplitOverlayDialogAsync(errVm).ConfigureAwait(true);
                return new CardTransactionResult { IsSuccess = false, UserAlreadyNotifiedOfFailure = true, ErrorMessage = "No authorized card machines found. Please authorize a card machine in settings." };
            }
            var refId = !string.IsNullOrWhiteSpace(_order.DisplayOrderId) ? _order.DisplayOrderId : reference;
            return await _cardMachineApiService.ProcessCardPaymentAsync(machinesWithAuth.First(), amount, refId);
        }

        public ICommand NumberPadCommand => new RelayCommand<string>(key =>
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            // Handle backspace
            if (string.Equals(key, "Backspace", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(CashInputString))
                {
                    CashInputString = CashInputString.Substring(0, CashInputString.Length - 1);
                }
                return;
            }

            // Validate input keys: digits and single decimal point
            if (key == ".")
            {
                if (CashInputString.Contains(".")) return; // prevent multiple dots
                CashInputString = string.IsNullOrEmpty(CashInputString) ? "0." : CashInputString + ".";
                return;
            }

            if (key.Length == 1 && char.IsDigit(key[0]))
            {
                // Append digit with max 2 decimals
                var next = CashInputString + key;
                if (IsValidCurrencyInput(next))
                {
                    CashInputString = TrimLeadingZeros(next);
                }
            }
        });

        public ICommand ClearCashGivenCommand => new RelayCommand(() =>
        {
            CashInputString = string.Empty;
        });

        private static bool IsValidCurrencyInput(string input)
        {
            if (string.IsNullOrEmpty(input)) return true;
            // Allow up to 2 decimal places
            var parts = input.Split('.');
            if (parts.Length > 2) return false;
            if (!long.TryParse(parts[0] == string.Empty ? "0" : parts[0], out _)) return false;
            if (parts.Length == 2)
            {
                if (parts[1].Length > 2) return false;
                if (parts[1].Length > 0 && !int.TryParse(parts[1], out _)) return false;
            }
            return true;
        }

        private static string TrimLeadingZeros(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (input.StartsWith("0") && !input.StartsWith("0."))
            {
                // remove leading zeros but keep at least one
                var i = 0;
                while (i < input.Length - 1 && input[i] == '0' && char.IsDigit(input[i + 1])) i++;
                input = input.Substring(i);
            }
            return input;
        }

        private async Task FinishOrderAsync()
        {
            try
            {
                IsLoading = true;
                // For platform 8 (table order), ensure paying amount is session total
                if (IsTableOrderPlatform && _order.OrderSessionId.HasValue && _order.OrderSessionId.Value > 0)
                    await EnsureSessionTotalLoadedAsync();
                var pmUpper = SelectedPaymentMethod.ToString().ToUpper();
                var isManualCard = pmUpper == "MANUALCARD";
                var isTerminalCard = pmUpper == "CARD";
                var isCash = pmUpper == "CASH";
                var isCod = pmUpper == "COD";

                var methodForApi = (isManualCard || isTerminalCard) ? "CARD" : (isCod ? "COD" : "CASH");
                var hasPendingSplitPayments = PendingSplitPayments != null && PendingSplitPayments.Count > 0;

                // For terminal card payments, ensure there is an active and authorized card machine
                if (isTerminalCard && !hasPendingSplitPayments)
                {
                    var cardMachines = CardMachineService.Instance.CardMachines;
                    var availableCardMachines = cardMachines.Where(cm => cm.IsActive).ToList();

                    if (!availableCardMachines.Any())
                    {
                        var vmNoMachines = StatusDialogViewModel.CreateWarning("Card Machine Not Available", "No active card machines available. Please activate a card machine in settings or select a different payment method.");
                        IsLoading = false;
                        await ShowStatusThenReopenAsync(vmNoMachines);
                        return; // stop proceed
                    }

                    var machinesWithAuth = availableCardMachines.Where(cm => !string.IsNullOrEmpty(cm.AuthToken)).ToList();
                    if (!machinesWithAuth.Any())
                    {
                        var vmNoAuth = StatusDialogViewModel.CreateWarning("Card Machine Not Authorized", "No authorized card machines found. Please authorize a card machine in settings.");
                        IsLoading = false;
                        await ShowStatusThenReopenAsync(vmNoAuth);
                        return; // stop proceed
                    }

                    // Process card transaction with the first authorized machine
                    var selectedMachine = machinesWithAuth.First();
                    var reference = !string.IsNullOrWhiteSpace(_order.DisplayOrderId)
                        ? _order.DisplayOrderId
                        : (_order.OrderNumber ?? _order.ApiId.ToString());

                    var transactionResult = await _cardMachineApiService.ProcessCardPaymentAsync(selectedMachine, SubTotal, reference);

                    if (transactionResult == null)
                    {
                        var vmCancelled = StatusDialogViewModel.CreateWarning("Card Payment", "Card payment was cancelled.");
                        IsLoading = false;
                        await ShowStatusThenReopenAsync(vmCancelled);
                        return;
                    }

                    if (!transactionResult.IsSuccess)
                    {
                        var vmFailed = StatusDialogViewModel.CreateError("Card Payment Failed", transactionResult.ErrorMessage ?? "Card transaction failed.");
                        IsLoading = false;
                        await ShowStatusThenReopenAsync(vmFailed);
                        return;
                    }
                }

                System.Collections.Generic.List<PaymentModel> payments;
                if (hasPendingSplitPayments)
                {
                    payments = SplitPaymentItem.ToPaymentModelsForOrder(PendingSplitPayments);
                }
                else
                {
                    payments = new System.Collections.Generic.List<PaymentModel>
                    {
                        new PaymentModel
                        {
                            PaymentMethod = methodForApi,
                            // Backend expects paying_amount to match order total
                            PayingAmount = SubTotal,
                            Cash = (isCash || isCod) ? (isCash ? CashGiven : SubTotal) : 0m,
                            Balance = isCash ? (CashGiven - SubTotal) : 0m,
                            TransactionId = isTerminalCard ? "terminal" : (isManualCard ? "manualcard" : string.Empty)
                        }
                    };
                }

                if (_order.PlatformId == 9 || _order.PlatformId2 == 9)
                {
                    await _apiService.UpdateOrderPaymentAsync(_order.ApiId, payments);
                }
                else if (_order.PlatformId == 6 || _order.PlatformId2 == 6)
                {
                    var remoteOrderId = !string.IsNullOrWhiteSpace(_order.RemoteOrderId)
                        ? _order.RemoteOrderId
                        : (!string.IsNullOrWhiteSpace(_order.DisplayOrderId) ? _order.DisplayOrderId : (_order.OrderNumber ?? _order.ApiId.ToString()));
                    if (hasPendingSplitPayments && PendingSplitPayments != null && PendingSplitPayments.Count > 0)
                    {
                        if (SplitPaymentItem.IsAllCashSplits(PendingSplitPayments))
                            await _apiService.NotifyCompleteOrderToDeliveryPlatformAsync(remoteOrderId, "CASH");
                        else
                        {
                            var splitLines = SplitPaymentItem.ToDeliveryPlatformSplitLines(PendingSplitPayments);
                            await _apiService.NotifyCompleteOrderToDeliveryPlatformAsync(remoteOrderId, "SPLIT", splitLines);
                        }
                    }
                    else
                    {
                        await _apiService.NotifyCompleteOrderToDeliveryPlatformAsync(remoteOrderId, methodForApi);
                    }
                }
                else if (_order.PlatformId == 8 || _order.PlatformId2 == 8)
                {
                    /*int[] tableIds = null;
                    if (_order.OrderSessionId.HasValue && _order.OrderSessionId.Value > 0)
                    {
                        tableIds = await _apiService.GetTableIdsFromSessionAsync(_order.OrderSessionId.Value);
                    }
                    if ((tableIds == null || tableIds.Length == 0) && _order.TableNumber.HasValue && _order.TableNumber.Value > 0)
                    {
                        tableIds = new[] { _order.TableNumber.Value };
                    }
                    if (tableIds != null && tableIds.Length > 0)
                    {
                        foreach (var id in tableIds)
                            await _apiService.UpdateTableStatusAsync(id, "AVAILABLE", 0);
                    }*/
                    var remoteOrderId = !string.IsNullOrWhiteSpace(_order.RemoteOrderId)
                        ? _order.RemoteOrderId
                        : (!string.IsNullOrWhiteSpace(_order.DisplayOrderId) ? _order.DisplayOrderId : (_order.OrderNumber ?? _order.ApiId.ToString()));
                    if (hasPendingSplitPayments && PendingSplitPayments != null && PendingSplitPayments.Count > 0)
                    {
                        if (SplitPaymentItem.IsAllCashSplits(PendingSplitPayments))
                            await _apiService.NotifyCompleteOrderToDeliveryPlatformAsync(remoteOrderId, "CASH");
                        else
                        {
                            var splitLines = SplitPaymentItem.ToDeliveryPlatformSplitLines(PendingSplitPayments);
                            await _apiService.NotifyCompleteOrderToDeliveryPlatformAsync(remoteOrderId, "SPLIT", splitLines);
                        }
                    }
                    else
                    {
                        await _apiService.NotifyCompleteOrderToDeliveryPlatformAsync(remoteOrderId, methodForApi);
                    }
                }
                else
                {
                    await _apiService.UpdateOrderStatusAsync(_order.ApiId, "COMPLETED");
                }

                _order.ApiStatus = "COMPLETED";
                GlobalDataService.Instance.NotifyOrderStatusChanged(_order.ApiId, "COMPLETED");
               /* if (!(_order.PlatformId == 1 || _order.PlatformId == 2 || _order.PlatformId == 6))
                {
                    await _apiService.UpdateOrderStatusAsync(_order.ApiId, "COMPLETED");
                }
                else
                {
                    var remoteOrderId = !string.IsNullOrWhiteSpace(_order.RemoteOrderId)
                        ? _order.RemoteOrderId
                        : (!string.IsNullOrWhiteSpace(_order.DisplayOrderId) ? _order.DisplayOrderId : (_order.OrderNumber ?? _order.ApiId.ToString()));
                    await _apiService.NotifyCompleteOrderToDeliveryPlatformAsync(remoteOrderId);
                }*/

                // Record cash drawer movement for platforms that require cash movement tracking.
                var isCashMovementPlatform =
                    _order.PlatformId == 6 || _order.PlatformId2 == 6 ||
                    _order.PlatformId == 8 || _order.PlatformId2 == 8;
                var splitCashAmount = hasPendingSplitPayments && PendingSplitPayments != null
                    ? PendingSplitPayments
                        .Where(p => p != null && string.Equals(p.PaymentMethod.ToString(), "Cash", StringComparison.OrdinalIgnoreCase))
                        .Sum(p => p.Amount)
                    : 0m;
                var cashMovementAmount = isCash ? SubTotal : splitCashAmount;

                if (isCashMovementPlatform && cashMovementAmount > 0m)
                {
                    try
                    {
                        await _apiService.RecordCashMovementAsync("OTHER_SALES", cashMovementAmount, "");
                    }
                    catch { /* non-fatal: do not block order completion */ }
                }

                _order.PaymentStatus = "PAID";
                _order.IsPaid = true;

                // Persist last cash context for receipt printing
                try
                {
                    if (isCash)
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

                // Reused for success dialog: all DisplayOrderIds in session (table checkout)
                SessionOrdersResponse sessionOrdersSnapshot = null;

                // Print ONLY main receipt with cash balance
                try
                {
                        var pm = hasPendingSplitPayments && PendingSplitPayments != null && PendingSplitPayments.Count > 0
                            ? SplitPaymentItem.FormatReceiptPaymentSummary(PendingSplitPayments, GlobalDataService.Instance.ShopDetails?.Currency ?? "£")
                            : (isCash ? "CASH" : (isTerminalCard ? "CARD" : (isManualCard ? "CARD" : "")));
                        
                        // If order has a session id, print receipts for all orders in that session
                        if (_order.OrderSessionId.HasValue && _order.OrderSessionId.Value > 0)
                        {
                            try
                            {
                                var response = await _apiService.GetSessionOrdersAsync(_order.OrderSessionId.Value);
                                if (response?.Data?.OrderDetails != null && response.Data.OrderDetails.Count > 0)
                                {
                                    // Reuse this response for success dialog so we do not call session-orders again
                                    // (second call may fail silently or return no rows after checkout, losing multiple DisplayOrderIds).
                                    sessionOrdersSnapshot = response;
                                    foreach (var od in response.Data.OrderDetails)
                                    {
                                        if (od.OrderApiId <= 0) continue;
                                        try
                                        {
                                            var fullOrder = await _apiService.GetOrderByIdAsync(od.OrderApiId);
                                            if (fullOrder != null)
                                            {
                                                fullOrder.PaymentMethod = pm;
                                                // Completion print: use MainReceipt flag only; ignore MainReceiptOnOrder.
                                                await POS_UI.Services.ReceiptPrintingService.Instance.PrintIncomingMainReceiptAsync(fullOrder, pm, onlyWhenMainReceiptOnOrder: false);
                                            }
                                        }
                                        catch
                                        {
                                            // Continue with other orders if one fails
                                        }
                                    }
                                }
                                else
                                {
                                    // Fallback: print current order only if session has no order details
                                    await PrintSingleOrderReceiptAsync(pm);
                                }
                            }
                            catch
                            {
                                // If session fetch fails, print current order only
                                await PrintSingleOrderReceiptAsync(pm);
                            }
                        }
                        else
                        {
                            await PrintSingleOrderReceiptAsync(pm);
                        }
                    
                }
                catch { /* non-fatal printing error */ }

                PendingSplitPayments = null;

                //await KitchenViewModel.MoveToCompletedStatic(_order);

                // Show success status after successful payment/completion
                try
                {
                    // Close checkout dialog if still open to avoid nested root host issues
                    if (DialogHost.IsDialogOpen(_dialogHostId))
                    {
                        DialogHost.Close(_dialogHostId);
                        await Task.Delay(100);
                    }

                    var orderRef = await BuildSuccessDialogOrderRefAsync(sessionOrdersSnapshot);
                    var isCashPayment = string.Equals(SelectedPaymentMethod.ToString(), "Cash", System.StringComparison.OrdinalIgnoreCase);
                    var successVm = isCashPayment
                        ? StatusDialogViewModel.CreateCompletedPaymentSuccess("Payment Successful", CashGiven, SubTotal, Math.Max(CashBalance, 0.0m), orderRef)
                        : StatusDialogViewModel.CreateSuccess("Payment Successful", $"Order {orderRef} completed successfully.");
                    // Open cash drawer immediately upon API success, before displaying success dialog
                    //MessageBox.Show("the is cash payment is: " + isCashPayment);
                    if (isCashPayment)
                    {
                        try { OpenCashDrawer(); } catch { }
                    }
                    var successDlg = new POS_UI.View.StatusDialog { DataContext = successVm };
                    await DialogHost.Show(successDlg, _dialogHostId);
                }
                catch { }

                // Refresh kitchen page so completed order is removed from queue
                try { POS_UI.Services.GlobalDataService.Instance.RequestKitchenRefresh(); } catch { }

                // Refresh Live Orders page when completing a TableOrder
                if ((_order.PlatformId == 8 || _order.PlatformId2 == 8) && ViewModels.LiveOrdersViewModel.Instance != null)
                    _ = ViewModels.LiveOrdersViewModel.Instance.LoadOrdersAsync();

                // Refresh Tables page when checkout was opened from Tables (RootDialogHost) and order is table order
                if ((_order.PlatformId == 8 || _order.PlatformId2 == 8) && _dialogHostId == "RootDialogHost")
                    try { POS_UI.Services.GlobalDataService.Instance.RequestTablesRefresh(); } catch { }
                  
                if (DialogHost.IsDialogOpen(_dialogHostId))
                {
                    DialogHost.Close(_dialogHostId, true);
                }
            }
            catch (System.Exception ex)
            {
                var vm = StatusDialogViewModel.CreateError("Payment Failed", $"Failed to finish order: {ex.Message}");
                await ShowStatusThenReopenAsync(vm);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ShowKitchenSplitOverlayDialogAsync(StatusDialogViewModel vm)
        {
            try
            {
                if (DialogHost.IsDialogOpen(KitchenSplitOverlayHostId))
                    DialogHost.Close(KitchenSplitOverlayHostId);
                await Task.Delay(50).ConfigureAwait(true);
                await DialogHost.Show(new POS_UI.View.StatusDialog { DataContext = vm }, KitchenSplitOverlayHostId).ConfigureAwait(true);
            }
            catch
            {
                var icon = vm.Variant == StatusVariant.Warning ? MessageBoxImage.Warning : MessageBoxImage.Error;
                MessageBox.Show(vm.Message ?? "", vm.Header ?? "Error", MessageBoxButton.OK, icon);
            }
        }

        /// <summary>When the main dialog host is busy, show cash drawer errors on the overlay host.</summary>
        private void ShowCashDrawerErrorStatusDialog(StatusDialogViewModel vm)
        {
            var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
            if (DialogHost.IsDialogOpen(_dialogHostId))
            {
                try
                {
                    if (DialogHost.IsDialogOpen(KitchenSplitOverlayHostId))
                        DialogHost.Close(KitchenSplitOverlayHostId);
                    DialogHost.Show(dlg, KitchenSplitOverlayHostId);
                    return;
                }
                catch
                {
                    MessageBox.Show(vm.Message ?? "", vm.Header ?? "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            DialogHost.Show(dlg, _dialogHostId);
        }

        private async void OpenCashDrawer()
        {
            try
            {
                // Same logic as CashierHomeViewModel: send pulse to all active printers; if none active, use all discovered printers
                var printersService = PrintersService.Instance;
                var targetPrinters = printersService.Printers.Where(p => p.IsActive).Select(p => p.DeviceName).ToList();
                if (targetPrinters.Count == 0)
                {
                    targetPrinters = printersService.Printers.Select(p => p.DeviceName).ToList();
                }
                if (targetPrinters.Count == 0)
                {
                    var vm = StatusDialogViewModel.CreateError("Failed", "Failed to trigger cash drawer.");
                    ShowCashDrawerErrorStatusDialog(vm);
                    return;
                }

                // ESC/POS pulse command to open cash drawer (Kick-out) - same as when creating order with cash
                byte[] openDrawerCommand = new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA };
                bool anySuccess = false;
                foreach (var pn in targetPrinters.Distinct())
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(pn)) continue;
                        if (RawPrinterHelper.SendBytesToPrinter(pn, openDrawerCommand))
                            anySuccess = true;
                    }
                    catch { }
                }
                if (!anySuccess)
                {
                    var vm = StatusDialogViewModel.CreateError("Failed", "Failed to trigger cash drawer.");
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
            catch (System.Exception ex)
            {
                var vm = StatusDialogViewModel.CreateError("Error Opening Cash Drawer", $"Error opening cash drawer: {ex.Message}");
                ShowCashDrawerErrorStatusDialog(vm);
            }
        }

        private async Task ShowStatusThenReopenAsync(StatusDialogViewModel vm)
        {
            // Close current checkout dialog if open
            if (DialogHost.IsDialogOpen(_dialogHostId))
            {
                DialogHost.Close(_dialogHostId);
                await Task.Delay(100);
            }

            var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
            await DialogHost.Show(dlg, _dialogHostId);

            // Reopen checkout dialog so user can retry
            var checkoutDialog = new POS_UI.View.CheckoutDialog { DataContext = this };
            await DialogHost.Show(checkoutDialog, _dialogHostId);
        }
    }
}


