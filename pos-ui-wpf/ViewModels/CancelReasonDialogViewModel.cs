using System.Collections.Generic;

namespace POS_UI.ViewModels
{
    public class CancelReasonDialogViewModel : BaseViewModel
    {
        public string PlaceholderReason { get; } = "Please select a reason";

        private string _selectedReason;
        public string SelectedReason 
        { 
            get => _selectedReason; 
            set 
            { 
                _selectedReason = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(CanProceed));
            } 
        }

        public List<string> CancellationReasons { get; }

        public bool CanProceed => !string.IsNullOrWhiteSpace(SelectedReason) 
            && !string.Equals(SelectedReason, PlaceholderReason);

        public CancelReasonDialogViewModel()
        {
            CancellationReasons = new List<string>
            {
                PlaceholderReason,
                "Restaurant closed",
                "Customer wants to cancel the order",
                "Rider not Available",
                "Item not Available",
                "Other"
            };

            SelectedReason = PlaceholderReason;
        }
    }
}

