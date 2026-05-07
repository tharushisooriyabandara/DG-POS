using System.Windows;
using POS_UI.ViewModels;
using POS_UI.Models;

namespace POS_UI.View
{
    public partial class AddCardMachineDialog : Window
    {
        private AddCardMachineDialogViewModel _viewModel;
        
        public AddCardMachineDialog(CardMachineModel cardMachine = null)
        {
            InitializeComponent();
            
            _viewModel = new AddCardMachineDialogViewModel();
            DataContext = _viewModel;
            
            // Subscribe to property changes to handle dialog result
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            
            // If editing existing card machine, populate the form
            if (cardMachine != null)
            {
                _viewModel.IsEditMode = true;
                _viewModel.PopulateFromCardMachine(cardMachine);
            }
        }
        
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.DialogResult))
            {
                if (_viewModel.DialogResult)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    DialogResult = false;
                    Close();
                }
            }
        }
        
        public CardMachineModel GetCardMachine()
        {
            return _viewModel?.CreateCardMachine();
        }

        public void SetAuthToken(string authToken)
        {
            _viewModel?.SetAuthTokenForEdit(authToken);
        }
    }
} 