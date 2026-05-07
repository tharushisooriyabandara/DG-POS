using System.Windows.Controls;

namespace POS_UI.View
{
    public partial class CustomerPage : UserControl
    {
        public CustomerPage()
        {
            InitializeComponent();
            DataContext = new ViewModels.CustomerViewModel();
        }
    }
}
