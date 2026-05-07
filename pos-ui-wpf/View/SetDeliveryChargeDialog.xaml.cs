using System.Windows;
using System.Windows.Controls;

namespace POS_UI.View
{
    public partial class SetDeliveryChargeDialog : UserControl
    {
        public SetDeliveryChargeDialog()
        {
            InitializeComponent();
        }

        private void QuickAmount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                CustomAmountTextBox.Text = tag;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, null);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var amountValue = CustomAmountTextBox.Text?.Trim();
            MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(amountValue, null);
        }
    }
} 