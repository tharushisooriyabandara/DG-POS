using System;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using POS_UI.Services;
using POS_UI.Models;

namespace POS_UI.View
{
    public partial class CashInOutDialog : UserControl
    {
        public enum CashFlowType { CashIn, CashOut }

        public CashFlowType FlowType { get; }
        public decimal? EnteredAmount { get; private set; }

        public CashDrawerSessionModel ActiveCashDrawerSession { get; set; }

        public CashInOutDialog(CashFlowType type, CashDrawerSessionModel activeSession = null)
        {
            InitializeComponent();
            FlowType = type;
            TitleText.Text = type == CashFlowType.CashIn ? "Cash In" : "Cash Out";
            PrimaryButton.Content = type == CashFlowType.CashIn ? "Cash In" : "Cash Out";
            
            // Use provided session or load from API
            if (activeSession != null)
            {
                ActiveCashDrawerSession = activeSession;
            }
            else
            {
                // Load active session from API
                LoadActiveCashDrawerSessionAsync();
            }
        }

        private async void LoadActiveCashDrawerSessionAsync()
        {
            try
            {
                var apiService = new ApiService();
                var activeSession = await apiService.GetActiveCashDrawerSessionAsync();
                
                if (activeSession != null)
                {
                    // Convert CashDrawerActiveSessionModel to CashDrawerSessionModel
                    ActiveCashDrawerSession = new CashDrawerSessionModel
                    {
                        Id = activeSession.Id,
                        CashDrawerId = activeSession.CashDrawerId,
                        SessionStartedUserId = activeSession.SessionStartedUserId,
                        SessionStartedUser = activeSession.SessionStartedUser,
                        OpenedAt = activeSession.OpenedAt,
                        OpeningBalance = activeSession.OpeningBalance,
                        ClosingBalanceExpected = activeSession.ClosingBalanceExpected,
                        TotalInAmount = activeSession.TotalInAmount,
                        TotalOutAmount = activeSession.TotalOutAmount,
                        TotalSalesAmount = activeSession.TotalSalesAmount,
                        TotalRefundAmount = activeSession.TotalRefundAmount,
                        OtherSalesAmount = activeSession.OtherSalesAmount,
                        Status = activeSession.Status,
                        CreatedAt = activeSession.CreatedAt,
                        UpdatedAt = activeSession.UpdatedAt
                    };
                }
                else
                {
                    // If no active session, initialize with empty model
                    ActiveCashDrawerSession = new CashDrawerSessionModel();
                }
            }
            catch (Exception ex)
            {
                // On error, initialize with empty model
                ActiveCashDrawerSession = new CashDrawerSessionModel();
                System.Diagnostics.Debug.WriteLine($"Error loading active cash drawer session: {ex.Message}");
            }
        }

        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(AmountTextBox.Text, out var amount))
            {
                EnteredAmount = amount;
                var note = NoteTextBox?.Text?.Trim();
                if (string.IsNullOrWhiteSpace(note))
                {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Note Required", "Please enter a note for the cash movement.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    await DialogHost.Show(dlg, "CashInOutDialogHost");
                    return;
                }
                if (FlowType == CashFlowType.CashOut && ActiveCashDrawerSession.ClosingBalanceExpected < EnteredAmount) 
                {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Insufficient Funds", "The cash drawer balance is less than the amount to be cashed out.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    await DialogHost.Show(dlg, "CashInOutDialogHost");
                    return;
                }
                var movementType = FlowType == CashFlowType.CashIn ? "PAY_IN" : "PAY_OUT";
                try
                {
                    var api = new ApiService();
                    var ok = await api.RecordCashMovementAsync(movementType, amount, note);
                    
                    // Close current dialog before showing status
                    try { DialogHost.CloseDialogCommand.Execute(amount, null); } catch { }

                    if (ok)
                    {
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateSuccess("Success", $"{(FlowType == CashFlowType.CashIn ? "Cash In" : "Cash Out")} recorded: {amount:0.00}");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        DialogHost.Show(dlg, "RootDialog");
                    }
                    else
                    {
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed", "Unable to record cash movement. Please try again.");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        DialogHost.Show(dlg, "RootDialog");
                    }
                }
                catch (Exception ex)
                {
                    try { DialogHost.CloseDialogCommand.Execute(null, null); } catch { }
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("API Error", ex.Message);
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    DialogHost.Show(dlg, "RootDialog");
                }
            }
            else
            {
                try { DialogHost.CloseDialogCommand.Execute(null, null); } catch { }
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Invalid Amount", "Please enter a valid numeric amount.");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                DialogHost.Show(dlg, "RootDialog");
            }
        }
    }
}


