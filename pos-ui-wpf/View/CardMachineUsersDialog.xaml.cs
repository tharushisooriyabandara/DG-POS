using System.Windows;
using POS_UI.ViewModels;

namespace POS_UI.View
{
    public partial class CardMachineUsersDialog : Window
    {
        public CardMachineUsersDialog(CardMachineUsersDialogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
} 