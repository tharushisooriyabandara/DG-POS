using System;
using System.Windows.Input;

namespace POS_UI.ViewModels
{
	public class RefundMethodDialogViewModel : BaseViewModel
	{
		public Action RequestClose { get; set; }
		public string Title { get; set; } = "Refund Method";
		private decimal _refundAmount;
		public decimal RefundAmount { get => _refundAmount; set { _refundAmount = value; OnPropertyChanged(); OnPropertyChanged(nameof(RefundAmountText)); } }
		public string RefundAmountText => RefundAmount.ToString("F2");
		public string SelectedChoice { get; private set; } // "CARD" or "SKIP" or null

		public ICommand UseCardMachineCommand { get; }
		public ICommand SkipRefundCommand { get; }
		public ICommand CancelCommand { get; }

		public RefundMethodDialogViewModel()
		{
			UseCardMachineCommand = new RelayCommand(() => { SelectedChoice = "CARD"; RequestClose?.Invoke(); });
			SkipRefundCommand = new RelayCommand(() => { SelectedChoice = "SKIP"; RequestClose?.Invoke(); });
			CancelCommand = new RelayCommand(() => { SelectedChoice = null; RequestClose?.Invoke(); });
		}
	}
}


