using System.Collections.ObjectModel;
using System.Windows.Input;
using POS_UI.Models;
using MaterialDesignThemes.Wpf;

namespace POS_UI.ViewModels
{
    public class TableSelectionDialogViewModel : BaseViewModel
    {
        public ObservableCollection<TableModel> Tables { get; set; }
        private string _incomingTableName;
        public string IncomingTableName
        {
            get => _incomingTableName;
            set { _incomingTableName = value; OnPropertyChanged(); }
        }
        //Incoming order's order_session_id. When set, reserved tables with matching session are selectable.</summary>
        public int? IncomingOrderSessionId { get; set; }
        private TableModel _selectedTable;
        public TableModel SelectedTable
        {
            get => _selectedTable;
            set { _selectedTable = value; OnPropertyChanged(); }
        }
        public ICommand SelectTableCommand { get; }
        public RelayCommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public TableSelectionDialogViewModel(ObservableCollection<TableModel> tables, TableModel selectedTable, string incomingTableName = null, int? incomingOrderSessionId = null)
        {
            Tables = tables;
            SelectedTable = selectedTable;
            IncomingTableName = incomingTableName;
            IncomingOrderSessionId = incomingOrderSessionId;
            SelectTableCommand = new RelayCommand<TableModel>(SelectTable);
            SaveCommand = new RelayCommand(Save, () => SelectedTable != null);
            CancelCommand = new RelayCommand(Cancel);
        }
        private void SelectTable(TableModel table)
        {
            foreach (var t in Tables) t.IsSelected = false;
            table.IsSelected = true;
            SelectedTable = table;
            // Update Save button availability
            SaveCommand.RaiseCanExecuteChanged();
        }
        private void Save() { DialogHost.CloseDialogCommand.Execute(SelectedTable, null); }
        private void Cancel() { DialogHost.CloseDialogCommand.Execute(null, null); }
    }
} 