using System;
using System.Windows.Input;

namespace POS_UI.ViewModels
{
    public class PrintSelectionDialogViewModel : BaseViewModel
    {
        public string SelectedChoice { get; private set; }
        public Action RequestClose { get; set; }

        public ICommand SelectMainCommand { get; }
        public ICommand SelectKitchenCommand { get; }
        public ICommand SelectBothCommand { get; }

        public PrintSelectionDialogViewModel()
        {
            SelectMainCommand = new RelayCommand(() => Choose("MAIN"));
            SelectKitchenCommand = new RelayCommand(() => Choose("KITCHEN"));
            SelectBothCommand = new RelayCommand(() => Choose("BOTH"));
        }

        private void Choose(string choice)
        {
            SelectedChoice = choice;
            // Close active DialogHost programmatically without requiring identifier
            MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, null);
            // Also support Window-based fallback
            RequestClose?.Invoke();
        }
    }
}


