using System.Windows;
using System.Text.RegularExpressions;
using POS_UI.ViewModels;
using POS_UI.Models;

namespace POS_UI.View
{
    public partial class PrinterSettingsDialog : Window
    {
        public PrinterSettingsDialogViewModel ViewModel { get; set; }

        public PrinterSettingsDialog(PrinterModel printer)
        {
            InitializeComponent();
            ViewModel = new PrinterSettingsDialogViewModel(printer);
            DataContext = ViewModel;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedStatus != null)
            {
                // Save all settings including receipt configuration
                ViewModel.SaveSettings();
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Please select a status.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void NumberValidationTextBox(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void MainReceiptCountUp_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(ViewModel.MainReceiptCount.ToString(), out int currentValue))
            {
                ViewModel.MainReceiptCount = currentValue + 1;
            }
        }

        private void MainReceiptCountDown_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(ViewModel.MainReceiptCount.ToString(), out int currentValue) && currentValue > 1)
            {
                ViewModel.MainReceiptCount = currentValue - 1;
            }
        }

        private void KitchenReceiptCountUp_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(ViewModel.KitchenReceiptCount.ToString(), out int currentValue))
            {
                ViewModel.KitchenReceiptCount = currentValue + 1;
            }
        }

        private void KitchenReceiptCountDown_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(ViewModel.KitchenReceiptCount.ToString(), out int currentValue) && currentValue > 1)
            {
                ViewModel.KitchenReceiptCount = currentValue - 1;
            }
        }
    }
} 