using System.Windows;
using System.Windows.Controls;

namespace POS_UI.View
{
    /// <summary>
    /// Interaction logic for XReportConfirmationDialog.xaml
    /// </summary>
    public partial class XReportConfirmationDialog : UserControl
    {
        public XReportConfirmationDialog()
        {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Close the dialog with false result
            MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(false, null);
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            // Close the dialog with true result
            MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(true, null);
        }
    }
}

