using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using POS_UI.Services;
using POS_UI.Models;
using System.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace POS_UI.View
{
    public partial class EndShiftDialog : UserControl
    {
        public EndShiftDialog()
        {
            InitializeComponent();
        }

        public decimal? EnteredCashAmount { get; private set; }

        private async void EndShift_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(CashAmountTextBox.Text, out var amount))
            {
                EnteredCashAmount = amount;
                
                // Call API to close cash drawer session
                try
                {
                    var apiService = new ApiService();
                    var success = await apiService.CloseCashDrawerSessionAsync(amount);
                    
                    if (success)
                    {
                        // Close the dialog with amount so SettingsViewModel can show success dialog, then ZReportDialog
                        DialogHost.CloseDialogCommand.Execute(amount, null);
                    }
                    else
                    {
                        DialogHost.CloseDialogCommand.Execute(amount, null);
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Failed to End Shift", "Unable to close cash drawer shift. Please try again.");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        DialogHost.Show(dlg, "RootDialog");
                    }
                }
                catch (Exception ex)
                {
                    // Close the current dialog first; the API throws on non-success so we land here
                    // and need to dismiss this dialog before showing a status dialog on the same host
                    string jsonPart = ex.Message;
                    int jsonStart = ex.Message.IndexOf("{");
                    if (jsonStart >= 0)
                    {
                        jsonPart = ex.Message.Substring(jsonStart);
                    }
                    
                    // Try to parse the error response as JSON using dynamic parsing
                    var jsonObject = JsonConvert.DeserializeObject<dynamic>(jsonPart);
                    string header = jsonObject?.message ?? "End Session Failed";
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
                    }

                    // Add space only before capital that follows lowercase (e.g. "someError" -> "some Error"),
                    // so order IDs like "KQAH,ZZ07" are not broken into "K Q A H, Z Z07"
                    string friendlyText = Regex.Replace(errorDetails, "([a-z])([A-Z])", "$1 $2");
                    
                    try { DialogHost.CloseDialogCommand.Execute(null, null); } catch { /* ignore */ }
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError(header, friendlyText);
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    DialogHost.Show(dlg, "RootDialog");

                }
            }
            else
            {
                // Close the current dialog first; the API throws on non-success so we land here
                // and need to dismiss this dialog before showing a status dialog on the same host
                try { DialogHost.CloseDialogCommand.Execute(null, null); } catch { /* ignore */ }
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Invalid Amount", "Please enter a valid numeric cash amount.");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                DialogHost.Show(dlg, "RootDialog");
            }
        }
    }
}

