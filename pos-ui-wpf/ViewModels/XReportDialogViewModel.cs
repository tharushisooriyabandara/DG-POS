using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using POS_UI.Models;
using POS_UI.Services;
using System.Globalization;

namespace POS_UI.ViewModels
{
    /// <summary>
    /// ViewModel for X Report dialog. Loads the active cash drawer session and Z-Report stats.
    /// </summary>
    public class XReportDialogViewModel : INotifyPropertyChanged
    {
        private CashDrawerSessionModel _session;
        private ZReportStatsModel _zReportStats;
        private bool _isLoadingSession = true;
        private bool _isLoadingZReportStats;
        private bool _isPrinting;
        private bool _noActiveSession;
        private readonly ApiService _apiService;
        private readonly LocalStorageService _localStorageService;

        public string OutletName { get; set; }

        public CashDrawerSessionModel Session
        {
            get => _session;
            set
            {
                _session = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SessionStatusDisplay));
                OnPropertyChanged(nameof(CashDrawerCashSaleCashRefundAmount));
                if (_session != null)
                {
                    _ = LoadZReportStatsAsync();
                }
            }
        }

        /// <summary>For X Report the shift is always open; show "Open" or session status.</summary>
        public string SessionStatusDisplay => Session?.Status ?? "Open";

        public ZReportStatsModel ZReportStats
        {
            get => _zReportStats;
            set
            {
                _zReportStats = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasZReportStats));
                OnPropertyChanged(nameof(HasTaxSummary));
                OnPropertyChanged(nameof(ShowNoZReportData));
                OnPropertyChanged(nameof(FilteredPlatformStats));
                OnPropertyChanged(nameof(FilteredPlatformCashStats));
                OnPropertyChanged(nameof(FilteredPlatformCardStats));
                OnPropertyChanged(nameof(HasCardSales));
                OnPropertyChanged(nameof(FilteredPlatformDiscountStats));
                OnPropertyChanged(nameof(FilteredPlatformVoucherStats));
                OnPropertyChanged(nameof(FilteredPlatformCashRefundStats));
                OnPropertyChanged(nameof(FilteredPlatformCardRefundStats));
                OnPropertyChanged(nameof(TotalRefundsDisplayed));
                OnPropertyChanged(nameof(HasPlatformCashRefunds));
                OnPropertyChanged(nameof(HasPlatformCardRefunds));
                OnPropertyChanged(nameof(PosVoucherDiscount));
                OnPropertyChanged(nameof(CalculatedTotalTaxAmount));
                OnPropertyChanged(nameof(CalculatedTotalOrderAmount));
            }
        }

        //Total taxes calculated from tax breakdown 
        public decimal CalculatedTotalTaxAmount
        {
            get
            {
                if (ZReportStats?.TaxSummary?.TaxBreakdown == null || ZReportStats.TaxSummary.TaxBreakdown.Count == 0)
                    return ZReportStats?.TaxSummary?.TotalTaxAmount ?? 0m;
                return ZReportStats.TaxSummary.TaxBreakdown.Values.Sum(x => x.TaxAmount);
            }
        }

        //Total taxable amount calculated from tax breakdown 
        public decimal CalculatedTotalOrderAmount
        {
            get
            {
                if (ZReportStats?.TaxSummary?.TaxBreakdown == null || ZReportStats.TaxSummary.TaxBreakdown.Count == 0)
                    return ZReportStats?.TaxSummary?.TotalOrderAmount ?? 0m;
                return ZReportStats.TaxSummary.TaxBreakdown.Values.Sum(x => x.OrderAmount);
            }
        }

        public bool NoActiveSession
        {
            get => _noActiveSession;
            set
            {
                if (_noActiveSession == value) return;
                _noActiveSession = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowReportContent));
            }
        }

        public bool IsLoadingSession
        {
            get => _isLoadingSession;
            set
            {
                if (_isLoadingSession == value) return;
                _isLoadingSession = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowReportContent));
            }
        }

        /// <summary>Show the report content (session loaded and not "no active session").</summary>
        public bool ShowReportContent => !IsLoadingSession && !NoActiveSession && Session != null;

        public ObservableCollection<PlatformDisplayModel> FilteredPlatformStats =>
            BuildFilteredPlatformStats();

        public ObservableCollection<PlatformCashDisplayModel> FilteredPlatformCashStats =>
            BuildFilteredPlatformCashStats();

        public ObservableCollection<PlatformCardDisplayModel> FilteredPlatformCardStats =>
            BuildFilteredPlatformCardStats();

        public bool HasCardSales =>
            FilteredPlatformCardStats != null && FilteredPlatformCardStats.Count > 0;

        public ObservableCollection<PlatformCashRefundDisplayModel> FilteredPlatformCashRefundStats =>
            BuildFilteredPlatformCashRefundStats();

        public ObservableCollection<PlatformCardRefundDisplayModel> FilteredPlatformCardRefundStats =>
            BuildFilteredPlatformCardRefundStats();

        public bool HasPlatformCashRefunds => FilteredPlatformCashRefundStats?.Count > 0;
        public bool HasPlatformCardRefunds => FilteredPlatformCardRefundStats?.Count > 0;

        /// <summary>Cash Sale Cash refund for Cash Drawer Summary: TotalCashSaleCashRefundAmount + TotalOtherCashSaleCashRefundAmount.</summary>
        public decimal CashDrawerCashSaleCashRefundAmount => (Session?.TotalCashSaleCashRefundAmount ?? 0m) + (Session?.TotalOtherCashSaleCashRefundAmount ?? 0m);

        public decimal TotalRefundsDisplayed
        {
            get
            {
                if (ZReportStats == null) return 0m;
                decimal total = ZReportStats.PosCardRefund
                    + ZReportStats.PosCashSaleCashRefund + ZReportStats.PosCardSaleCashRefund;
                if (ZReportStats.PlatformStats != null)
                {
                    foreach (var kvp in ZReportStats.PlatformStats)
                    {
                        if (kvp.Value.PlatformId == 9) continue;
                        var rs = kvp.Value.RefundSummary;
                        total += ParseCurrencyString(rs?.CashSaleCashRefund) + ParseCurrencyString(rs?.CardSaleCashRefund);
                    }
                }
                foreach (var item in FilteredPlatformCardRefundStats ?? new ObservableCollection<PlatformCardRefundDisplayModel>())
                    total += item.CardRefund;
                return total;
            }
        }

        public ObservableCollection<PlatformDiscountDisplayModel> FilteredPlatformDiscountStats =>
            BuildFilteredPlatformDiscountStats();

        public decimal PosVoucherDiscount
        {
            get
            {
                if (ZReportStats?.PlatformStats == null) return 0m;
                foreach (var platform in ZReportStats.PlatformStats.Values)
                {
                    if (platform.PlatformId == 9)
                        return ParseCurrencyString(platform.DiscountSummary?.VoucherDiscount ?? "0.00");
                }
                return 0m;
            }
        }

        public ObservableCollection<PlatformVoucherDisplayModel> FilteredPlatformVoucherStats =>
            BuildFilteredPlatformVoucherStats();

        private static decimal ParseCurrencyString(string currencyString)
        {
            if (string.IsNullOrWhiteSpace(currencyString)) return 0m;
            var cleaned = currencyString.Replace("£", "").Replace("$", "").Replace("€", "").Replace(",", "").Replace(" ", "").Trim();
            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : 0m;
        }

        public bool HasZReportStats => ZReportStats != null;
        public bool HasTaxSummary => ZReportStats?.TaxSummary != null;
        public bool ShowNoZReportData => !IsLoadingZReportStats && !HasZReportStats;

        public bool IsLoadingZReportStats
        {
            get => _isLoadingZReportStats;
            set
            {
                _isLoadingZReportStats = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowNoZReportData));
            }
        }

        /// <summary>True while the X report is being printed (Generate clicked). Used to show loader overlay in the dialog.</summary>
        public bool IsPrinting
        {
            get => _isPrinting;
            set
            {
                if (_isPrinting == value) return;
                _isPrinting = value;
                OnPropertyChanged();
            }
        }

        public XReportDialogViewModel()
        {
            _apiService = new ApiService();
            _localStorageService = new LocalStorageService();
            LoadShopDetails();
            _ = LoadActiveSessionAsync();
        }

        private void LoadShopDetails()
        {
            try
            {
                var shop = _localStorageService.GetShopDetails();
                OutletName = shop?.Name ?? "Unknown Outlet";
                OnPropertyChanged(nameof(OutletName));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[XReportDialog] Failed to load shop details: {ex.Message}");
                OutletName = "Unknown Outlet";
                OnPropertyChanged(nameof(OutletName));
            }
        }

        private async Task LoadActiveSessionAsync()
        {
            IsLoadingSession = true;
            NoActiveSession = false;
            Session = null;
            try
            {
                var activeSession = await _apiService.GetActiveCashDrawerSessionAsync(xReport: true);
                if (activeSession == null)
                {
                    NoActiveSession = true;
                    return;
                }
                var sessionModel = new CashDrawerSessionModel
                {
                    Id = activeSession.Id,
                    CashDrawerId = activeSession.CashDrawerId,
                    SessionStartedUserId = activeSession.SessionStartedUserId,
                    SessionStartedUser = activeSession.SessionStartedUser,
                    SessionEndedUser = null,
                    SessionEndedUserId = null,
                    OpenedAt = activeSession.OpenedAt,
                    OpeningBalance = activeSession.OpeningBalance,
                    ClosedAt = null,
                    ClosingBalanceCounted = null,
                    ClosingBalanceExpected = activeSession.ClosingBalanceExpected,
                    Difference = 0m,
                    TotalInAmount = activeSession.TotalInAmount,
                    TotalOutAmount = activeSession.TotalOutAmount,
                    TotalSalesAmount = activeSession.TotalSalesAmount,
                    TotalRefundAmount = activeSession.TotalRefundAmount,
                    TotalCashSaleCashRefundAmount = activeSession.TotalCashSaleCashRefundAmount,
                    TotalCardSaleCashRefundAmount = activeSession.TotalCardSaleCashRefundAmount,
                    TotalOtherCashSaleCashRefundAmount = activeSession.TotalOtherCashSaleCashRefundAmount,
                    OtherSalesAmount = activeSession.OtherSalesAmount,
                    Status = activeSession.Status ?? "Open",
                    CreatedAt = activeSession.CreatedAt,
                    UpdatedAt = activeSession.UpdatedAt
                };
                Session = sessionModel;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[XReportDialog] Failed to load active session: {ex.Message}");
                NoActiveSession = true;
            }
            finally
            {
                IsLoadingSession = false;
            }
        }

        private async Task LoadZReportStatsAsync()
        {
            if (Session == null) return;

            IsLoadingZReportStats = true;
            try
            {
                var fromDate = Session.OpenedAt;
                var toDate = DateTime.UtcNow;

                var zReportStats = await _apiService.GetZReportStatsAsync(fromDate.ToUniversalTime(), toDate.AddDays(1).ToUniversalTime());
                if (zReportStats != null)
                    zReportStats.CalculateOrderCounts();

                ZReportStats = zReportStats;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[XReportDialog] Failed to load Z-Report stats: {ex.Message}");
                ZReportStats = null;
            }
            finally
            {
                IsLoadingZReportStats = false;
            }
        }

        private ObservableCollection<PlatformDisplayModel> BuildFilteredPlatformStats()
        {
            var collection = new ObservableCollection<PlatformDisplayModel>();
            if (ZReportStats?.PlatformStats == null) return collection;
            foreach (var kvp in ZReportStats.PlatformStats)
            {
                var platform = kvp.Value;
                if (platform.PlatformId != 9)
                    collection.Add(new PlatformDisplayModel { PlatformName = kvp.Key, PlatformId = platform.PlatformId, PlatformStats = platform });
            }
            return collection;
        }

        private ObservableCollection<PlatformCashDisplayModel> BuildFilteredPlatformCashStats()
        {
            var collection = new ObservableCollection<PlatformCashDisplayModel>();
            if (ZReportStats?.PlatformStats == null) return collection;
            foreach (var kvp in ZReportStats.PlatformStats)
            {
                var platform = kvp.Value;
                if (platform.PlatformId != 9)
                    collection.Add(new PlatformCashDisplayModel { PlatformName = kvp.Key, PlatformId = platform.PlatformId, PlatformStats = platform });
            }
            return collection;
        }

        private ObservableCollection<PlatformCardDisplayModel> BuildFilteredPlatformCardStats()
        {
            var collection = new ObservableCollection<PlatformCardDisplayModel>();
            if (ZReportStats?.PlatformStats == null) return collection;
            foreach (var kvp in ZReportStats.PlatformStats)
            {
                var platform = kvp.Value;
                if (platform.PlatformId != 9)
                {
                    var cardOrderCount = platform.TenderSummary?.CardOnlineOrderCount ?? 0;
                    var cardRevenue = ParseCurrencyString(platform.TenderSummary?.CardOnlineRevenue);
                    if (cardOrderCount > 0 || cardRevenue > 0m)
                        collection.Add(new PlatformCardDisplayModel { PlatformName = kvp.Key, PlatformId = platform.PlatformId, PlatformStats = platform });
                }
            }
            return collection;
        }

        private ObservableCollection<PlatformCashRefundDisplayModel> BuildFilteredPlatformCashRefundStats()
        {
            var collection = new ObservableCollection<PlatformCashRefundDisplayModel>();
            if (ZReportStats?.PlatformStats == null) return collection;
            foreach (var kvp in ZReportStats.PlatformStats)
            {
                var platform = kvp.Value;
                if (platform.PlatformId == 9) continue;
                var cashRefund = ParseCurrencyString(platform.RefundSummary?.CashRefund);
                var cashSaleCashRefund = ParseCurrencyString(platform.RefundSummary?.CashSaleCashRefund);
                var cardSaleCashRefund = ParseCurrencyString(platform.RefundSummary?.CardSaleCashRefund);
                if (cashRefund > 0m || cashSaleCashRefund > 0m || cardSaleCashRefund > 0m)
                    collection.Add(new PlatformCashRefundDisplayModel { PlatformName = kvp.Key, PlatformStats = platform });
            }
            return collection;
        }

        private ObservableCollection<PlatformCardRefundDisplayModel> BuildFilteredPlatformCardRefundStats()
        {
            var collection = new ObservableCollection<PlatformCardRefundDisplayModel>();
            if (ZReportStats?.PlatformStats == null) return collection;
            foreach (var kvp in ZReportStats.PlatformStats)
            {
                var platform = kvp.Value;
                if (platform.PlatformId != 9 && ParseCurrencyString(platform.RefundSummary?.CardRefund) > 0m)
                    collection.Add(new PlatformCardRefundDisplayModel { PlatformName = kvp.Key, PlatformStats = platform });
            }
            return collection;
        }

        private ObservableCollection<PlatformDiscountDisplayModel> BuildFilteredPlatformDiscountStats()
        {
            var collection = new ObservableCollection<PlatformDiscountDisplayModel>();
            if (ZReportStats?.PlatformStats == null) return collection;
            foreach (var kvp in ZReportStats.PlatformStats)
            {
                var platform = kvp.Value;
                if (platform.PlatformId != 9)
                    collection.Add(new PlatformDiscountDisplayModel { PlatformName = kvp.Key, PlatformId = platform.PlatformId, PlatformStats = platform });
            }
            return collection;
        }

        private ObservableCollection<PlatformVoucherDisplayModel> BuildFilteredPlatformVoucherStats()
        {
            var collection = new ObservableCollection<PlatformVoucherDisplayModel>();
            if (ZReportStats?.PlatformStats == null) return collection;
            foreach (var kvp in ZReportStats.PlatformStats)
            {
                var platform = kvp.Value;
                if (platform.PlatformId != 9)
                    collection.Add(new PlatformVoucherDisplayModel { PlatformName = kvp.Key, PlatformId = platform.PlatformId, PlatformStats = platform });
            }
            return collection;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
