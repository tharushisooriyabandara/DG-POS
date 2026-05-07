using System.Windows.Controls;
using System.Windows;
using MaterialDesignThemes.Wpf;

namespace POS_UI.View
{
    /// <summary>
    /// Interaction logic for HistoryPage.xaml
    /// </summary>
    public partial class HistoryPage : Page
    {
        public HistoryPage()
        {
            InitializeComponent();
        }

        private async void ViewOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is POS_UI.Models.OrderModel order)
            {
                try
                {
                    var dialog = new KitchenOrderDetailsDialog
                    {
                        DataContext = new POS_UI.ViewModels.KitchenOrderDetailsDialogViewModel(order.ApiId, POS_UI.ViewModels.KitchenOrderDetailsDialogViewModel.DialogMode.ViewOnly)
                    };
                    await DialogHost.Show(dialog, "RootDialog");
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Error showing order details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
