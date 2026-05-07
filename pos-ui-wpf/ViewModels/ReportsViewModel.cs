using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using POS_UI.Models;
using POS_UI.Services;
using POS_UI.ViewModels;
using Microsoft.Win32;
using System.IO;
using System.Text;

namespace POS_UI.ViewModels
{
    public class ReportsViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly SettingsService _settingsService;
        private readonly LocalStorageService _localStorageService = new LocalStorageService();
        
        private ObservableCollection<OrderModel> _posOrders;
        private ObservableCollection<OrderModel> _allOrders;
        private bool _isLoading;
        private bool _isPrintingXReport;
        private string _currency;
        private DateTime? _fromDate;
        private DateTime? _toDate;
        private string _posStatsRaw;
        
        public ICommand ShowPosStatsCommand { get; }
        public ICommand ShowDashboardStatsCommand { get; }
        public ICommand PrintZReportCommand { get; }
        public ICommand ClearDatesCommand { get; }
        
        public string CurrentPage { get; set; }

        public ReportsViewModel()
        {
            _apiService = new ApiService();
            _settingsService = new SettingsService();
            CurrentPage = "Reports";
            _posOrders = new ObservableCollection<OrderModel>();
            _allOrders = new ObservableCollection<OrderModel>();
            
            // Initialize currency from local storage (ShopDetails)
            try
            {
                var shop = _localStorageService.GetShopDetails();
                Currency = string.IsNullOrWhiteSpace(shop?.Currency) ? "£" : shop.Currency;
            }
            catch
            {
                Currency = "Rsss";
            }

            // Default dates to today
            FromDate = DateTime.Today;
            ToDate = DateTime.Today;

            // Initial fetch of POS stats
            _ = RefreshPosStatsAsync();

            ShowPosStatsCommand = new RelayCommand(async () => await RefreshPosStatsAsync(), () => !IsLoading && FromDate.HasValue && ToDate.HasValue);
            ShowDashboardStatsCommand = new RelayCommand(async () => await RefreshDashboardStatsAsync(), () => !IsLoading && FromDate.HasValue && ToDate.HasValue);
            PrintZReportCommand = new RelayCommand(async () => await PrintZReportAsync(), () => !IsLoading);
            ClearDatesCommand = new RelayCommand(() =>
            {
                FromDate = null;
                ToDate = null;
            });
        }

        public ObservableCollection<OrderModel> POSOrders
        {
            get => _posOrders;
            set
            {
                _posOrders = value;
                OnPropertyChanged(nameof(POSOrders));
            }
        }

        public ObservableCollection<OrderModel> AllOrders
        {
            get => _allOrders;
            set
            {
                _allOrders = value;
                OnPropertyChanged(nameof(AllOrders));
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
                (ShowPosStatsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public bool IsPrintingXReport
        {
            get => _isPrintingXReport;
            set
            {
                _isPrintingXReport = value;
                OnPropertyChanged(nameof(IsPrintingXReport));
            }
        }
        
        public DateTime? FromDate
        {
            get => _fromDate;
            set { _fromDate = value; OnPropertyChanged(nameof(FromDate)); }
        }
        
        public DateTime? ToDate
        {
            get => _toDate;
            set { _toDate = value; OnPropertyChanged(nameof(ToDate)); }
        }
        
        // Raw response holder (optional for debugging/binding)
        public string PosStatsRaw
        {
            get => _posStatsRaw;
            private set { _posStatsRaw = value; OnPropertyChanged(nameof(PosStatsRaw)); }
        }
        // Currency symbol from local storage ShopDetails
        public string Currency
        {
            get => _currency;
            private set
            {
                _currency = value;
                OnPropertyChanged(nameof(Currency));
            }
        }

        // Summary computed properties for POS Orders
        public int TotalOrders  {get; set;}
        public int TotalDineInOrders {get; set;}
        public int TotalTakeawayOrders {get; set;}
        public int TotalDeliveryOrders {get; set;}

        public decimal TotalAmount {get; set;}
        public decimal TotalDineInAmount {get; set;}
        public decimal TotalDeliveryAmount {get; set;}
        public decimal TotalTakeawayAmount {get; set;}

        // POS Orders payment-type summaries
        public int TotalPOSCashOrders { get; set; }
        public int TotalPOSCardOrders { get; set; }
        public decimal POSCashOrdersAmount { get; set; }
        public decimal POSCardOrdersAmount { get; set; }

        // POS Orders additional summaries
        public int PosCancelledOrders { get; set; }
        public decimal PosTotalRevenue { get; set; }
        public decimal PosGrossRevenue { get; set; }
        public decimal PosCancelledAmount { get; set; }
        public Models.TaxSummaryModel PosTaxData { get; set; }
        
        public decimal PosTotalTaxAmount 
        { 
            get => PosTaxData?.TotalTaxAmount ?? 0m; 
        }

        // Summary computed properties for All Orders
        public int AllTotalOrders {get; set;}
        public int AllCompletedOrders {get; set;}
        public int AllAcceptedOrders {get; set;}
        public int AllCancelledOrders {get; set;}
        public int AllDeclinedOrders {get; set;}
        public int AllReadyOrders {get; set;}
        public string AllBestSellingPlatform {get; set;}
        public string AllBestSellingPlatformUrl {get; set;}
           
        public decimal AllTotalRevenue {get; set;}

        public async Task RefreshPosStatsAsync()
        {
            try
            {
                if (!FromDate.HasValue || !ToDate.HasValue) return;
                IsLoading = true;
                var stats = await _apiService.GetPosStatsAsync(FromDate.Value, ToDate.Value);
                PosStatsRaw = string.Empty;

                TotalOrders = stats.TotalOrders;
                TotalDineInOrders = stats.DineInOrders;
                TotalTakeawayOrders = stats.TakeawayOrders;
                TotalDeliveryOrders = stats.DeliveryOrders;

                PosTotalRevenue = stats.NetRevenue;
                PosGrossRevenue = stats.GrossRevenue;

                TotalDineInAmount = stats.DineInRevenue;
                TotalDeliveryAmount = stats.DeliveryRevenue;
                TotalTakeawayAmount = stats.TakeawayRevenue;

                POSCashOrdersAmount = stats.CashRevenue;
                POSCardOrdersAmount = stats.CardRevenue;

                PosCancelledOrders = stats.CancelledOrders;
                PosCancelledAmount = stats.CancelledRevenue;

                TotalPOSCashOrders = stats.CashOrders;
                TotalPOSCardOrders = stats.CardOrders;
                
                PosTaxData = stats.Tax;
                
                // Trigger property change notifications for UI binding
                OnPropertyChanged(nameof(PosTaxData));
                OnPropertyChanged(nameof(PosTotalTaxAmount));
                OnPropertyChanged(nameof(TotalOrders));
                OnPropertyChanged(nameof(TotalDineInOrders));
                OnPropertyChanged(nameof(TotalTakeawayOrders));
                OnPropertyChanged(nameof(TotalDeliveryOrders));
                OnPropertyChanged(nameof(PosTotalRevenue));
                OnPropertyChanged(nameof(PosGrossRevenue));
                OnPropertyChanged(nameof(TotalDineInAmount));
                OnPropertyChanged(nameof(TotalDeliveryAmount));
                OnPropertyChanged(nameof(TotalTakeawayAmount));
                OnPropertyChanged(nameof(POSCashOrdersAmount));
                OnPropertyChanged(nameof(POSCardOrdersAmount));
                OnPropertyChanged(nameof(TotalPOSCashOrders));
                OnPropertyChanged(nameof(TotalPOSCardOrders));
                OnPropertyChanged(nameof(PosCancelledOrders));
                OnPropertyChanged(nameof(PosCancelledAmount));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load POS stats: {ex.Message}", "Reports", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task RefreshDashboardStatsAsync()
        {
            try
            {
                if (!FromDate.HasValue || !ToDate.HasValue) return;
                IsLoading = true;
                var stats = await _apiService.GetDashboardStatsAsync(FromDate.Value, ToDate.Value);

                AllTotalOrders = stats.TotalOrders;
                AllCompletedOrders = stats.CompletedOrders;
                AllAcceptedOrders = stats.AcceptedOrders;
                AllCancelledOrders = stats.CancelledOrders;
                AllDeclinedOrders = stats.DeclinedOrders;
                AllReadyOrders = stats.ReadyForPickupOrders;
                AllBestSellingPlatform = stats.BestPlatform?.Name ?? "N/A";
                AllBestSellingPlatformUrl = stats.BestPlatform?.Url ?? string.Empty;
                AllTotalRevenue = stats.NetRevenue;

                OnPropertyChanged(nameof(AllTotalOrders));
                OnPropertyChanged(nameof(AllCompletedOrders));
                OnPropertyChanged(nameof(AllAcceptedOrders));
                OnPropertyChanged(nameof(AllCancelledOrders));
                OnPropertyChanged(nameof(AllDeclinedOrders));
                OnPropertyChanged(nameof(AllReadyOrders));
                OnPropertyChanged(nameof(AllBestSellingPlatform));
                OnPropertyChanged(nameof(AllBestSellingPlatformUrl));
                OnPropertyChanged(nameof(AllTotalRevenue));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load dashboard stats: {ex.Message}", "Reports", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task PrintZReportAsync()
        {
            try
            {
                // Prompt for date range via modal (defaults pre-filled to today)
                DateTime defaultFrom = FromDate ?? DateTime.Today;
                DateTime defaultTo = ToDate ?? DateTime.Today;

                // Fallback to WPF Window dialog to avoid host issues
                var dialogVm = new DateRangeDialogViewModel { FromDate = defaultFrom, ToDate = defaultTo, Title = "Z Report" };
                var win = new POS_UI.View.DateRangeWindow { DataContext = dialogVm };
                var owner = System.Windows.Application.Current?.Windows?.OfType<System.Windows.Window>()?.FirstOrDefault(w => w.IsActive)
                            ?? System.Windows.Application.Current?.MainWindow;
                double originalOpacity = 1.0;
                bool faded = false;
                if (owner != null)
                {
                    win.Owner = owner;
                    originalOpacity = owner.Opacity;
                    owner.Opacity = 0.6; // fade background while modal is open
                    faded = true;
                }
                bool ok = false;
                try
                {
                    ok = win.ShowDialog() == true;
                }
                finally
                {
                    if (faded && owner != null)
                    {
                        owner.Opacity = originalOpacity;
                    }
                }
                if (!ok)
                {
                    return;
                }
                FromDate = dialogVm.FromDate;
                ToDate = dialogVm.ToDate;

                IsLoading = true;
                var platformSummaries = await _apiService.GetPlatformSummariesAsync(FromDate.Value, ToDate.Value);
                System.Diagnostics.Debug.WriteLine($"[ZReport] Platform summary count: {platformSummaries?.Count ?? 0}");

                if (platformSummaries == null || platformSummaries.Count == 0)
                {
                    System.Windows.MessageBox.Show("No platform data for selected date range.", "Z Report", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                var shop = _localStorageService.GetShopDetails();
                var currency = string.IsNullOrWhiteSpace(shop?.Currency) ? "£" : shop.Currency;

                var sb = new System.Text.StringBuilder();
                // Centered big title by using bold with no pipe (renderer centers titles)
                sb.AppendLine("**Z REPORT**");
                sb.AppendLine(new string('=', 50));
                // Show date range on one aligned line - use country-based date formatting
                var receiptService = POS_UI.Services.ReceiptPrintingService.Instance;
                sb.AppendLine($"FROM: {receiptService.FormatDateOnly(FromDate.Value)} | TO: {receiptService.FormatDateOnly(ToDate.Value)}");
                sb.AppendLine(new string('-', 50));

                int grandCount = 0;
                decimal grandRevenue = 0m;
                foreach (var p in platformSummaries)
                {
                    // Header per platform with brand suffix
                    var headerName = string.IsNullOrWhiteSpace(p.BrandName) ? p.Name : $"{p.Name} ({p.BrandName})";
                    sb.AppendLine($"**{headerName}**");
                    sb.AppendLine(new string('-', 50));

                    // Print all metrics present (order categories and totals)
                    if (p.Metrics != null && p.Metrics.Count > 0)
                    {
                        foreach (var kv in p.Metrics)
                        {
                            // Align like other receipts: label left, price right using pipe
                            sb.AppendLine($"{kv.Key}:|**{currency} {kv.Value:F2}**");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"Revenue:|**{currency} {p.Revenue:F2}**");
                    }

                    // Add to grand totals using Total Sales if available
                    if (p.Metrics != null && p.Metrics.TryGetValue("Total Sales", out var platTotal)) grandRevenue += platTotal; else grandRevenue += p.Revenue;
                    grandCount += p.OrderCount; // count may not be present; remains 0

                    sb.AppendLine(new string('-', 50));
                }

                sb.AppendLine(new string('-', 50));
                // Final line: show total revenue only (no order count)
                sb.AppendLine($"**TOTAL REVENUE:**|**{currency} {grandRevenue:F2}**");

                // Hide loader when printing actually starts
                IsLoading = false;

                // OPTIMIZATION: Print to all active printers in PARALLEL instead of sequentially
                var activePrinters = POS_UI.Services.ReceiptPrintingService.Instance.GetActivePrinters();
                var receiptContent = sb.ToString();
                var printTasks = activePrinters.Select(printer => 
                    Task.Run(async () =>
                    {
                        try { await POS_UI.Services.ReceiptPrintingService.Instance.PrintRawContentAsync(printer, receiptContent); } catch { }
                    })
                ).ToList();
                
                if (printTasks.Count > 0)
                {
                    await Task.WhenAll(printTasks);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to print Z Report: {ex.Message}", "Z Report", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
