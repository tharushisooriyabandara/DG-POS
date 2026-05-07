using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using POS_UI.ViewModels;
using POS_UI.Models;

namespace POS_UI.View
{
    public partial class ConnectedPrinterGroupsDialog : UserControl
    {
        public ConnectedPrinterGroupsDialogViewModel ViewModel { get; set; }

        public ConnectedPrinterGroupsDialog(PrinterGroupModel printerGroup, ICommand testPrintCommand = null)
        {
            InitializeComponent();
            ViewModel = new ConnectedPrinterGroupsDialogViewModel(printerGroup, testPrintCommand);
            DataContext = ViewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.CloseDialogCommand.Execute(null, null);
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggleButton && 
                toggleButton.Tag is PrinterWithSelectionModel printerWithSelection)
            {
                printerWithSelection.IsSelected = true;
                ViewModel.SavePrinterSelection(printerWithSelection);
            }
        }

        private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggleButton && 
                toggleButton.Tag is PrinterWithSelectionModel printerWithSelection)
            {
                printerWithSelection.IsSelected = false;
                ViewModel.SavePrinterSelection(printerWithSelection);
            }
        }
    }
}
