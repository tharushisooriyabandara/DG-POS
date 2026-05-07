using System.Windows;
using POS_UI.ViewModels;

namespace POS_UI.View
{
    public partial class AddCardMachineUserDialog : Window
    {
        public AddCardMachineUserDialog(AddCardMachineUserDialogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
} 