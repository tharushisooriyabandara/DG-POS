using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace POS_UI.View
{
    /// <summary>
    /// Interaction logic for DraftsDialog.xaml
    /// </summary>
    public partial class DraftsDialog : OptimizedDialogBase
    {
        public DraftsDialog()
        {
            InitializeComponent();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Console.WriteLine($"Border_MouseLeftButtonDown called for sender: {sender}");
                
                if (sender is Border border && border.DataContext is POS_UI.Models.DraftOrderModel draft)
                {
                    Console.WriteLine($"Draft found: {draft.CustomerName} - {draft.OrderType}");
                    
                    var viewModel = DataContext as POS_UI.ViewModels.DraftsDialogViewModel;
                    if (viewModel != null)
                    {
                        Console.WriteLine($"ViewModel found, executing LoadDraftCommand");
                        viewModel.LoadDraftCommand.Execute(draft);
                    }
                    else
                    {
                        Console.WriteLine("ViewModel is null!");
                    }
                }
                else
                {
                    var dataContext = sender is Border borderElement ? borderElement.DataContext : "Not a Border";
                    Console.WriteLine($"Invalid sender or DataContext. Sender: {sender}, DataContext: {dataContext}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Border_MouseLeftButtonDown: {ex.Message}");
                MessageBox.Show($"Error loading draft: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
