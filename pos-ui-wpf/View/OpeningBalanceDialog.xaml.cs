using System;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using POS_UI.Services;

namespace POS_UI.View
{
    public partial class OpeningBalanceDialog : UserControl
    {
        public decimal? EnteredOpeningBalance { get; private set; }
        public string DialogHostIdentifier { get; set; } = "RootDialog";

        public OpeningBalanceDialog()
        {
            InitializeComponent();
        }

        private string GetDialogHostForStatus()
        {
            // Use AddItemDialogHost if that's what was set, otherwise use RootDialog
            return DialogHostIdentifier == "AddItemDialogHost" ? "AddItemDialogHost" : "RootDialog";
        }

        private async void StartShift_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(OpeningBalanceTextBox.Text, out var openingBalance))
            {
                EnteredOpeningBalance = openingBalance;
                
                try
                {
                    // Call API with opening balance
                    var api = new ApiService();
                    var ok = await api.OpenCashDrawerSessionAsync(openingBalance);
                    
                    // Close the dialog before showing status
                    try { DialogHost.CloseDialogCommand.Execute(null, null); } catch { }
                    
                    if (ok)
                    {
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateSuccess("Shift Started", "Shift has been started successfully. You can now use the cashier.");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        DialogHost.Show(dlg, GetDialogHostForStatus());
                    }
                    else
                    {
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to Start Shift", "Could not start the shift. Please try again.");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        DialogHost.Show(dlg, GetDialogHostForStatus());
                    }
                }
                catch (Exception ex)
                {
                    try { DialogHost.CloseDialogCommand.Execute(null, null); } catch { }
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("API Error", ex.Message);
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    DialogHost.Show(dlg, GetDialogHostForStatus());
                }
            }
            else
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Invalid Amount", "Please enter a valid numeric amount for opening balance.");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                DialogHost.Show(dlg, GetDialogHostForStatus());
            }
        }
    }
}

