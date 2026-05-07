using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using POS_UI.Models;
using POS_UI.Services;

namespace POS_UI.ViewModels
{
    /// <summary>One row in the split payment grid: method + amount + cash/card details.</summary>
    public class SplitPaymentRowViewModel : INotifyPropertyChanged
    {
        private PaymentMethod _paymentMethod = PaymentMethod.Cash;
        private string _amountString = "0.00";
        private bool _isCharged;
        private string _cashGivenString = "";
        private string _cashBalanceString = "";
        private string _authCode = "";
        private string _cardPan = "";
        private string _cardScheme = "";
        private string _transactionId = "";

        public PaymentMethod PaymentMethod
        {
            get => _paymentMethod;
            set { _paymentMethod = value; OnPropertyChanged(); }
        }

        public string AmountString
        {
            get => _amountString;
            set { _amountString = value ?? "0"; OnPropertyChanged(); OnPropertyChanged(nameof(Amount)); }
        }

        public decimal Amount
        {
            get => decimal.TryParse(_amountString, out var v) ? Math.Max(0, v) : 0m;
            set { AmountString = value.ToString("F2"); }
        }

        /// <summary>True when this split has been charged (payment details stored).</summary>
        public bool IsCharged
        {
            get => _isCharged;
            set { _isCharged = value; OnPropertyChanged(); }
        }

        /// <summary>When charged, the exact payment item stored at charge time (used when confirming so order is placed with charged payment models).</summary>
        public SplitPaymentItem ChargedPaymentItem { get; set; }

        /// <summary>Cash tendered (for cash splits).</summary>
        public string CashGivenString 
        {   
            get => _cashGivenString; 

            set { _cashGivenString = value ?? ""; 
                    OnPropertyChanged(); } 
        }
        public decimal CashGiven => decimal.TryParse(_cashGivenString, out var v) ? Math.Max(0, v) : 0m;
        /// <summary>Change/balance (for cash splits).</summary>
        public string CashBalanceString { get => _cashBalanceString; set { _cashBalanceString = value ?? ""; OnPropertyChanged(); } }
        public decimal CashBalance => decimal.TryParse(_cashBalanceString, out var v) ? v : 0m;

        /// <summary>Card auth code (for card splits).</summary>
        public string AuthCode { get => _authCode; set { _authCode = value ?? ""; OnPropertyChanged(); } }
        /// <summary>Card PAN / last digits (for card splits).</summary>
        public string CardPan { get => _cardPan; set { _cardPan = value ?? ""; OnPropertyChanged(); } }
        /// <summary>Card scheme (for card splits).</summary>
        public string CardScheme { get => _cardScheme; set { _cardScheme = value ?? ""; OnPropertyChanged(); } }
        /// <summary>Transaction/reference id (for card splits).</summary>
        public string TransactionId { get => _transactionId; set { _transactionId = value ?? ""; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class SplitPaymentDialogViewModel : BaseViewModel
    {
        private readonly decimal _totalAmount;
        /// <summary>Amount left to split among rows (order total minus existing temp payments when API returned data).</summary>
        private decimal _effectiveSplitTotal;
        private readonly string _dialogHostId;
        private readonly string _orderDisplayOrderId;
        private readonly string _tempPaymentTypeId;
        private readonly string _tempPaymentType;
        private readonly ApiService _apiService;
        private readonly Func<decimal, string, Task<CardTransactionResult>> _runCardPaymentAsync;
        private readonly Action<string, string> _onCardPaymentError;
        /// <summary>Invoked after a cash split line is successfully charged (same idea as opening the drawer on cash checkout).</summary>
        private readonly Action _openCashDrawerOnSplitCashCharge;
        private string _currency;
        private string _totalFormatted;
        private string _balanceMessage;
        private bool _isTotalValid;
        private bool _isRedistributing;
        private bool _hasExistingTempPayments;
        /// <summary>True when GET temp-payments sums to the full order total — Confirm is allowed without charging the (zero) remainder rows.</summary>
        private bool _tempPaymentsCoverFullOrder;
        /// <summary>Temp payments returned when the dialog opened (already on server). Prepended to Confirm/Cancel payment lists so the order request includes all charged amounts.</summary>
        private List<TempPaymentRecord> _tempPaymentsAtDialogOpen;

        public SplitPaymentDialogViewModel(
            decimal totalAmount,
            string dialogHostId = "SplitPaymentDialogHost",
            Func<decimal, string, Task<CardTransactionResult>> runCardPaymentAsync = null,
            Action<string, string> onCardPaymentError = null,
            Action openCashDrawerOnSplitCashCharge = null,
            string orderDisplayOrderId = null,
            string tempPaymentTypeId = null,
            string tempPaymentType = "ORDER")
        {
            _totalAmount = Math.Round(totalAmount, 2, MidpointRounding.AwayFromZero);
            _effectiveSplitTotal = _totalAmount;
            _dialogHostId = dialogHostId;
            _orderDisplayOrderId = (orderDisplayOrderId ?? "").Trim();
            _tempPaymentTypeId = !string.IsNullOrWhiteSpace(tempPaymentTypeId)
                ? tempPaymentTypeId.Trim()
                : _orderDisplayOrderId;
            _tempPaymentType = string.IsNullOrWhiteSpace(tempPaymentType)
                ? "ORDER"
                : tempPaymentType.Trim().ToUpperInvariant();
            _apiService = new ApiService();
            _runCardPaymentAsync = runCardPaymentAsync;
            _onCardPaymentError = onCardPaymentError;
            _openCashDrawerOnSplitCashCharge = openCashDrawerOnSplitCashCharge;
            _currency = GlobalDataService.Instance?.ShopDetails?.Currency ?? "£";
            _totalFormatted = $"{_currency} {_totalAmount:F2}";

            Splits = new ObservableCollection<SplitPaymentRowViewModel>();
            AddSplitCommand = new RelayCommand(AddSplit);
            IncreaseSplitsCommand = new RelayCommand(AddSplit, () => CanIncreaseSplits());
            DecreaseSplitsCommand = new RelayCommand(DecreaseSplits, () => CanDecreaseSplits());
            RemoveSplitCommand = new RelayCommand<SplitPaymentRowViewModel>(RemoveSplit, row => row != null && !row.IsCharged && Splits.Count > 1);
            ChargeSplitCommand = new AsyncRelayCommand<SplitPaymentRowViewModel>(ChargeSplitAsync, CanChargeSplitRow);
            ConfirmCommand = new RelayCommand(Confirm, () => IsTotalValid && IsTotalCharged);
            CancelCommand = new RelayCommand(Cancel);
            ChargedPayments = new ObservableCollection<SplitPaymentItem>();
            ExistingTempPayments = new ObservableCollection<ExistingTempPaymentLine>();
            _tempPaymentsAtDialogOpen = new List<TempPaymentRecord>();

            // Start with 2 equal cash splits (exact cents: floor first n−1, remainder on last)
            var initial = DistributeTotalExactCents(_effectiveSplitTotal, 2);
            Splits.Add(new SplitPaymentRowViewModel { PaymentMethod = PaymentMethod.Cash, Amount = initial[0] });
            Splits.Add(new SplitPaymentRowViewModel { PaymentMethod = PaymentMethod.Cash, Amount = initial[1] });
            UpdateBalance();
            foreach (var row in Splits)
                row.PropertyChanged += OnSplitRowPropertyChanged;
        }

        /// <summary>Loads temp payments from the API using temp payment reference id (session id when available, otherwise display id).</summary>
        public async Task LoadExistingTempPaymentsAsync()
        {
            if (string.IsNullOrWhiteSpace(_tempPaymentTypeId))
                return;

            List<TempPaymentRecord> data = null;
            try
            {
                _apiService.RefreshHeadersFromSettings();
                var (_, _, list) = await _apiService.GetTempPaymentsByDisplayOrderIdAsync(_tempPaymentTypeId).ConfigureAwait(true);
                data = list;
            }
            catch
            {
                data = null;
            }

            ExistingTempPayments.Clear();
            _tempPaymentsAtDialogOpen = new List<TempPaymentRecord>();
            _tempPaymentsCoverFullOrder = false;
            if (data == null || data.Count == 0)
            {
                HasExistingTempPayments = false;
                RaiseChargeSplitCanExecuteChanged();
                OnPropertyChanged(nameof(ShowSplitPaymentEditor));
                return;
            }

            _tempPaymentsAtDialogOpen = data.ToList();

            foreach (var r in data)
            {
                var mode = (r.PaymentMode ?? "").Trim();
                ExistingTempPayments.Add(new ExistingTempPaymentLine
                {
                    PaymentMode = string.IsNullOrEmpty(mode) ? "—" : mode,
                    AmountFormatted = $"{_currency} {r.PaymentAmount:F2}"
                });
            }
            HasExistingTempPayments = true;

            var tempPaidSum = Math.Round(data.Sum(r => r.PaymentAmount), 2, MidpointRounding.AwayFromZero);
            _effectiveSplitTotal = Math.Max(0m, Math.Round(_totalAmount - tempPaidSum, 2, MidpointRounding.AwayFromZero));
            _tempPaymentsCoverFullOrder = tempPaidSum == _totalAmount;

            foreach (var row in Splits.ToList())
            {
                row.PropertyChanged -= OnSplitRowPropertyChanged;
            }
            Splits.Clear();
            var initial = DistributeTotalExactCents(_effectiveSplitTotal, 2);
            var first = new SplitPaymentRowViewModel { PaymentMethod = PaymentMethod.Cash, Amount = initial[0] };
            var second = new SplitPaymentRowViewModel { PaymentMethod = PaymentMethod.Cash, Amount = initial[1] };
            first.PropertyChanged += OnSplitRowPropertyChanged;
            second.PropertyChanged += OnSplitRowPropertyChanged;
            Splits.Add(first);
            Splits.Add(second);
            OnPropertyChanged(nameof(SplitCount));
            OnPropertyChanged(nameof(IsTotalCharged));
            ((RelayCommand)ConfirmCommand).RaiseCanExecuteChanged();
            RaiseSplitCountCommandsCanExecuteChanged();
            ((RelayCommand<SplitPaymentRowViewModel>)RemoveSplitCommand).RaiseCanExecuteChanged();
            UpdateBalance();
            RaiseChargeSplitCanExecuteChanged();
            OnPropertyChanged(nameof(ShowSplitPaymentEditor));
        }

        /// <summary>True when recorded charges (this session) match the amount to collect, or server temps already cover the full order.</summary>
        private bool IsChargedAmountEqualsOrderTotal()
        {
            if (_tempPaymentsCoverFullOrder) return true;
            var charged = Math.Round(Splits.Where(s => s.IsCharged).Sum(s => s.Amount), 2, MidpointRounding.AwayFromZero);
            var target = Math.Round(_effectiveSplitTotal, 2, MidpointRounding.AwayFromZero);
            return charged == target;
        }

        /// <summary>False only when temp payments loaded at dialog open already cover the full order — hide split count and split rows (opening state from API, not in-session charges).</summary>
        public bool ShowSplitPaymentEditor => !_tempPaymentsCoverFullOrder;

        private static decimal RoundMoney(decimal v) =>
            Math.Round(v, 2, MidpointRounding.AwayFromZero);

        private decimal SumChargedAmounts() =>
            RoundMoney(Splits.Where(s => s.IsCharged).Sum(s => s.Amount));

        /// <summary>Upper bound while typing an uncharged row: all uncharged lines share (effective total − sums already charged in this session).</summary>
        private decimal MaxEditableAmountForRow(SplitPaymentRowViewModel row)
        {
            if (row == null || row.IsCharged) return row?.Amount ?? 0m;
            return Math.Max(0m, RoundMoney(_effectiveSplitTotal - SumChargedAmounts()));
        }

        /// <summary>Remaining payable on this line given every other row’s current amount (used before Charge and to clamp over-entry when another row already holds the rest).</summary>
        private decimal RemainingPayableForRow(SplitPaymentRowViewModel row)
        {
            if (row == null) return 0m;
            var sumOthers = RoundMoney(Splits.Where(s => s != row).Sum(s => s.Amount));
            return Math.Max(0m, RoundMoney(_effectiveSplitTotal - sumOthers));
        }

        private void RaiseSplitCountCommandsCanExecuteChanged()
        {
            if (IncreaseSplitsCommand is RelayCommand inc) inc.RaiseCanExecuteChanged();
            if (DecreaseSplitsCommand is RelayCommand dec) dec.RaiseCanExecuteChanged();
        }

        private bool CanChargeSplitRow(SplitPaymentRowViewModel row)
        {
            if (row == null || row.IsCharged || _tempPaymentsCoverFullOrder) return false;
            if (row.Amount <= 0m) return false;
            if (row.Amount > MaxEditableAmountForRow(row) + 0.01m) return false;
            return row.Amount <= RemainingPayableForRow(row) + 0.01m;
        }

        private void RaiseChargeSplitCanExecuteChanged()
        {
            if (ChargeSplitCommand is AsyncRelayCommand<SplitPaymentRowViewModel> cmd)
                cmd.RaiseCanExecuteChanged();
        }

        private void OnSplitRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e?.PropertyName == nameof(SplitPaymentRowViewModel.AmountString) && !_isRedistributing && sender is SplitPaymentRowViewModel editedRow)
            {
                var maxEditable = MaxEditableAmountForRow(editedRow);
                if (!editedRow.IsCharged && editedRow.Amount > maxEditable)
                {
                    _isRedistributing = true;
                    try { editedRow.Amount = maxEditable; }
                    finally { _isRedistributing = false; }
                }
                else if (Splits.Count == 1 && !editedRow.IsCharged)
                {
                    var need = RoundMoney(_effectiveSplitTotal - SumChargedAmounts());
                    if (editedRow.Amount < need)
                    {
                        _isRedistributing = true;
                        try { editedRow.Amount = need; }
                        finally { _isRedistributing = false; }
                    }
                }
                RedistributeAfterAmountChange(editedRow);
                RaiseChargeSplitCanExecuteChanged();
            }
            else if (e?.PropertyName == nameof(SplitPaymentRowViewModel.AmountString))
                RaiseChargeSplitCanExecuteChanged();

            UpdateBalance();
            if (e?.PropertyName == nameof(SplitPaymentRowViewModel.IsCharged))
            {
                OnPropertyChanged(nameof(IsTotalCharged));
                ((RelayCommand)ConfirmCommand).RaiseCanExecuteChanged();
                ((RelayCommand<SplitPaymentRowViewModel>)RemoveSplitCommand).RaiseCanExecuteChanged();
                RaiseChargeSplitCanExecuteChanged();
            }
            RaiseSplitCountCommandsCanExecuteChanged();
        }

        /// <summary>When one row's amount changes, distribute (total - that amount - charged others) among the other uncharged rows so total matches.</summary>
        private void RedistributeAfterAmountChange(SplitPaymentRowViewModel editedRow)
        {
            var maxEditable = MaxEditableAmountForRow(editedRow);
            if (!editedRow.IsCharged && editedRow.Amount > maxEditable)
            {
                _isRedistributing = true;
                try { editedRow.Amount = maxEditable; }
                finally { _isRedistributing = false; }
            }

            var chargedSumExcludingEdited = Splits.Where(s => s.IsCharged && s != editedRow).Sum(s => s.Amount);
            var remaining = Math.Round(_effectiveSplitTotal - editedRow.Amount - chargedSumExcludingEdited, 2, MidpointRounding.AwayFromZero);
            var otherUncharged = Splits.Where(s => !s.IsCharged && s != editedRow).ToList();
            if (otherUncharged.Count == 0 || remaining < 0) return;
            _isRedistributing = true;
            try
            {
                var distribution = DistributeTotalExactCents(remaining, otherUncharged.Count);
                for (int i = 0; i < otherUncharged.Count; i++)
                    otherUncharged[i].Amount = distribution[i];
            }
            finally
            {
                _isRedistributing = false;
            }
            RemoveZeroAmountUnchargedRows(editedRow);
        }

        /// <summary>Remove any uncharged split rows that have amount 0, keeping at least one split. Excludes the edited row only when its amount is empty (user cleared field); if user entered 0, that row is removed.</summary>
        private void RemoveZeroAmountUnchargedRows(SplitPaymentRowViewModel excludeRow = null)
        {
            var toRemove = Splits.Where(s => !s.IsCharged && s.Amount == 0 && (excludeRow == null || s != excludeRow || !string.IsNullOrWhiteSpace(excludeRow.AmountString))).ToList();
            var maxToRemove = Splits.Count - 1; // keep at least 1 split
            var removed = 0;
            foreach (var row in toRemove.Take(maxToRemove))
            {
                row.PropertyChanged -= OnSplitRowPropertyChanged;
                Splits.Remove(row);
                removed++;
            }
            if (removed > 0)
            {
                OnPropertyChanged(nameof(SplitCount));
                OnPropertyChanged(nameof(IsTotalCharged));
                UpdateBalance();
                ((RelayCommand)ConfirmCommand).RaiseCanExecuteChanged();
                RaiseSplitCountCommandsCanExecuteChanged();
                ((RelayCommand<SplitPaymentRowViewModel>)RemoveSplitCommand).RaiseCanExecuteChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool CanDecreaseSplits()
        {
            if (Splits.Count <= 1) return false;
            var chargedCount = Splits.Count(s => s.IsCharged);
            // Keep at least one row per charged split; decrease disabled when count == chargedCount.
            return Splits.Count > chargedCount;
        }

        /// <summary>True if we can add one more split without forcing a negative amount on any split (exact cent distribution).</summary>
        private bool CanIncreaseSplits()
        {
            if (Splits.Count >= 50) return false;
            if (IsChargedAmountEqualsOrderTotal()) return false;
            var chargedSum = Splits.Where(s => s.IsCharged).Sum(s => s.Amount);
            var remaining = Math.Round(_effectiveSplitTotal - chargedSum, 2, MidpointRounding.AwayFromZero);
            var chargedCount = Splits.Count(s => s.IsCharged);
            var nextUnchargedCount = Splits.Count - chargedCount + 1; // after adding one more split
            if (nextUnchargedCount <= 0) return true;
            if (remaining < 0) return false;
            var distribution = DistributeTotalExactCents(remaining, nextUnchargedCount);
            return distribution.Count == nextUnchargedCount && distribution.All(v => v >= 0m);
        }

        public string TotalFormatted
        {
            get => _totalFormatted;
            set { _totalFormatted = value; OnPropertyChanged(); }
        }

        public decimal TotalAmount => _effectiveSplitTotal;

        public string Currency => _currency;

        public ObservableCollection<SplitPaymentRowViewModel> Splits { get; }

        /// <summary>Payment details stored when user clicks Charge on a split row.</summary>
        public ObservableCollection<SplitPaymentItem> ChargedPayments { get; }

        /// <summary>Server-side temp payments for this order (from GET temp-payments). Shown when non-empty.</summary>
        public ObservableCollection<ExistingTempPaymentLine> ExistingTempPayments { get; }

        public bool HasExistingTempPayments
        {
            get => _hasExistingTempPayments;
            private set
            {
                if (_hasExistingTempPayments == value) return;
                _hasExistingTempPayments = value;
                OnPropertyChanged();
            }
        }

        public string BalanceMessage
        {
            get => _balanceMessage;
            set { _balanceMessage = value; OnPropertyChanged(); }
        }

        public bool IsTotalValid
        {
            get => _isTotalValid;
            set { _isTotalValid = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsTotalCharged)); ((RelayCommand)ConfirmCommand).RaiseCanExecuteChanged(); }
        }

        /// <summary>True when split amounts match total and every split has been charged, or existing temp payments already cover the full order total.</summary>
        public bool IsTotalCharged => IsTotalValid && (_tempPaymentsCoverFullOrder || (Splits.Count > 0 && Splits.All(s => s.IsCharged)));

        /// <summary>Payment methods available for each split (Cash, Manual Card, Card only).</summary>
        public PaymentMethod[] PaymentMethodOptions => new[] { PaymentMethod.Cash, PaymentMethod.ManualCard, PaymentMethod.Card };

        public int SplitCount => Splits.Count;

        public ICommand AddSplitCommand { get; }
        public ICommand IncreaseSplitsCommand { get; }
        public ICommand DecreaseSplitsCommand { get; }
        public ICommand RemoveSplitCommand { get; }
        public ICommand ChargeSplitCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public void AddSplit()
        {
            if (!CanIncreaseSplits()) return;
            var row = new SplitPaymentRowViewModel { PaymentMethod = PaymentMethod.Cash, Amount = 0m };
            row.PropertyChanged += OnSplitRowPropertyChanged;
            Splits.Add(row);
            RedistributeUnchargedAmounts();
            UpdateBalance();
            OnPropertyChanged(nameof(SplitCount));
            OnPropertyChanged(nameof(IsTotalCharged));
            ((RelayCommand)ConfirmCommand).RaiseCanExecuteChanged();
            RaiseSplitCountCommandsCanExecuteChanged();
            CommandManager.InvalidateRequerySuggested();
        }

        public void DecreaseSplits()
        {
            if (!CanDecreaseSplits()) return;
            // Remove an uncharged row only (never drop a split that was already charged).
            for (int i = Splits.Count - 1; i >= 0; i--)
            {
                if (Splits[i].IsCharged) continue;
                var row = Splits[i];
                row.PropertyChanged -= OnSplitRowPropertyChanged;
                Splits.RemoveAt(i);
                break;
            }
            RedistributeUnchargedAmounts();
            UpdateBalance();
            OnPropertyChanged(nameof(SplitCount));
            OnPropertyChanged(nameof(IsTotalCharged));
            ((RelayCommand)ConfirmCommand).RaiseCanExecuteChanged();
            RaiseSplitCountCommandsCanExecuteChanged();
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>Store this row's payment details when user clicks Charge — runs card terminal for Card method (like main flow) and stores PaymentModel-style values.</summary>
        public async Task ChargeSplitAsync(SplitPaymentRowViewModel row)
        {
            if (row == null || row.IsCharged || _tempPaymentsCoverFullOrder) return;

            var maxEditable = MaxEditableAmountForRow(row);
            if (row.Amount > maxEditable)
            {
                _isRedistributing = true;
                try { row.Amount = maxEditable; }
                finally { _isRedistributing = false; }
            }

            var payable = RemainingPayableForRow(row);
            if (row.Amount > payable)
            {
                _isRedistributing = true;
                try { row.Amount = payable; }
                finally { _isRedistributing = false; }
                UpdateBalance();
            }

            if (row.Amount <= 0m)
            {
                _onCardPaymentError?.Invoke("Invalid amount", "Enter an amount greater than zero.");
                return;
            }

            var sumAll = RoundMoney(Splits.Sum(s => s.Amount));
            if (sumAll > _effectiveSplitTotal + 0.01m)
            {
                RedistributeUnchargedAmounts();
                payable = RemainingPayableForRow(row);
                if (row.Amount > payable)
                {
                    _isRedistributing = true;
                    try { row.Amount = payable; }
                    finally { _isRedistributing = false; }
                }
                UpdateBalance();
                sumAll = RoundMoney(Splits.Sum(s => s.Amount));
                if (sumAll > _effectiveSplitTotal + 0.01m)
                {
                    _onCardPaymentError?.Invoke("Amount too high", $"Split amounts cannot exceed the order total. Remaining for this line is at most {Currency} {payable:F2}.");
                    return;
                }
            }

            var pmUpper = row.PaymentMethod.ToString().ToUpper();
            var isTerminalCard = pmUpper == "CARD";

            if (isTerminalCard && _runCardPaymentAsync != null)
            {
                var reference = $"SPLIT-{Guid.NewGuid():N}";
                var result = await _runCardPaymentAsync(row.Amount, reference).ConfigureAwait(true);
                if (result == null || !result.IsSuccess)
                {
                    if (result?.UserAlreadyNotifiedOfFailure != true)
                        _onCardPaymentError?.Invoke("Card payment failed", result?.ErrorMessage ?? "Card payment was cancelled or failed.");
                    return;
                }
                row.AuthCode = result.AuthorisationCode ?? "";
                row.CardPan = result.CardPan ?? "";
                row.CardScheme = result.CardScheme ?? "";
                row.TransactionId = result.RetrievalReferenceNumber ?? "";
            }
            else if (pmUpper == "MANUALCARD" && string.IsNullOrWhiteSpace(row.TransactionId))
            {
                row.TransactionId = "manualcard";
            }

            if (!string.IsNullOrWhiteSpace(_tempPaymentTypeId))
            {
                try
                {
                    _apiService.RefreshHeadersFromSettings();
                    var paymentMode = pmUpper == "CARD" || pmUpper == "MANUALCARD" ? "CARD" : "CASH";
                    await _apiService.CreateTempPaymentAsync(
                        typeId: _tempPaymentTypeId,
                        paymentType: _tempPaymentType,
                        paymentMode: paymentMode,
                        paymentAmount: row.Amount,
                        transactionId: row.TransactionId ?? "").ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    _onCardPaymentError?.Invoke("Temp payment failed", ex.Message);
                    return;
                }
            }

            if (pmUpper == "CASH")
            {
                try { _openCashDrawerOnSplitCashCharge?.Invoke(); } catch { /* drawer optional */ }
            }

            var item = SplitPaymentItemFromRow(row);
            ChargedPayments.Add(item);
            row.ChargedPaymentItem = item; // store so Confirm uses this exact charged payment model
            row.IsCharged = true;
            RaiseSplitCountCommandsCanExecuteChanged();
        }

        /// <summary>Maps a server temp-payment row to a split item (card details not stored on server beyond transaction id).</summary>
        private static SplitPaymentItem SplitPaymentItemFromTempRecord(TempPaymentRecord r)
        {
            if (r == null || r.PaymentAmount <= 0m) return null;
            var mode = (r.PaymentMode ?? "").Trim().ToUpperInvariant();
            var isManual = mode == "MANUALCARD" || mode == "MANUAL_CARD";
            var isCard = isManual || mode == "CARD";
            var pm = isCard ? (isManual ? PaymentMethod.ManualCard : PaymentMethod.Card) : PaymentMethod.Cash;
            var amt = Math.Round(r.PaymentAmount, 2, MidpointRounding.AwayFromZero);
            var tid = r.TransactionId ?? "";
            return new SplitPaymentItem
            {
                PaymentMethod = pm,
                Amount = amt,
                Cash = isCard ? 0m : amt,
                Balance = 0m,
                AuthCode = "",
                CardPan = "",
                CardScheme = "",
                TransactionId = isCard ? tid : ""
            };
        }

        /// <summary>Build SplitPaymentItem with cash or card properties per PaymentModel.</summary>
        private static SplitPaymentItem SplitPaymentItemFromRow(SplitPaymentRowViewModel row)
        {
            var pmUpper = row.PaymentMethod.ToString().ToUpper();
            var isCard = pmUpper == "CARD" || pmUpper == "MANUALCARD";
            return new SplitPaymentItem
            {
                PaymentMethod = row.PaymentMethod,
                Amount = row.Amount,
                Cash = isCard ? 0m : (row.CashGiven > 0 ? row.CashGiven : row.Amount),
                Balance = isCard ? 0m : Math.Max(0, row.CashBalance),
                AuthCode = isCard ? (row.AuthCode ?? "") : "",
                CardPan = isCard ? (row.CardPan ?? "") : "",
                CardScheme = isCard ? (row.CardScheme ?? "") : "",
                TransactionId = isCard ? (row.TransactionId ?? (pmUpper == "MANUALCARD" ? "manualcard" : "")) : ""
            };
        }

        public void RemoveSplit(SplitPaymentRowViewModel row)
        {
            if (row == null || Splits.Count <= 1) return;
            row.PropertyChanged -= OnSplitRowPropertyChanged;
            Splits.Remove(row);
            RedistributeUnchargedAmounts();
           UpdateBalance();
            OnPropertyChanged(nameof(SplitCount));
            OnPropertyChanged(nameof(IsTotalCharged));
            ((RelayCommand)ConfirmCommand).RaiseCanExecuteChanged();
            RaiseSplitCountCommandsCanExecuteChanged();
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>Distribute (total - sum of charged splits) among uncharged splits only; leaves charged split amounts unchanged.</summary>
        private void RedistributeUnchargedAmounts()
        {
            var chargedSum = Splits.Where(s => s.IsCharged).Sum(s => s.Amount);
            var remaining = Math.Round(_effectiveSplitTotal - chargedSum, 2, MidpointRounding.AwayFromZero);
            var uncharged = Splits.Where(s => !s.IsCharged).ToList();
            if (uncharged.Count == 0) return;
            var distribution = DistributeTotalExactCents(remaining, uncharged.Count);
            for (int i = 0; i < uncharged.Count; i++)
                uncharged[i].Amount = distribution[i];
        }

        /// <summary>
        /// Split <paramref name="total"/> across <paramref name="count"/> rows using whole cents:
        /// first n−1 rows get floor(total/n); last row gets remainder so the sum equals total exactly.
        /// </summary>
        private static List<decimal> DistributeTotalExactCents(decimal total, int count)
        {
            var list = new List<decimal>(count);
            if (count <= 0) return list;

            var safeTotal = Math.Round(total, 2, MidpointRounding.AwayFromZero);
            if (safeTotal <= 0m)
            {
                for (int i = 0; i < count; i++)
                    list.Add(0m);
                return list;
            }

            var totalCents = (long)Math.Round(safeTotal * 100m, MidpointRounding.AwayFromZero);
            var n = count;
            var perFloorCents = totalCents / n;
            var lastCents = totalCents - perFloorCents * (n - 1);

            for (int i = 0; i < n - 1; i++)
                list.Add(perFloorCents / 100m);
            list.Add(lastCents / 100m);
            return list;
        }

        private void UpdateBalance()
        {
            var sum = Splits.Sum(s => s.Amount);
            var diff = _effectiveSplitTotal - sum;
            if (Math.Abs(diff) < 0.01m)
            {
                BalanceMessage = "";
                IsTotalValid = true;
            }
            else if (diff > 0)
            {
                BalanceMessage = $"{Currency} {diff:F2} remaining";
                IsTotalValid = false;
            }
            else
            {
                BalanceMessage = $"Over by {Currency} {Math.Abs(diff):F2}";
                IsTotalValid = false;
            }
        }

        private void Confirm()
        {
            if (!IsTotalValid || !IsTotalCharged) return;
            var prior = (_tempPaymentsAtDialogOpen ?? Enumerable.Empty<TempPaymentRecord>())
                .Select(SplitPaymentItemFromTempRecord)
                .Where(i => i != null)
                .ToList();
            if (_tempPaymentsCoverFullOrder)
            {
                DialogHost.Close(_dialogHostId, new SplitPaymentDialogResult { Confirmed = true, Payments = prior });
                return;
            }
            // Rows charged in this session (remainder after prior temps)
            var fromRows = Splits.Select(row => row.IsCharged && row.ChargedPaymentItem != null
                ? row.ChargedPaymentItem
                : SplitPaymentItemFromRow(row)).ToList();
            var list = prior.Concat(fromRows).ToList();
            DialogHost.Close(_dialogHostId, new SplitPaymentDialogResult { Confirmed = true, Payments = list });
        }

        private void Cancel()
        {
            var prior = (_tempPaymentsAtDialogOpen ?? Enumerable.Empty<TempPaymentRecord>())
                .Select(SplitPaymentItemFromTempRecord)
                .Where(i => i != null)
                .ToList();
            var charged = ChargedPayments != null && ChargedPayments.Count > 0
                ? ChargedPayments.ToList()
                : new List<SplitPaymentItem>();
            var list = prior.Concat(charged).ToList();
            DialogHost.Close(_dialogHostId, new SplitPaymentDialogResult { Confirmed = false, Payments = list });
        }
    }

    /// <summary>One line in the split dialog summary for existing API temp payments.</summary>
    public sealed class ExistingTempPaymentLine
    {
        public string PaymentMode { get; set; }
        public string AmountFormatted { get; set; }
    }
}
