using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using POS_UI.Models;
using POS_UI.ViewModels;
using POS_UI.Services;

namespace POS_UI.View
{
    /// <summary>
    /// Interaction logic for ReportsPage.xaml
    /// </summary>
    public partial class ReportsPage : Page
    {
        public ReportsPage()
        {
            InitializeComponent();
        }

        // Ensure DatePicker opens calendar when clicking anywhere, without swallowing child clicks
        private void DatePicker_FocusOpen(object sender, MouseButtonEventArgs e)
        {
            if (sender is DatePicker dp)
            {
                if (!dp.IsDropDownOpen)
                {
                    dp.IsDropDownOpen = true;
                    dp.DisplayDate = dp.SelectedDate ?? System.DateTime.Today;
                }
                // Do NOT set e.Handled = true; it prevents date selection inside the calendar
            }
        }

        private void DatePicker_FocusOpen(object sender, TouchEventArgs e)
        {
            if (sender is DatePicker dp)
            {
                if (!dp.IsDropDownOpen)
                {
                    dp.IsDropDownOpen = true;
                    dp.DisplayDate = dp.SelectedDate ?? System.DateTime.Today;
                }
                // Do NOT set e.Handled = true
            }
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DatePicker dp)
            {
                dp.IsDropDownOpen = false;
            }
        }

        private void DatePicker_CalendarClosed(object sender, RoutedEventArgs e)
        {
            if (sender is DatePicker dp)
            {
                dp.IsDropDownOpen = false;
            }
        }

        private void DatePicker_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is DatePicker dp)
            {
                dp.DisplayDate = dp.SelectedDate ?? System.DateTime.Today;
            }
        }

        private void DatePicker_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is DatePicker dp)
            {
                dp.IsDropDownOpen = true;
                dp.DisplayDate = dp.SelectedDate ?? System.DateTime.Today;
            }
        }

        private void SidebarControl_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is TabControl tc && tc.SelectedItem is TabItem tab && tab.Header is string header)
            {
                if (header.Equals("All Orders", StringComparison.OrdinalIgnoreCase))
                {
                    if (DataContext is POS_UI.ViewModels.ReportsViewModel vm)
                    {
                        vm.FromDate = DateTime.Today;
                        vm.ToDate = DateTime.Today;
                        if (vm.ShowDashboardStatsCommand?.CanExecute(null) == true)
                        {
                            vm.ShowDashboardStatsCommand.Execute(null);
                        }
                    }
                }
                else if (header.Equals("Shifts", StringComparison.OrdinalIgnoreCase))
                {
                    // Ensure the embedded SettingsViewModel loads cash drawer data
                    var settingsVm = this.FindResource("ShiftsSettingsVM") as POS_UI.ViewModels.SettingsViewModel;
                    if (settingsVm != null)
                    {
                        // Switch to Cash Drawer tab flow to trigger loads
                        if (settingsVm.SwitchTabCommand?.CanExecute("Cash Drawer") == true)
                        {
                            settingsVm.SwitchTabCommand.Execute("Cash Drawer");
                        }
                    }
                }
            }
        }

        private async void ViewCashSession_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is CashDrawerSessionModel session)
            {
                var dialog = new CashSessionDetailsDialog(session);
                await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog");
            }
        }

        private async void TaxBreakdownCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is POS_UI.ViewModels.ReportsViewModel viewModel)
            {
                var taxData = viewModel.PosTaxData;
                var dialogViewModel = new POS_UI.ViewModels.TaxInfoDialogViewModel(taxData);
                var dialog = new TaxInfoDialog { DataContext = dialogViewModel };
                await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog");
            }
        }

        private async void XReportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new XReportDialog();
            dialog.OnGenerateRequested = async () =>
            {
                var xReportVm = dialog.DataContext as XReportDialogViewModel;
                await PrintXReportAsync(xReportVm);
            };
            await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog");
        }

        /// <summary>
        /// Prints X Report using data already loaded by the XReportDialogViewModel.
        /// Reuses Session and ZReportStats from the view to avoid duplicate API calls.
        /// Only makes the lightweight GetCashDrawerSessionsAsync call for fresh session data.
        /// </summary>
        private async Task PrintXReportAsync(XReportDialogViewModel xReportVm)
        {
            try
            {
                if (DataContext is ReportsViewModel viewModel)
                {
                    viewModel.IsPrintingXReport = true;
                }

                // Check printer availability FIRST before any work
                var printersService = PrintersService.Instance;
                var hasValidPrinter = false;
                
                foreach (var printer in printersService.Printers)
                {
                    if (printer.IsActive)
                    {
                        var printerSettings = PrinterSettingsService.Instance.GetPrinterSettings(printer.DeviceName);
                        if (printerSettings != null && printerSettings.MainReceipt)
                        {
                            hasValidPrinter = true;
                            break;
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

                // Reuse Session already loaded by the dialog ViewModel (no duplicate API call)
                var session = xReportVm?.Session;
                if (session == null)
                {
                    System.Windows.MessageBox.Show(
                        "No active cash drawer session found. Cannot print X-Report.",
                        "Print Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // Reuse ZReportStats already loaded by the dialog ViewModel (no duplicate API call)
                POS_UI.Models.ZReportStatsModel zReportStats = xReportVm.ZReportStats;

                // Only fetch from API if the dialog didn't have it loaded (e.g. previous load failed)
                if (zReportStats == null)
                {
                    try
                    {
                        var apiService = new ApiService();
                        var fromDate = session.OpenedAt;
                        var toDate = DateTime.UtcNow;
                        zReportStats = await apiService.GetZReportStatsAsync(fromDate.ToUniversalTime(), toDate.AddDays(1).ToUniversalTime());
                    }
                    catch (Exception apiEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[XReport] Failed to fetch Z-Report stats: {apiEx.Message}");
                        System.Windows.MessageBox.Show(
                            $"Failed to fetch Z-Report stats: {apiEx.Message}\nCannot print report without data.",
                            "Print Error",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                        return;
                    }
                }

                if (zReportStats == null)
                {
                    System.Windows.MessageBox.Show(
                        "Unable to fetch Z-Report stats. Cannot print report.",
                        "Print Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }

                // Only make the lightweight GetCashDrawerSessionsAsync call to get fresh session data for printing
                List<CashDrawerSessionModel> cashSessions = null;
                try
                {
                    var apiService = new ApiService();
                    var fromDate = session.OpenedAt;
                    var toDate = DateTime.UtcNow;
                    var cashSessionsFromApi = await apiService.GetCashDrawerSessionsAsync(fromDate, toDate);
                    if (cashSessionsFromApi != null && cashSessionsFromApi.Count > 0)
                    {
                        var targetSession = cashSessionsFromApi.FirstOrDefault(s => s.Id == session.Id)
                                          ?? cashSessionsFromApi.FirstOrDefault();
                        cashSessions = targetSession != null
                            ? new List<CashDrawerSessionModel> { targetSession }
                            : new List<CashDrawerSessionModel> { session };
                    }
                    else
                    {
                        cashSessions = new List<CashDrawerSessionModel> { session };
                    }
                }
                catch (Exception sessionsEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[XReport] Failed to fetch cash drawer sessions: {sessionsEx.Message}");
                    cashSessions = new List<CashDrawerSessionModel> { session };
                }

                var sessionToPrint = cashSessions?.FirstOrDefault() ?? session;

                try
                {
                    await ReceiptPrintingService.Instance.PrintReportReceiptAsync(sessionToPrint, zReportStats, cashSessions, null);
                }
                catch (Exception printEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[XReport] PrintReportReceiptAsync failed: {printEx.Message}");
                    System.Windows.MessageBox.Show(
                        $"Failed to print X-Report:\n{printEx.Message}\n\nPlease check printer connection and settings.",
                        "Print Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[XReport] Failed to print X-Report: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Failed to print X-Report: {ex.Message}",
                    "Print Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                if (DataContext is ReportsViewModel viewModel)
                {
                    viewModel.IsPrintingXReport = false;
                }
            }
        }
    }
}
