using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using POS_UI.Models;
using POS_UI.Services;
using POS_UI.View;
using MaterialDesignThemes.Wpf;

namespace POS_UI.ViewModels
{
    public class RefundFlowDialogViewModel : BaseViewModel
    {
        public enum RefundStep
        {
            AmountEntry,
            ReasonSelection,
            ModeSelection,
            Summary
        }

        /// <summary>Initial step is set to ModeSelection, or AmountEntry when mode selection is skipped.</summary>
        private RefundStep _currentStep = RefundStep.ModeSelection;
        public RefundStep CurrentStep
        {
            get => _currentStep;
            set
            {
                _currentStep = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAmountStep));
                OnPropertyChanged(nameof(IsReasonStep));
                OnPropertyChanged(nameof(IsModeStep));
                OnPropertyChanged(nameof(IsSummaryStep));
                OnPropertyChanged(nameof(SummaryRefundModeDisplay));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(StepTitle));
                OnPropertyChanged(nameof(ShowCashDrawerBalanceErrorInSummary));
                OnPropertyChanged(nameof(CanConfirmRefund));
                OnPropertyChanged(nameof(ShowCashDrawerBalanceErrorOnAmountStep));
                OnPropertyChanged(nameof(ShowCardRefundBalanceZeroWarning));

                // Cash drawer balance is checked on the amount step (after Cash is selected), not on mode selection.
                if (value == RefundStep.AmountEntry && string.Equals(SelectedMode?.Trim(), "Cash", StringComparison.OrdinalIgnoreCase))
                    _ = ValidateCashDrawerBalanceAsync();

                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    (NextCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }));
            }
        }

        /// Insufficient drawer message on the amount step when refund mode is Cash.
        public bool ShowCashDrawerBalanceErrorOnAmountStep =>
            IsAmountStep
            && string.Equals(SelectedMode?.Trim(), "Cash", StringComparison.OrdinalIgnoreCase)
            && HasCashDrawerBalanceError
            && !IsOrderPaymentModeCash;

        public bool IsAmountStep => CurrentStep == RefundStep.AmountEntry;
        public bool IsReasonStep => CurrentStep == RefundStep.ReasonSelection;
        public bool IsModeStep => CurrentStep == RefundStep.ModeSelection;
        public bool IsSummaryStep => CurrentStep == RefundStep.Summary;

        public string StepTitle
        {
            get
            {
                return CurrentStep switch
                {
                    RefundStep.AmountEntry => "RefundReason",
                    RefundStep.ReasonSelection => "RefundReason",
                    RefundStep.ModeSelection => "Refund Mode",
                    RefundStep.Summary => "Refund Summary",
                    _ => "Refund"
                };
            }
        }

        // Order information
        private OrderModel _order;
        public OrderModel Order
        {
            get => _order;
            set
            {
                _order = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OrderNumber));
                OnPropertyChanged(nameof(OrderTime));
                OnPropertyChanged(nameof(OrderTotal));
                OnPropertyChanged(nameof(RefundBalance));
                OnPropertyChanged(nameof(IsOrderPaymentModeCash));
                OnPropertyChanged(nameof(ShowCashDrawerBalanceErrorInSummary));
                OnPropertyChanged(nameof(ShowCashDrawerBalanceErrorOnAmountStep));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(SessionTotalAmount));
                OnPropertyChanged(nameof(ShowSessionTotal));
                OnPropertyChanged(nameof(RefundableAmountLabel));
                OnPropertyChanged(nameof(ShowCardRefundBalanceZeroWarning));
            }
        }

        //exactly ONE transaction where TransactionType = SALE and TransactionMode = CARD or CASH
        public bool IsSingleSaleCardOrCashTransaction
        {
            get
            {
                if (Order?.Transactions == null)
                    return false;

                return Order.Transactions.Count(t =>
                    (t.TransactionType ?? "").Trim().ToUpperInvariant() == "SALE" &&
                    (
                        (t.TransactionMode ?? "").Trim().ToUpperInvariant() == "CARD" ||
                        (t.TransactionMode ?? "").Trim().ToUpperInvariant() == "CASH"
                    )
                ) == 1;
            }
        }

        public string OrderNumber => Order?.OrderNumber ?? Order?.DisplayOrderId ?? "";
        public string OrderTime => Order?.DeliveryDateTime?.ToUniversalTime().ToString("HH:mm") ?? "";
        public decimal OrderTotal => Order?.ApiTotal ?? 0m;
        private string _sessionTotalAmount = "";
        public string SessionTotalAmount
        {
            get => _sessionTotalAmount;
            set
            {
                _sessionTotalAmount = value ?? "";
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowSessionTotal));
            }
        }
        public bool ShowSessionTotal =>
            !IsCancelOrderFlow
            && Order?.OrderSessionId.HasValue == true
            && Order.OrderSessionId.Value > 0
            && !string.IsNullOrWhiteSpace(SessionTotalAmount);
        public string RefundableAmountLabel =>
            !IsCancelOrderFlow
            && Order?.OrderSessionId.HasValue == true
            && Order.OrderSessionId.Value > 0
                ? "Refundable Amount (Session) : "
                : "Refundable Amount : ";

        private bool IsSplitPaymentOrder =>
            Order != null &&
            string.Equals(Order.PaymentMode?.Trim(), "SPLIT", StringComparison.OrdinalIgnoreCase);

        private static bool IsCardOrManualRefundMode(string mode) =>
            string.Equals(mode?.Trim(), "Card", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode?.Trim(), "Manual Card", StringComparison.OrdinalIgnoreCase);

        /// <summary>API returned refund buckets and the card leg is zero — Card/Manual cannot proceed; use Cash.</summary>
        private bool IsZeroApiCardRefundBalance =>
            Order?.RefundBalances != null
            && Math.Round(Order.RefundBalances.CardRefundBalance, 2, MidpointRounding.AwayFromZero) <= 0m;

        /// <summary>Shown on refund mode step when Card or Manual Card is selected but card refund balance is zero.</summary>
        public bool ShowCardRefundBalanceZeroWarning =>
            IsModeStep
            && IsCardOrManualRefundMode(SelectedMode)
            && IsZeroApiCardRefundBalance
            && !IsCancelFlowSessionOrder;

        public string CardRefundBalanceZeroMessage
        {
            get
            {
                var sym = GlobalDataService.Instance?.ShopDetails?.Currency ?? "£";
                return $"The card balance for refund is {sym}0.00. Continue the refund via cash.";
            }
        }

        private bool BlocksNextDueToZeroCardRefundBalance() =>
            IsCardOrManualRefundMode(SelectedMode)
            && IsZeroApiCardRefundBalance
            && !IsCancelFlowSessionOrder;

        /// <summary>Cancel + table/session order: cap refundable UI and validation to this order's total, not session aggregate refund balance.</summary>
        private bool IsCancelFlowSessionOrder =>
            IsCancelOrderFlow
            && Order?.OrderSessionId.HasValue == true
            && Order.OrderSessionId.Value > 0;

        /// <summary>Refundable cap for the current step: split payment + Card refund mode uses the card leg from the API.</summary>
        public decimal RefundBalance
        {
            get
            {
                if (Order == null) return 0m;
                if (IsCancelFlowSessionOrder)
                    return OrderTotal;
                if (IsSplitPaymentOrder
                    && (string.Equals(SelectedMode?.Trim(), "Card", StringComparison.OrdinalIgnoreCase) || string.Equals(SelectedMode?.Trim(), "Manual Card", StringComparison.OrdinalIgnoreCase)))
                    return Order.RefundBalances?.CardRefundBalance ?? Order.RefundBalance;
                return Order.RefundBalance;
            }
        }

        /// <summary>Use SessionPaymentType when not null/empty, otherwise PaymentType.</summary>
        private static string GetEffectivePaymentType(OrderModel order)
        {
            if (order == null) return "";
            var s = !string.IsNullOrWhiteSpace(order.SessionPaymentType) ? order.SessionPaymentType : order.PaymentType;
            return (s ?? "").Trim();
        }

        // Non-POS orders: when platform is not POS (PlatformId2 != 9) and payment is Stripe, PayHere or Verifone, refund amount is fixed
        public bool IsNonPosOrder => Order != null && Order.PlatformId2 != 9 &&
                                    (string.Equals(GetEffectivePaymentType(Order), "stripe", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(GetEffectivePaymentType(Order), "payhere", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(GetEffectivePaymentType(Order), "verifone", StringComparison.OrdinalIgnoreCase));

       //refund amount is fixed for non-pos orders
        public bool IsRefundAmountReadOnly => IsNonPosOrder;

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
                if (total > 0 && balance <= 0)
                    return "Refunded";
                if (total > 0 && balance < total)
                    return "Partially Refunded";*/
                var refundStatus = Order.RefundStatus;
                return string.IsNullOrWhiteSpace(refundStatus) ? Order.PaymentStatus ?? "" : refundStatus;
            }
        }
        public string PaymentMode => Order?.PaymentMode ?? "";

        // Step 1: Amount Entry
        private decimal _refundAmount;
        private string _refundAmountText = "";
        
        public decimal RefundAmount
        {
            get => _refundAmount;
            set
            {
                _refundAmount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoNext));
            }
        }

        public string RefundAmountText
        {
            get => _refundAmountText;
            set
            {
                string newValue = value ?? "";
                
                // If value contains decimal point, limit to 2 decimal places
                if (newValue.Contains("."))
                {
                    int decimalIndex = newValue.IndexOf(".");
                    string decimalPart = newValue.Substring(decimalIndex + 1);
                    if (decimalPart.Length > 2)
                    {
                        // Limit to 2 decimal places
                        newValue = newValue.Substring(0, decimalIndex + 1) + decimalPart.Substring(0, 2);
                    }
                }
                
                _refundAmountText = newValue;
                OnPropertyChanged();
                
                // Parse and update RefundAmount, rounding to 2 decimal places (non-POS: ignore input and keep RefundBalance)
                if (IsNonPosOrder)
                {
                    _refundAmount = RefundBalance;
                    _refundAmountText = RefundBalance > 0 ? RefundBalance.ToString() : "";
                    OnPropertyChanged(nameof(RefundAmountText));
                    OnPropertyChanged(nameof(RefundAmount));
                    OnPropertyChanged(nameof(CanGoNext));
                }
                else if (decimal.TryParse(_refundAmountText, out decimal amount))
                {
                    _refundAmount = Math.Round(amount, 2);
                    OnPropertyChanged(nameof(RefundAmount));
                    OnPropertyChanged(nameof(CanGoNext));
                    // Re-validate on amount step when refund mode is Cash
                    if (IsAmountStep && string.Equals(SelectedMode?.Trim(), "Cash", StringComparison.OrdinalIgnoreCase))
                        _ = ValidateCashDrawerBalanceAsync();
                }
                else if (string.IsNullOrWhiteSpace(_refundAmountText))
                {
                    _refundAmount = 0m;
                    OnPropertyChanged(nameof(RefundAmount));
                    OnPropertyChanged(nameof(CanGoNext));
                    // Clear error when amount is cleared
                    if (string.Equals(SelectedMode?.Trim(), "Cash", StringComparison.OrdinalIgnoreCase))
                        CashDrawerBalanceError = null;
                }
            }
        }

        // Step 2: Reason Selection
        public List<string> RefundReasons { get; } = new List<string>
        {
            "Restaurant closed",
            "Customer wants to cancel the order",
            "Rider not Available",
            "Item not Available",
            "Other"
        };

        private string _selectedReason;
        public string SelectedReason
        {
            get => _selectedReason;
            set
            {
                _selectedReason = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(IsOtherReason));
                OnPropertyChanged(nameof(FinalReason));
            }
        }

        private string _otherReason;
        public string OtherReason
        {
            get => _otherReason;
            set
            {
                _otherReason = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FinalReason));
            }
        }

        public bool IsOtherReason => SelectedReason == "Other";

        // Step 3: Mode Selection
        public List<string> RefundModes { get; } = new List<string>
        {
            "Cash",
            "Card",
            "Manual Card"
        };

        private string _selectedMode;
        public string SelectedMode
        {
            get => _selectedMode;
            set
            {
                _selectedMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(RefundBalance));
                OnPropertyChanged(nameof(ShowCashDrawerBalanceErrorOnAmountStep));
                // Drawer balance is validated on the amount entry step, not when picking Cash here.
                IsValidatingCashDrawer = false;
                CashDrawerBalanceError = null;
                if (IsAmountStep && string.Equals(value?.Trim(), "Cash", StringComparison.OrdinalIgnoreCase))
                    _ = ValidateCashDrawerBalanceAsync();
                OnPropertyChanged(nameof(SummaryRefundModeDisplay));
                OnPropertyChanged(nameof(ShowCardRefundBalanceZeroWarning));
                SyncRefundAmountToEffectiveBalance();
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    (NextCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }));
            }
        }

        //Refund mode text shown in the summary step.
        public string SummaryRefundModeDisplay
        {
            get
            {
                var mode = SelectedMode ?? "";
                if (Order == null || Order.PlatformId == 9 || Order.PlatformId2 == 9) return mode;
                if (!string.Equals(mode, "Card", StringComparison.OrdinalIgnoreCase)) return mode;
                var pt = GetEffectivePaymentType(Order).ToUpperInvariant();
                if (string.Equals(pt, "stripe", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pt, "payhere", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pt, "verifone", StringComparison.OrdinalIgnoreCase))
                {
                    return "CARD - PAYMENT GATEWAY REFUND";
                }
                return "CARD - MACHINE";
            }
        }

        // Loading state
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanConfirmRefund));
            }
        }

        public bool CanConfirmRefund => !IsLoading && !ShowCashDrawerBalanceErrorInSummary;

        // True while ValidateCashDrawerBalanceAsync is running (Cash + amount step / amount changes). Keeps Next disabled until result is known.
        private bool _isValidatingCashDrawer;
        public bool IsValidatingCashDrawer
        {
            get => _isValidatingCashDrawer;
            set
            {
                if (_isValidatingCashDrawer == value) return;
                _isValidatingCashDrawer = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoNext));
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    (NextCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }));
            }
        }

        // Cash drawer balance validation
        private string _cashDrawerBalanceError;
        public string CashDrawerBalanceError
        {
            get => _cashDrawerBalanceError;
            set
            {
                _cashDrawerBalanceError = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCashDrawerBalanceError));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(CashDrawerBalanceErrorForModeStep));
                OnPropertyChanged(nameof(CashDrawerBalanceErrorForSummary));
                OnPropertyChanged(nameof(ShowCashDrawerBalanceErrorInSummary));
                OnPropertyChanged(nameof(ShowCashDrawerBalanceErrorOnAmountStep));
                OnPropertyChanged(nameof(CanConfirmRefund));
                // Notify the Next button to re-evaluate CanExecute so it disables as soon as the error appears
                // (ValidateCashDrawerBalanceAsync may set this from a background thread, so use Dispatcher)
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    (NextCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ConfirmRefundCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (CancelAndRefundCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }));
            }
        }

        public bool HasCashDrawerBalanceError => !string.IsNullOrWhiteSpace(CashDrawerBalanceError);

        /// <summary>Long insufficient-balance message (shown on amount entry when refund mode is Cash).</summary>
        public string CashDrawerBalanceErrorForModeStep =>
            HasCashDrawerBalanceError
                ? "Insufficient cash drawer balance. Please select another refund mode or adjust the refund amount."
                : "";

        /// <summary>Message for summary step: short message to adjust refund amount only.</summary>
        public string CashDrawerBalanceErrorForSummary =>
            HasCashDrawerBalanceError
                ? "Insufficient cash drawer balance. Please adjust the refund amount."
                : "";

        /// <summary>True when order was paid with cash.</summary>
        public bool IsOrderPaymentModeCash =>
            Order != null && !string.IsNullOrWhiteSpace(Order.PaymentMode) &&
            (Order.PaymentMode.Trim().Equals("CASH", StringComparison.OrdinalIgnoreCase) ||
             Order.PaymentMode.Trim().Equals("Cash", StringComparison.OrdinalIgnoreCase));

        /// <summary>True when on summary step, order payment mode is cash, and there is a cash drawer balance error.</summary>
        public bool ShowCashDrawerBalanceErrorInSummary =>
            IsSummaryStep && IsOrderPaymentModeCash && HasCashDrawerBalanceError;

        // Cancel order flow flag
        private bool _isCancelOrderFlow;
        public bool IsCancelOrderFlow
        {
            get => _isCancelOrderFlow;
            set
            {
                if (_isCancelOrderFlow == value) return;
                _isCancelOrderFlow = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowSessionTotal));
                OnPropertyChanged(nameof(RefundableAmountLabel));
                OnPropertyChanged(nameof(RefundBalance));
                OnPropertyChanged(nameof(ShowCardRefundBalanceZeroWarning));
            }
        }

        // Commands
        public ICommand NextCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand BackToOrderCommand { get; }
        public ICommand ConfirmRefundCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand CancelAndRefundCommand { get; }
        public ICommand SelectReasonCommand { get; }
        public ICommand SelectModeCommand { get; }

        public Action<RefundResult> OnRefundComplete { get; set; }
        
        private readonly ApiService _apiService;
        private KitchenOrderDetailsDialogViewModel.DialogMode _dialogMode;

        public class RefundResult
        {
            public decimal Amount { get; set; }
            public string Reason { get; set; }
            public string Mode { get; set; }
            public bool IsConfirmed { get; set; }
        }

        public RefundFlowDialogViewModel(OrderModel order, bool isCancelOrderFlow = false, KitchenOrderDetailsDialogViewModel.DialogMode dialogMode = KitchenOrderDetailsDialogViewModel.DialogMode.Kitchen)
        {
            IsCancelOrderFlow = isCancelOrderFlow;
            Order = order;
            _dialogMode = dialogMode;
            _apiService = new ApiService();

            // If payment mode is cash, automatically set refund mode to cash
            if (order != null && !string.IsNullOrWhiteSpace(order.PaymentMode))
            {
                var paymentMode = order.PaymentMode.Trim();
                if (paymentMode.Equals("CASH", StringComparison.OrdinalIgnoreCase) || 
                    paymentMode.Equals("Cash", StringComparison.OrdinalIgnoreCase))
                {
                    SelectedMode = "Cash";
                }
            }

            // If platform is not 9, order is paid, and payment method is not pay-on-collection/delivery/later, set refund mode to Card
            if (order != null && string.IsNullOrWhiteSpace(SelectedMode) && order.PlatformId2 != 9
                && string.Equals(order.PaymentStatus?.Trim(), "paid", StringComparison.OrdinalIgnoreCase))
            {
                var pt = GetEffectivePaymentType(order).ToUpperInvariant();
                if ( string.Equals(pt, "stripe", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pt, "payhere", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pt, "verifone", StringComparison.OrdinalIgnoreCase))
                {
                    SelectedMode = "Card";
                }
            }

            NextCommand = new RelayCommand(GoNext, () => CanGoNext);
            BackCommand = new RelayCommand(GoBack, () => CanGoBack);
            BackToOrderCommand = new RelayCommand(() => _ = BackToOrderAsync());
            ConfirmRefundCommand = new RelayCommand(() => _ = ConfirmRefundAsync(), () => CanConfirmRefund);
            CancelCommand = new RelayCommand(Cancel);
            CancelAndRefundCommand = new RelayCommand(() => _ = CancelAndRefundAsync(), () => CanConfirmRefund);
            SelectReasonCommand = new RelayCommand<string>(reason => SelectedReason = reason);
            SelectModeCommand = new RelayCommand<string>(mode => SelectedMode = mode);

            // When refund mode is already determined (e.g. cash payment → Cash), skip the mode step and start at amount.
            var skipModeSelection = ShouldSkipModeSelection();
            CurrentStep = skipModeSelection ? RefundStep.AmountEntry : RefundStep.ModeSelection;

            // Cap and label use mode-specific balance (e.g. split + Card → card_refund_balance).
            decimal initialAmount = RefundBalance;
            RefundAmount = initialAmount;
            _refundAmountText = initialAmount > 0 ? initialAmount.ToString() : "";
            OnPropertyChanged(nameof(RefundAmountText));

            if (skipModeSelection && SelectedMode == "Cash")
                _ = ValidateCashDrawerBalanceAsync();

            _ = LoadSessionTotalAsync();
        }

        private async Task LoadSessionTotalAsync()
        {
            try
            {
                if (IsCancelOrderFlow)
                {
                    SessionTotalAmount = "";
                    return;
                }

                if (Order?.OrderSessionId.HasValue != true || Order.OrderSessionId.Value <= 0)
                {
                    SessionTotalAmount = "";
                    return;
                }

                var response = await _apiService.GetSessionOrdersAsync(Order.OrderSessionId.Value);
                var totalAmount = response?.Data?.TotalAmount;
                SessionTotalAmount = !string.IsNullOrWhiteSpace(totalAmount) ? totalAmount : "";
            }
            catch
            {
                SessionTotalAmount = "";
            }
        }

    private void SyncRefundAmountToEffectiveBalance()
        {
            if (IsNonPosOrder)
            {
                var cap = RefundBalance;
                _refundAmount = cap;
                _refundAmountText = cap > 0 ? cap.ToString() : "";
                OnPropertyChanged(nameof(RefundAmount));
                OnPropertyChanged(nameof(RefundAmountText));
                OnPropertyChanged(nameof(CanGoNext));
                return;
            }

            var max = RefundBalance;
            if (RefundAmount > max)
            {
                _refundAmount = max;
                _refundAmountText = max > 0 ? max.ToString() : "";
                OnPropertyChanged(nameof(RefundAmount));
                OnPropertyChanged(nameof(RefundAmountText));
                OnPropertyChanged(nameof(CanGoNext));
                if (IsAmountStep && string.Equals(SelectedMode?.Trim(), "Cash", StringComparison.OrdinalIgnoreCase))
                    _ = ValidateCashDrawerBalanceAsync();
            }
        }

        public bool CanGoNext
        {
            get
            {
                return CurrentStep switch
                {
                    RefundStep.AmountEntry =>
                        RefundAmount > 0
                        && (IsNonPosOrder ? RefundAmount == RefundBalance : RefundAmount <= RefundBalance)
                        && (!string.Equals(SelectedMode?.Trim(), "Cash", StringComparison.OrdinalIgnoreCase)
                            || (IsOrderPaymentModeCash
                                ? !IsValidatingCashDrawer
                                : !HasCashDrawerBalanceError && !IsValidatingCashDrawer)),
                    RefundStep.ReasonSelection => !string.IsNullOrWhiteSpace(SelectedReason) && (!IsOtherReason || !string.IsNullOrWhiteSpace(OtherReason)),
                    RefundStep.ModeSelection => !string.IsNullOrWhiteSpace(SelectedMode) && !BlocksNextDueToZeroCardRefundBalance(),
                    RefundStep.Summary => true, // Always allow confirming from summary
                    _ => false
                };
            }
        }

        public bool CanGoBack =>
            CurrentStep != (ShouldSkipModeSelection() ? RefundStep.AmountEntry : RefundStep.ModeSelection);

        private void GoNext()
        {
            switch (CurrentStep)
            {
                case RefundStep.ModeSelection:
                    CurrentStep = RefundStep.AmountEntry;
                    break;
                case RefundStep.AmountEntry:
                    CurrentStep = RefundStep.ReasonSelection;
                    break;
                case RefundStep.ReasonSelection:
                    // Same as legacy flow: validate cash drawer before summary when refunding as Cash
                    if (SelectedMode == "Cash")
                        _ = ValidateCashDrawerBalanceAsync();
                    CurrentStep = RefundStep.Summary;
                    break;
            }
        }

        private void GoBack()
        {
            switch (CurrentStep)
            {
                case RefundStep.AmountEntry:
                    // Only return to mode step when that step exists in this flow
                    if (!ShouldSkipModeSelection())
                        CurrentStep = RefundStep.ModeSelection;
                    break;
                case RefundStep.ReasonSelection:
                    CurrentStep = RefundStep.AmountEntry;
                    break;
                case RefundStep.Summary:
                    CurrentStep = RefundStep.ReasonSelection;
                    break;
            }
        }

        private bool ShouldSkipModeSelection()
        {
            if (Order == null) return false;

            // Skip mode selection if payment mode is cash and refund mode is already set to Cash
            if (Order != null && !string.IsNullOrWhiteSpace(Order.PaymentMode))
            {
                var paymentMode = Order.PaymentMode.Trim();
                if ((paymentMode.Equals("CASH", StringComparison.OrdinalIgnoreCase) || 
                     paymentMode.Equals("Cash", StringComparison.OrdinalIgnoreCase)) &&
                    SelectedMode == "Cash")
                {
                    return true;
                }
            }

            // Skip mode selection when refund mode was auto-set to Card (platform not 9, paid, payment method not pay-on-collection/delivery/later)
            if (Order.PlatformId2 != 9
                && string.Equals(Order.PaymentStatus?.Trim(), "paid", StringComparison.OrdinalIgnoreCase)
                && SelectedMode == "Card")
            {
                var pt = GetEffectivePaymentType(Order).ToUpperInvariant();
                if (string.Equals(pt, "stripe", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pt, "payhere", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pt, "verifone", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the card machine should handle this refund (vs payment gateway).
        /// POS orders and in-person collected orders use the physical terminal.
        /// Online/webshop orders (Stripe etc.) are refunded via API gateway.
        /// </summary>
        private bool ShouldUseCardMachine()
        {
            if (!string.Equals(SelectedMode?.Trim(), "Card", StringComparison.OrdinalIgnoreCase))
                return false;

            // POS orders: paid via physical card terminal
            if (Order?.PlatformId2 == 9 || Order?.PlatformId == 9)
                return true;

            // Non-POS orders: only use card machine if payment was collected in person
            var pt = GetEffectivePaymentType(Order).ToUpperInvariant();
            if ( !string.Equals(pt, "stripe", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(pt, "payhere", StringComparison.OrdinalIgnoreCase) 
                && !string.Equals(pt, "verifone", StringComparison.OrdinalIgnoreCase))
                return true;

            // Online/webshop/table orders with gateway payment: refund via API, not card machine
            return false;
        }

        /// <summary>
        /// Sends a refund transaction to the physical card machine.
        /// Returns true if successful (or not needed), false if it failed and the refund should be blocked.
        /// </summary>
        private async Task<bool> ProcessCardMachineRefundIfNeededAsync(string selectedMode, decimal amount)
        {
            if (!string.Equals(selectedMode?.Trim(), "Card", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!ShouldUseCardMachine())
                return true;

            var machines = CardMachineService.Instance.CardMachines
                .Where(m => m.IsActive && !string.IsNullOrEmpty(m.AuthToken))
                .ToList();

            if (!machines.Any())
            {
                MessageBox.Show(
                    "No active authorized card machine available.\nPlease activate the card machine or select a different refund method.",
                    "Card Machine Not Available",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var cardApi = new CardMachineApiService();
            var reference = Order != null
                ? (!string.IsNullOrWhiteSpace(Order.DisplayOrderId)
                    ? Order.DisplayOrderId
                    : (Order.OrderNumber ?? Order.ApiId.ToString()))
                : "";

            var result = await cardApi.ProcessCardRefundAsync(
                machines.First(), amount, reference,
                status =>
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[CardRefund] Status: {status}");
                    });
                });

            if (!result.IsSuccess)
            {
                string errorMsg = result.IsCancelled
                    ? "Card refund was cancelled on the terminal."
                    : $"Card refund failed: {result.ErrorMessage}";
                MessageBox.Show(errorMsg, "Card Refund Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[CardRefund] Success - Auth: {result.AuthorisationCode}, Card: {result.CardPan}");
            return true;
        }

        private async Task ConfirmRefundAsync()
        {
            if (Order == null || Order.ApiId <= 0)
            {
                MessageBox.Show("Order information is missing. Cannot process refund.", "Invalid Order", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (RefundAmount <= 0)
            {
                MessageBox.Show("Refund amount must be greater than zero.", "Invalid Amount", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IsNonPosOrder && RefundAmount != RefundBalance)
            {
                MessageBox.Show("For this order the refund amount must equal the refundable amount.", "Invalid Amount", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedMode))
            {
                MessageBox.Show("Please select a refund mode.", "Invalid Mode", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Map refund mode: "Cash" -> "CASH", "Card" or "Manual Card" -> "CARD"
            string refundMode = SelectedMode?.Trim().ToUpperInvariant() switch
            {
                "CASH" => "CASH",
                "CARD" or "MANUAL CARD" => "CARD",
                _ => "CASH" // Default to CASH if unknown
            };

            string refundReason = IsOtherReason && !string.IsNullOrWhiteSpace(OtherReason) 
                ? OtherReason 
                : SelectedReason;

            if (string.IsNullOrWhiteSpace(refundReason))
            {
                MessageBox.Show("Please provide a refund reason.", "Invalid Reason", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Process card machine refund BEFORE recording in API (POS + in-person orders only, not gateway/Stripe)
            if (ShouldUseCardMachine())
            {
                IsLoading = true;
                bool cardSuccess = await ProcessCardMachineRefundIfNeededAsync(SelectedMode, RefundAmount);
                if (!cardSuccess)
                {
                    IsLoading = false;
                    return;
                }
            }

            IsLoading = true;
            try
            {
                var apiService = new ApiService();

                if (Order.PlatformId2 == 9)
                {
                    bool success = await apiService.ProcessOrderRefundAsync(
                        orderId: Order.ApiId,
                        refundMode: refundMode,
                        refundAmount: RefundAmount,
                        refundReason: refundReason
                    );

                    if (success)
                    {
                        // Open cash drawer when refund mode is Cash (same as cash payments)
                        if (refundMode == "CASH")
                        {
                            try { await TriggerCashDrawerAsync(); } catch { }
                        }
                        var result = new RefundResult
                        {
                            Amount = RefundAmount,
                            Reason = refundReason,
                            Mode = SelectedMode,
                            IsConfirmed = true
                        };
                        try { await ReceiptPrintingService.Instance.PrintRefundReceiptAsync(Order, result.Amount, result.Reason, result.Mode); } catch { }
                        OnRefundComplete?.Invoke(result);
                    }
                }
                else if (Order.PlatformId2 != 9)
                {
                    bool success = await apiService.ProcessRefundOrderForNonPosPlatformAsync(Order.RemoteOrderId, RefundAmount, refundMode, refundReason);

                    if (refundMode == "CASH" && success)
                    {
                        await RecordNonPosCashRefundMovementAsync(apiService, RefundAmount);
                    }
                    if (success)
                    {
                        // Open cash drawer when refund mode is Cash (same as cash payments)
                        if (refundMode == "CASH")
                        {
                            try { await TriggerCashDrawerAsync(); } catch { }
                        }
                        var result = new RefundResult
                        {
                            Amount = RefundAmount,
                            Reason = refundReason,
                            Mode = SelectedMode,
                            IsConfirmed = true
                        };
                        try { await ReceiptPrintingService.Instance.PrintRefundReceiptAsync(Order, result.Amount, result.Reason, result.Mode); } catch { }
                        OnRefundComplete?.Invoke(result);
                    }
                    else
                    {
                        MessageBox.Show("Failed to process refund.", "Refund Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                // Close the refund dialog first
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (DialogHost.IsDialogOpen("RootDialog"))
                        {
                            DialogHost.Close("RootDialog", null);
                        }
                        else if (DialogHost.IsDialogOpen("RootDialogHost"))
                        {
                            DialogHost.Close("RootDialogHost", null);
                        }
                    }
                    catch { }
                });

                // Wait a moment for the dialog to close
                await Task.Delay(100);

                // Show error dialog
                var errorVm = StatusDialogViewModel.CreateError("Refund Failed", $"Failed to process refund:\n\n{ex.Message}");
                var errorDlg = new View.StatusDialog { DataContext = errorVm };
                await DialogHost.Show(errorDlg, "RootDialog");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void Cancel()
        {
            OnRefundComplete?.Invoke(new RefundResult { IsConfirmed = false });
        }

        private async Task RecordNonPosCashRefundMovementAsync(ApiService apiService, decimal refundAmount)
        {
            const string movementDescription = "Total amount of NON-POS cash refunds for this session";
            decimal cashSaleTransactionAmount = Order?.Transactions?
                .Where(t => string.Equals(t?.TransactionType, "SALE", StringComparison.OrdinalIgnoreCase)
                         && string.Equals(t?.TransactionMode, "CASH", StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.TransactionAmount) ?? 0m;

            if (cashSaleTransactionAmount > 0m && refundAmount > cashSaleTransactionAmount)
            {
                decimal cardSaleCashRefundAmount = refundAmount - cashSaleTransactionAmount;
                await apiService.RecordCashMovementAsync("CARD_SALE_CASH_REFUND", cardSaleCashRefundAmount, movementDescription);
                await apiService.RecordCashMovementAsync("OTHER_REFUND", cashSaleTransactionAmount, movementDescription);
                return;
            }
            else
            {
                string paymentMode = (Order?.PaymentMode ?? "").Trim();
                if (string.Equals(paymentMode, "CARD", StringComparison.OrdinalIgnoreCase))
                {
                    await apiService.RecordCashMovementAsync("CARD_SALE_CASH_REFUND", refundAmount,"Total amount of NON-POS cash refunds for this session");
                }
                else
                {
                    await apiService.RecordCashMovementAsync("OTHER_REFUND", refundAmount,"Total amount of NON-POS cash refunds for this session");
                }
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

                // Close the refund dialog first
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

        private async Task CancelAndRefundAsync()
        {
            if (Order == null || Order.ApiId <= 0)
            {
                MessageBox.Show("Order information is missing. Cannot process cancellation and refund.", "Invalid Order", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (RefundAmount <= 0)
            {
                MessageBox.Show("Refund amount must be greater than zero.", "Invalid Amount", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IsNonPosOrder && RefundAmount != RefundBalance)
            {
                MessageBox.Show("For this order the refund amount must equal the refundable amount.", "Invalid Amount", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedMode))
            {
                MessageBox.Show("Please select a refund mode.", "Invalid Mode", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string refundReason = IsOtherReason && !string.IsNullOrWhiteSpace(OtherReason) 
                ? OtherReason 
                : SelectedReason;

            if (string.IsNullOrWhiteSpace(refundReason))
            {
                MessageBox.Show("Please provide a refund reason.", "Invalid Reason", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Map refund mode: "Cash" -> "CASH", "Card" or "Manual Card" -> "CARD"
            string refundMode = SelectedMode?.Trim().ToUpperInvariant() switch
            {
                "CASH" => "CASH",
                "CARD" or "MANUAL CARD" => "CARD",
                _ => "CASH" // Default to CASH if unknown
            };

            // Process card machine refund BEFORE cancelling/recording in API (POS + in-person orders only, not gateway/Stripe)
            if (ShouldUseCardMachine())
            {
                IsLoading = true;
                bool cardSuccess = await ProcessCardMachineRefundIfNeededAsync(SelectedMode, RefundAmount);
                if (!cardSuccess)
                {
                    IsLoading = false;
                    return;
                }
            }

            IsLoading = true;
            try
            {
                // Step 1: Cancel the order first
                var status = (Order.ApiStatus ?? string.Empty).ToUpper();
                bool isDeliveryPlatform = Order.PlatformId2 == 1 || Order.PlatformId2 == 2 || Order.PlatformId2 == 6;
                bool isTableOrderPlatform = Order.PlatformId == 8
                    || Order.PlatformId2 == 8
                    || string.Equals(Order.PlatformName, "TABLE_ORDER", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Order.Platform, "Table order", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Order.DeliveryPlatfornName, "Table order", StringComparison.OrdinalIgnoreCase);

                string remoteOrderId = !string.IsNullOrWhiteSpace(Order.RemoteOrderId)
                    ? Order.RemoteOrderId
                    : (!string.IsNullOrWhiteSpace(Order.DisplayOrderId) ? Order.DisplayOrderId : (Order.OrderNumber ?? Order.ApiId.ToString()));

                // Update table status if it's a table order
               /* if (isTableOrderPlatform)
                {
                    var tableId = Order.TableNumber;
                    if (tableId.HasValue && tableId.Value > 0)
                    {
                        try
                        {
                            await _apiService.UpdateTableStatusAsync(tableId.Value, "AVAILABLE", 0);
                        }
                        catch (Exception tableEx)
                        {
                            MessageBox.Show($"Failed to update table status: {tableEx.Message}", "Cancel Order", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }*/

                // Cancel the order
                if (Order.PlatformId2 == 9)
                {
                    
                    // Step 1: Process the refund
                    bool refundSuccess = await _apiService.ProcessOrderRefundAsync(
                    orderId: Order.ApiId,
                    refundMode: refundMode,
                    refundAmount: RefundAmount,
                    refundReason: refundReason
                    );
                    // Step 2: Update the order status
                    await _apiService.UpdateOrderStatusAsync(Order.ApiId, "CANCELLED", refundReason);
                    if (refundSuccess)
                    {
                        // Open cash drawer when refund mode is Cash (same as cash payments)
                        if (refundMode == "CASH")
                        {
                            try { await TriggerCashDrawerAsync(); } catch { }
                        }
                        // Invoke callback with success result
                        var result = new RefundResult
                        {
                            Amount = RefundAmount,
                            Reason = refundReason,
                            Mode = SelectedMode,
                            IsConfirmed = true
                        };
                        try { await ReceiptPrintingService.Instance.PrintRefundReceiptAsync(Order, result.Amount, result.Reason, result.Mode); } catch { }
                        OnRefundComplete?.Invoke(result);
                    }
                    else
                    {
                        MessageBox.Show("Failed to process refund. Order has been cancelled.", "Refund Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    bool notifiedRemote = false;
                    bool shouldNotifyRemote = (isDeliveryPlatform || isTableOrderPlatform) && !string.IsNullOrWhiteSpace(remoteOrderId);

                    if (shouldNotifyRemote)
                    {
                        var notifyResult = await _apiService.NotifyCancelOrderToDeliveryPlatformAsync(remoteOrderId, refundReason, RefundAmount, refundMode, refundReason);
                        if (refundMode == "CASH" && notifyResult.IsSuccess)
                        {
                            await RecordNonPosCashRefundMovementAsync(_apiService, RefundAmount);
                        }
                        if (!notifyResult.IsSuccess)
                        {
                            MessageBox.Show($"Failed to notify delivery platform: {notifyResult.ErrorMessage}", "Cancel Order", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        else
                        {
                            // Open cash drawer when refund mode is Cash (same as cash payments)
                            if (refundMode == "CASH")
                            {
                                try { await TriggerCashDrawerAsync(); } catch { }
                            }
                            // Invoke callback with success result
                            var result = new RefundResult
                            {
                                Amount = RefundAmount,
                                Reason = refundReason,
                                Mode = SelectedMode,
                                IsConfirmed = true
                            };
                            try { await ReceiptPrintingService.Instance.PrintRefundReceiptAsync(Order, result.Amount, result.Reason, result.Mode); } catch { }
                            OnRefundComplete?.Invoke(result);
                        }
                        notifiedRemote = true;
                    }

                    /*if (!isDeliveryPlatform || isTableOrderPlatform || !notifiedRemote)
                    {
                        await _apiService.UpdateOrderStatusAsync(Order.ApiId, "CANCELLED", refundReason);
                    }*/
                }

                // Update order status locally
                Order.ApiStatus = "CANCELLED";
                GlobalDataService.Instance.NotifyOrderStatusChanged(Order.ApiId, "CANCELLED");
                
            }
            catch (Exception ex)
            {
                // Close the refund dialog first
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (DialogHost.IsDialogOpen("RootDialog"))
                        {
                            DialogHost.Close("RootDialog", null);
                        }
                        else if (DialogHost.IsDialogOpen("RootDialogHost"))
                        {
                            DialogHost.Close("RootDialogHost", null);
                        }
                    }
                    catch { }
                });

                // Wait a moment for the dialog to close
                await Task.Delay(100);

                // Show error dialog
                var errorVm = StatusDialogViewModel.CreateError("Cancel & Refund Failed", $"Failed to process cancellation and refund:\n\n{ex.Message}");
                var errorDlg = new View.StatusDialog { DataContext = errorVm };
                await DialogHost.Show(errorDlg, "RootDialog");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public string FinalReason
        {
            get => IsOtherReason && !string.IsNullOrWhiteSpace(OtherReason) ? OtherReason : SelectedReason;
        }

        public string GetFinalReason()
        {
            return FinalReason;
        }

        private async Task ValidateCashDrawerBalanceAsync()
        {
            if (!string.Equals(SelectedMode?.Trim(), "Cash", StringComparison.OrdinalIgnoreCase))
            {
                IsValidatingCashDrawer = false;
                return;
            }

            IsValidatingCashDrawer = true;

            try
            {
                var activeSession = await _apiService.GetActiveCashDrawerSessionAsync();
                
                if (activeSession != null)
                {
                    // Check if closing balance expected is less than refund amount
                    if (activeSession.ClosingBalanceExpected < RefundAmount)
                    {
                        CashDrawerBalanceError = "Insufficient cash drawer balance. Please select another refund mode or adjust the refund amount.";
                    }
                    else
                    {
                        CashDrawerBalanceError = null;
                    }
                }
                else
                {
                    // If no active session, allow the refund (might be a new session scenario)
                    CashDrawerBalanceError = null;
                }
            }
            catch (Exception ex)
            {
                // On error, don't block the refund but log the error
                System.Diagnostics.Debug.WriteLine($"Error validating cash drawer balance: {ex.Message}");
                CashDrawerBalanceError = null;
            }
            finally
            {
                // Re-enable Next once we know the result (on UI thread)
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    IsValidatingCashDrawer = false;
                }));
            }
        }

        /// <summary>
        /// Opens the cash drawer via ESC/POS pulse on active (or all) printers, same as cash payments.
        /// </summary>
        private async Task TriggerCashDrawerAsync()
        {
            var printersService = PrintersService.Instance;
            var targetPrinters = printersService.Printers.Where(p => p.IsActive).Select(p => p.DeviceName).ToList();
            if (targetPrinters.Count == 0)
            {
                targetPrinters = printersService.Printers.Select(p => p.DeviceName).ToList();
            }

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

            if (anySuccess)
            {
                try
                {
                    var currentUser = new LocalStorageService().GetCurrentUser();
                    var currentUserId = currentUser?.Id;
                    await _apiService.LogUserActivityAsync("open", "cash_drawer", 1, currentUserId, "Opened cash drawer (cash refund)");
                }
                catch { }
            }
        }
    }
}
