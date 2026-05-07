using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using POS_UI.Models;
using POS_UI.Services;
using System.Globalization;

namespace POS_UI.ViewModels
{
    public class CashSessionDetailsDialogViewModel : INotifyPropertyChanged
    {
        private CashDrawerSessionModel _session;
        private ZReportStatsModel _zReportStats;
        private bool _isLoadingZReportStats;
        private bool _isPrinting;
        private readonly ApiService _apiService;
        private readonly LocalStorageService _localStorageService;
        public ICommand PrintSessionCommand { get; }
        public string OutletName { get; set; }

        public CashDrawerSessionModel Session
        {
            get => _session;
            set
            {
                _session = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CashDrawerCashSaleCashRefundAmount));
                // Load Z-Report stats when session is set
                if (_session != null)
                {
                    _ = LoadZReportStatsAsync();
                }
            }
        }

        /// <summary>Cash Sale Cash refund for Cash Drawer Summary: TotalCashSaleCashRefundAmount + TotalOtherCashSaleCashRefundAmount.</summary>
        public decimal CashDrawerCashSaleCashRefundAmount => (Session?.TotalCashSaleCashRefundAmount ?? 0m) + (Session?.TotalOtherCashSaleCashRefundAmount ?? 0m);

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
                //OnPropertyChanged(nameof(PosVoucherDiscountOrderCount));
                OnPropertyChanged(nameof(PosVoucherDiscount));
            }
        }

        public ObservableCollection<PlatformDisplayModel> FilteredPlatformStats
        {
            get
            {
                var collection = new ObservableCollection<PlatformDisplayModel>();
                if (ZReportStats?.PlatformStats != null)
                {
                    foreach (var kvp in ZReportStats.PlatformStats)
                    {
                        var platform = kvp.Value;
                        // Only include platforms where PlatformId != 9
                        if (platform.PlatformId != 9)
                        {
                            var displayModel = new PlatformDisplayModel
                            {
                                PlatformName = kvp.Key,
                                PlatformId = platform.PlatformId,
                                PlatformStats = platform
                            };
                            collection.Add(displayModel);
                        }
                    }
                }
                return collection;
            }
        }

        public ObservableCollection<PlatformCashDisplayModel> FilteredPlatformCashStats
        {
            get
            {
                var collection = new ObservableCollection<PlatformCashDisplayModel>();
                if (ZReportStats?.PlatformStats != null)
                {
                    foreach (var kvp in ZReportStats.PlatformStats)
                    {
                        var platform = kvp.Value;
                        // Only include platforms where PlatformId != 9
                        if (platform.PlatformId != 9)
                        {
                            var displayModel = new PlatformCashDisplayModel
                            {
                                PlatformName = kvp.Key,
                                PlatformId = platform.PlatformId,
                                PlatformStats = platform
                            };
                            collection.Add(displayModel);
                        }
                    }
                }
                return collection;
            }
        }

        public ObservableCollection<PlatformCardDisplayModel> FilteredPlatformCardStats
        {
            get
            {
                var collection = new ObservableCollection<PlatformCardDisplayModel>();
                if (ZReportStats?.PlatformStats != null)
                {
                    foreach (var kvp in ZReportStats.PlatformStats)
                    {
                        var platform = kvp.Value;
                        // Only include platforms where PlatformId != 9
                        if (platform.PlatformId != 9)
                        {
                            var cardOrderCount = platform.TenderSummary?.CardOnlineOrderCount ?? 0;
                            var cardRevenue = ParseCurrencyString(platform.TenderSummary?.CardOnlineRevenue);
                            
                            // Only include if there are orders or revenue
                            if (cardOrderCount > 0 || cardRevenue > 0m)
                            {
                                var displayModel = new PlatformCardDisplayModel
                                {
                                    PlatformName = kvp.Key,
                                    PlatformId = platform.PlatformId,
                                    PlatformStats = platform
                                };
                                collection.Add(displayModel);
                            }
                        }
                    }
                }
                return collection;
            }
        }

        public bool HasCardSales
        {
            get
            {
                return FilteredPlatformCardStats != null && FilteredPlatformCardStats.Count > 0;
            }
        }

        public ObservableCollection<PlatformCashRefundDisplayModel> FilteredPlatformCashRefundStats
        {
            get
            {
                var collection = new ObservableCollection<PlatformCashRefundDisplayModel>();
                if (ZReportStats?.PlatformStats != null)
                {
                    foreach (var kvp in ZReportStats.PlatformStats)
                    {
                        var platform = kvp.Value;
                        if (platform.PlatformId != 9)
                        {
                            var cashRefund = ParseCurrencyString(platform.RefundSummary?.CashRefund);
                            var cashSaleCashRefund = ParseCurrencyString(platform.RefundSummary?.CashSaleCashRefund);
                            var cardSaleCashRefund = ParseCurrencyString(platform.RefundSummary?.CardSaleCashRefund);
                            // Include platform if it has any of: cash refund, cash sale cash refund, card sale cash refund
                            if (cashRefund > 0m || cashSaleCashRefund > 0m || cardSaleCashRefund > 0m)
                            {
                                collection.Add(new PlatformCashRefundDisplayModel
                                {
                                    PlatformName = kvp.Key,
                                    PlatformStats = platform
                                });
                            }
                        }
                    }
                }
                return collection;
            }
        }

        public ObservableCollection<PlatformCardRefundDisplayModel> FilteredPlatformCardRefundStats
        {
            get
            {
                var collection = new ObservableCollection<PlatformCardRefundDisplayModel>();
                if (ZReportStats?.PlatformStats != null)
                {
                    foreach (var kvp in ZReportStats.PlatformStats)
                    {
                        var platform = kvp.Value;
                        if (platform.PlatformId != 9)
                        {
                            var cardRefund = ParseCurrencyString(platform.RefundSummary?.CardRefund);
                            if (cardRefund > 0m)
                            {
                                collection.Add(new PlatformCardRefundDisplayModel
                                {
                                    PlatformName = kvp.Key,
                                    PlatformStats = platform
                                });
                            }
                        }
                    }
                }
                return collection;
            }
        }

        public bool HasPlatformCashRefunds => FilteredPlatformCashRefundStats != null && FilteredPlatformCashRefundStats.Count > 0;
        public bool HasPlatformCardRefunds => FilteredPlatformCardRefundStats != null && FilteredPlatformCardRefundStats.Count > 0;

        public decimal TotalRefundsDisplayed
        {
            get
            {
                if (ZReportStats == null) return 0m;
                decimal total = ZReportStats.PosCardRefund
                    + ZReportStats.PosCashSaleCashRefund + ZReportStats.PosCardSaleCashRefund;
                // Platform cash refund section: CashRefund + CashSaleCashRefund + CardSaleCashRefund per platform (Id != 9)
                if (ZReportStats.PlatformStats != null)
                {
                    foreach (var kvp in ZReportStats.PlatformStats)
                    {
                        if (kvp.Value.PlatformId == 9) continue;
                        var rs = kvp.Value.RefundSummary;
                        total += ParseCurrencyString(rs?.CashSaleCashRefund) + ParseCurrencyString(rs?.CardSaleCashRefund);
                    }
                }
                // Platform card refund section: CardRefund per platform (Id != 9)
                foreach (var item in FilteredPlatformCardRefundStats ?? new ObservableCollection<PlatformCardRefundDisplayModel>())
                    total += item.CardRefund;
                return total;
            }
        }

        public ObservableCollection<PlatformDiscountDisplayModel> FilteredPlatformDiscountStats
        {
            get
            {
                var collection = new ObservableCollection<PlatformDiscountDisplayModel>();
                if (ZReportStats?.PlatformStats != null)
                {
                    foreach (var kvp in ZReportStats.PlatformStats)
                    {
                        var platform = kvp.Value;
                        // Only include platforms where PlatformId != 9
                        if (platform.PlatformId != 9)
                        {
                            var displayModel = new PlatformDiscountDisplayModel
                            {
                                PlatformName = kvp.Key,
                                PlatformId = platform.PlatformId,
                                PlatformStats = platform
                            };
                            collection.Add(displayModel);
                        }
                    }
                }
                return collection;
            }
        }

        /*public int PosVoucherDiscountOrderCount
        {
            get
            { 
                if ( ZReportStats.PlatformStats != null)
                {
                    foreach (var platform in ZReportStats.PlatformStats.Values)
                    {
                        if (platform.PlatformId == 9)
                        {
                            return platform.DiscountSummary?.VoucherDiscountOrderCount ?? 0;
                        }
                    }
                }
                return 0;
            }
        }*/

        public decimal PosVoucherDiscount
        {
            get
            {
                if (ZReportStats?.PlatformStats != null)
                {
                    foreach (var platform in ZReportStats.PlatformStats.Values)
                    {
                        if (platform.PlatformId == 9)
                        {
                            return ParseCurrencyString(platform.DiscountSummary?.VoucherDiscount ?? "0.00");
                        }
                    }
                }
                return 0m;
            }
        }

        public ObservableCollection<PlatformVoucherDisplayModel> FilteredPlatformVoucherStats
        {
            get
            {
                var collection = new ObservableCollection<PlatformVoucherDisplayModel>();
                if (ZReportStats?.PlatformStats != null)
                {
                    foreach (var kvp in ZReportStats.PlatformStats)
                    {
                        var platform = kvp.Value;

                        //Only include platforms where PlatformId != 9
                        if (platform.PlatformId != 9)
                        {
                            var displayModel = new PlatformVoucherDisplayModel
                            {
                                PlatformName = kvp.Key,
                                PlatformId = platform.PlatformId,
                                PlatformStats = platform
                            };
                            collection.Add(displayModel);
                        }
                    }
                }
                return collection;
            }
        }
        
        private decimal ParseCurrencyString(string currencyString)
        {
            if (string.IsNullOrWhiteSpace(currencyString))
                return 0m;

            // Remove currency symbols, commas, and spaces, then parse
            var cleaned = currencyString.Replace("£", "").Replace("$", "").Replace("€", "").Replace(",", "").Replace(" ", "").Trim();
            if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
                return result;

            return 0m;
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

        public bool IsPrinting
        {
            get => _isPrinting;
            set
            {
                _isPrinting = value;
                OnPropertyChanged();
            }
        }

        public CashSessionDetailsDialogViewModel()
        {
            _apiService = new ApiService();
            _localStorageService = new LocalStorageService();
            PrintSessionCommand = new AsyncRelayCommand(async () => await PrintSessionAsync());
            LoadShopDetails();
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
                System.Diagnostics.Debug.WriteLine($"[CashSessionDetailsDialog] Failed to load shop details: {ex.Message}");
                OutletName = "Unknown Outlet";
                OnPropertyChanged(nameof(OutletName));
            }
        }

        public CashSessionDetailsDialogViewModel(CashDrawerSessionModel session) : this()
        {
            Session = session;
        }

        private async Task LoadZReportStatsAsync()
        {
            if (Session == null) return;

            IsLoadingZReportStats = true;
            try
            {
                // Use the shift's opened date and closed date (or current time if not closed)
                var fromDate = Session.OpenedAt;
                var toDate = Session.ClosedAt ?? DateTime.UtcNow;

                // Fetch Z-Report stats from API using the shift's opened and closed dates
                // Note: CalculateOrderCounts() is already called inside GetZReportStatsAsync
                var zReportStats = await _apiService.GetZReportStatsAsync(fromDate.ToUniversalTime(), toDate.AddMinutes(2).ToUniversalTime());

                ZReportStats = zReportStats;
            }
            catch (Exception ex)
            {
                // Log error but don't block UI - Z-Report stats are optional for display
                System.Diagnostics.Debug.WriteLine($"[CashSessionDetailsDialog] Failed to load Z-Report stats: {ex.Message}");
                ZReportStats = null;
            }
            finally
            {
                IsLoadingZReportStats = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async Task PrintSessionAsync()
        {
            try
            {
                if (Session == null) return;

                IsPrinting = true;

                // OPTIMIZATION: Check printer availability FIRST before any API calls
                // This avoids wasting 30-60s on API calls when no printer is configured
                var printersService = PrintersService.Instance;
                var hasValidPrinter = false;
                var printerNames = new List<string>();
                
                foreach (var printer in printersService.Printers)
                {
                    if (printer.IsActive)
                    {
                        var printerSettings = PrinterSettingsService.Instance.GetPrinterSettings(printer.DeviceName);
                        if (printerSettings != null && printerSettings.MainReceipt)
                        {
                            hasValidPrinter = true;
                            printerNames.Add(printer.DeviceName);
                        }
                    }
                }

                if (!hasValidPrinter)
                {
                    System.Windows.MessageBox.Show(
                        "No active printer found with Main Receipt enabled.\n\nPlease check:\n" +
                        "1. At least one printer is marked as Active\n" +
                        "2. The printer has 'Main Receipt' enabled in settings",
                        "Print Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // OPTIMIZATION: Reuse already-loaded ZReportStats instead of making a second API call
                // LoadZReportStatsAsync already fetched this data when the dialog opened
                POS_UI.Models.ZReportStatsModel zReportStats = ZReportStats;

                // Only fetch from API if not already loaded (e.g., if dialog just opened or previous load failed)
                if (zReportStats == null)
                {
                    try
                    {
                        var fromDate = Session.OpenedAt;
                        var toDate = Session.ClosedAt ?? DateTime.UtcNow;
                        zReportStats = await _apiService.GetZReportStatsAsync(fromDate.ToUniversalTime(), toDate.AddMinutes(2).ToUniversalTime());
                    }
                    catch (Exception apiEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CashSessionDetailsDialog] Failed to fetch Z-Report stats: {apiEx.Message}");
                        System.Windows.MessageBox.Show($"Failed to fetch Z-Report stats: {apiEx.Message}\nCannot print report without data.", "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        return;
                    }
                }

                if (zReportStats == null)
                {
                    System.Windows.MessageBox.Show("Unable to fetch Z-Report stats. Cannot print report.", "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                // Fetch cash drawer sessions (lightweight call) while we already have zReportStats
                List<CashDrawerSessionModel> cashSessions = null;
                try
                {
                    var fromDate = Session.OpenedAt;
                    var toDate = Session.ClosedAt ?? DateTime.UtcNow;
                    var cashSessionsFromApi = await _apiService.GetCashDrawerSessionsAsync(fromDate, toDate);
                    if (cashSessionsFromApi != null && cashSessionsFromApi.Count > 0)
                    {
                        var targetSession = cashSessionsFromApi.FirstOrDefault(s => s.Id == Session.Id);
                        cashSessions = targetSession != null ? new List<CashDrawerSessionModel> { targetSession } : new List<CashDrawerSessionModel> { Session };
                    }
                    else
                    {
                        cashSessions = new List<CashDrawerSessionModel> { Session };
                    }
                }
                catch (Exception sessionsEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[CashSessionDetailsDialog] Failed to fetch cash drawer sessions: {sessionsEx.Message}");
                    cashSessions = new List<CashDrawerSessionModel> { Session };
                }

                var sessionToPrint = cashSessions?.FirstOrDefault() ?? Session;

                // Print the report with already-loaded ZReportStats
                try
                {
                    await ReceiptPrintingService.Instance.PrintReportReceiptAsync(sessionToPrint, zReportStats, cashSessions, null);
                }
                catch (Exception printEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[CashSessionDetailsDialog] PrintReportReceiptAsync failed: {printEx.Message}");
                    System.Windows.MessageBox.Show(
                        $"Failed to print X-Report:\n{printEx.Message}\n\nPlease check printer connection and settings.",
                        "Print Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CashSessionDetailsDialog] Failed to print X-Report: {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to print X-Report: {ex.Message}", "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsPrinting = false;
            }
        }
    }

    public class PlatformDisplayModel
    {
        public string PlatformName { get; set; }
        public int PlatformId { get; set; }
        public PlatformStatsModel PlatformStats { get; set; }

        public int TotalOrders
        {
            get
            {
                var collectionOrders = PlatformStats?.GrossSales?.CollectionOrders ?? 0;
                var deliveryOrders = PlatformStats?.GrossSales?.DeliveryOrders ?? 0;
                return collectionOrders + deliveryOrders;
            }
        }

        public decimal TotalRevenue
        {
            get
            {
                var collectionRevenue = ParseCurrencyString(PlatformStats?.GrossSales?.CollectionRevenue);
                var deliveryRevenue = ParseCurrencyString(PlatformStats?.GrossSales?.DeliveryOrderRevenue);
                return collectionRevenue + deliveryRevenue;
            }
        }

        private decimal ParseCurrencyString(string currencyString)
        {
            if (string.IsNullOrWhiteSpace(currencyString))
                return 0m;

            // Remove currency symbols, commas, and spaces, then parse
            var cleaned = currencyString.Replace("£", "").Replace("$", "").Replace("€", "").Replace(",", "").Replace(" ", "").Trim();
            if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
                return result;

            return 0m;
        }
    }

    public class PlatformCashDisplayModel
    {
        public string PlatformName { get; set; }
        public int PlatformId { get; set; }
        public PlatformStatsModel PlatformStats { get; set; }

        public int CashOrderCount
        {
            get
            {
                return PlatformStats?.TenderSummary?.CashOrderCount ?? 0;
            }
        }

        public decimal CashRevenue
        {
            get
            {
                return ParseCurrencyString(PlatformStats?.TenderSummary?.CashRevenue);
            }
        }

        private decimal ParseCurrencyString(string currencyString)
        {
            if (string.IsNullOrWhiteSpace(currencyString))
                return 0m;

            // Remove currency symbols, commas, and spaces, then parse
            var cleaned = currencyString.Replace("£", "").Replace("$", "").Replace("€", "").Replace(",", "").Replace(" ", "").Trim();
            if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
                return result;

            return 0m;
        }
    }

    public class PlatformCardDisplayModel
    {
        public string PlatformName { get; set; }
        public int PlatformId { get; set; }
        public PlatformStatsModel PlatformStats { get; set; }

        public int CardOnlineOrderCount
        {
            get
            {
                return PlatformStats?.TenderSummary?.CardOnlineOrderCount ?? 0;
            }
        }

        public decimal CardOnlineRevenue
        {
            get
            {
                return ParseCurrencyString(PlatformStats?.TenderSummary?.CardOnlineRevenue);
            }
        }

        private decimal ParseCurrencyString(string currencyString)
        {
            if (string.IsNullOrWhiteSpace(currencyString))
                return 0m;

            // Remove currency symbols, commas, and spaces, then parse
            var cleaned = currencyString.Replace("£", "").Replace("$", "").Replace("€", "").Replace(",", "").Replace(" ", "").Trim();
            if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
                return result;

            return 0m;
        }
    }

    public class PlatformDiscountDisplayModel
    {
        public string PlatformName { get; set; }
        public int PlatformId { get; set; }
        public PlatformStatsModel PlatformStats { get; set; }

        public int DiscountOrderCount
        {
            get
            {
                return PlatformStats?.DiscountSummary?.DiscountOrderCount ?? 0;
            }
        }

        public decimal Discount
        {
            get
            {
                return ParseCurrencyString(PlatformStats?.DiscountSummary?.Discount);
            }
        }

        private decimal ParseCurrencyString(string currencyString)
        {
            if (string.IsNullOrWhiteSpace(currencyString))
                return 0m;

            // Remove currency symbols, commas, and spaces, then parse
            var cleaned = currencyString.Replace("£", "").Replace("$", "").Replace("€", "").Replace(",", "").Replace(" ", "").Trim();
            if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
                return result;

            return 0m;
        }
    }

    public class PlatformVoucherDisplayModel
    {
        public string PlatformName { get; set; }
        public int PlatformId { get; set; }
        public PlatformStatsModel PlatformStats { get; set; }

       /* public int VoucherDiscountOrderCount
        {
            get
            {
                return PlatformStats?.DiscountSummary?.VoucherDiscountOrderCount ?? 0;
            }
        }
        */
        public decimal VoucherDiscount
        {
            get
            {
                return ParseCurrencyString(PlatformStats?.DiscountSummary?.VoucherDiscount);
            }
        }

        private decimal ParseCurrencyString(string currencyString)
        {
            if (string.IsNullOrWhiteSpace(currencyString))
                return 0m;

            // Remove currency symbols, commas, and spaces, then parse
            var cleaned = currencyString.Replace("£", "").Replace("$", "").Replace("€", "").Replace(",", "").Replace(" ", "").Trim();
            if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
                return result;

            return 0m;
        }
    }

    public class PlatformCashRefundDisplayModel
    {
        public string PlatformName { get; set; }
        public PlatformStatsModel PlatformStats { get; set; }

        public decimal CashRefund
        {
            get => ParseCurrencyString(PlatformStats?.RefundSummary?.CashRefund);
        }

        public decimal CashSaleCashRefund
        {
            get => ParseCurrencyString(PlatformStats?.RefundSummary?.CashSaleCashRefund);
        }

        public decimal CardSaleCashRefund
        {
            get => ParseCurrencyString(PlatformStats?.RefundSummary?.CardSaleCashRefund);
        }

        private static decimal ParseCurrencyString(string currencyString)
        {
            if (string.IsNullOrWhiteSpace(currencyString)) return 0m;
            var cleaned = currencyString.Replace("£", "").Replace("$", "").Replace("€", "").Replace(",", "").Replace(" ", "").Trim();
            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : 0m;
        }
    }

    public class PlatformCardRefundDisplayModel
    {
        public string PlatformName { get; set; }
        public PlatformStatsModel PlatformStats { get; set; }

        public decimal CardRefund
        {
            get => ParseCurrencyString(PlatformStats?.RefundSummary?.CardRefund);
        }

        private static decimal ParseCurrencyString(string currencyString)
        {
            if (string.IsNullOrWhiteSpace(currencyString)) return 0m;
            var cleaned = currencyString.Replace("£", "").Replace("$", "").Replace("€", "").Replace(",", "").Replace(" ", "").Trim();
            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : 0m;
        }
    }
}
