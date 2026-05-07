using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using POS_UI.Models;

namespace POS_UI.ViewModels
{
    public class TaxInfoDialogViewModel : BaseViewModel
    {
        private string _title = "Tax Information";
        public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }

        public ObservableCollection<TaxBreakdownItem> TaxRows { get; set; }
        
        private decimal _totalTaxAmount;
        public decimal TotalTaxAmount 
        { 
            get => _totalTaxAmount; 
            set 
            { 
                _totalTaxAmount = value; 
                OnPropertyChanged(); 
            } 
        }
        
        private decimal _totalOrderAmount;
        public decimal TotalOrderAmount 
        { 
            get => _totalOrderAmount; 
            set 
            { 
                _totalOrderAmount = value; 
                OnPropertyChanged(); 
            } 
        }

        public ICommand CloseCommand { get; }

        public TaxInfoDialogViewModel(TaxSummaryModel taxData = null)
        {
            CloseCommand = new RelayCommand(() => DialogHost.Close("RootDialog"));
            TaxRows = new ObservableCollection<TaxBreakdownItem>();
            
            if (taxData != null)
            {
                TotalTaxAmount = taxData.TotalTaxAmount;
                TotalOrderAmount = taxData.TotalOrderAmount;
                PopulateTaxRows(taxData);
            }
        }

        private void PopulateTaxRows(TaxSummaryModel taxData)
        {
            TaxRows.Clear();
            
            if (taxData?.TaxBreakdown != null && taxData.TaxBreakdown.Count > 0)
            {
                foreach (var breakdown in taxData.TaxBreakdown.OrderByDescending(x => 
                {
                    if (decimal.TryParse(x.Key, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rate))
                        return rate;
                    return 0m;
                }))
                {
                    TaxRows.Add(breakdown.Value);
                }
            }
        }
    }
}

