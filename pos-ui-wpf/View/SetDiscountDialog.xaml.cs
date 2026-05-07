using System.Windows;
using System.Windows.Controls;
using POS_UI.Services;
namespace POS_UI.View
{
    public partial class SetDiscountDialog : UserControl
    {
        public SetDiscountDialog()
        {
            InitializeComponent();
        }

        private void QuickDiscount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag && FindName("PercentTextBox") is TextBox percentBox)
            {
                percentBox.Text = tag;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, null);
        }

        private void SavePercentButton_Click(object sender, RoutedEventArgs e)
        {
            var discountValue = (FindName("PercentTextBox") as TextBox)?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(discountValue))
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Invalid Discount", "Please enter a value between 1 and 100, or enter a valid amount.");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "NestedModifiersDialogHost");
                return;
            }

            if (decimal.TryParse(discountValue, out decimal percent) && percent > 0 && percent <= 100)
            {
                MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(discountValue, null);
                return;
            }

            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Invalid Discount", "Please enter a value between 1 and 100, or enter a valid amount.");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "NestedModifiersDialogHost");
            }
        }

        private void SaveAmountButton_Click(object sender, RoutedEventArgs e)
        {
            var amountValue = (FindName("AmountTextBox") as TextBox)?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(amountValue))
            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Invalid Amount", "Invalid amount. Please enter a valid number.");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "NestedModifiersDialogHost");
                return;
            }

            if (decimal.TryParse(amountValue, out decimal amount) && amount >= 0)
            {
                // Prefix a token to distinguish amount from percent; ViewModel can parse
                // e.g., "amount:12.50"

                var cartTotal = CartService.Instance.SubTotal;

                if (amount >= cartTotal)
                {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Invalid Amount", "Discount cannot exceed the order total.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "NestedModifiersDialogHost");
                    return;
                }

                var payload = $"amount:{amountValue}";
                MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(payload, null);
                return;
            }

            {
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Invalid Amount", "Invalid amount. Please enter a valid number.");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "NestedModifiersDialogHost");
            }
        }

        private void QuickAmount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag && FindName("AmountTextBox") is TextBox amountBox)
            {
                amountBox.Text = tag;
            }
        }
    }
} 