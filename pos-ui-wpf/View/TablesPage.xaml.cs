using MaterialDesignThemes.Wpf;
using POS_UI.View;
using POS_UI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using POS_UI.Services;

namespace POS_UI
{
    public partial class TablesPage : Page
    {
        public TablesPage()
        {
            InitializeComponent();
            this.DataContext = new TablesViewModel();
            Loaded += (_, _) =>
            {
                if (DataContext is TablesViewModel vm)
                {
                    vm.RefreshFloorPlanLayoutState();
                }
            };
        }

        private void CashierButton_Click(object sender, RoutedEventArgs e)
        {
            var navigationService = new NavigationStateService();
            navigationService.SaveNavigationState("/View/CashierHomePage.xaml", "CashierHomePage");
            NavigationService?.Navigate(new Uri("/View/CashierHomePage.xaml", UriKind.Relative));
        }
        private async void OrderItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is POS_UI.Models.OrderModel order)
            {
                try
                {
                    var dialog = new KitchenOrderDetailsDialog
                    {
                        DataContext = new POS_UI.ViewModels.KitchenOrderDetailsDialogViewModel(order.ApiId, POS_UI.ViewModels.KitchenOrderDetailsDialogViewModel.DialogMode.Tables)
                    };
                    await DialogHost.Show(dialog, "RootDialog");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error showing order details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
} 