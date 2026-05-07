using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using MaterialDesignThemes.Wpf;
using POS_UI.Models;

namespace POS_UI.ViewModels
{
    /// <summary>One outlet table row in the Settings floor plan "Select Table" dialog.</summary>
    public sealed class FloorPlanTableSelectionRow : INotifyPropertyChanged
    {
        public TableModel Table { get; }
        public bool IsAlreadyOnFloorPlan { get; }
        public bool IsUnavailable => Table.Status == TableStatus.Unavailable;
        public bool IsPickEnabled => !IsUnavailable && !IsAlreadyOnFloorPlan;

        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public FloorPlanTableSelectionRow(TableModel table, bool isAlreadyOnFloorPlan)
        {
            Table = table;
            IsAlreadyOnFloorPlan = isAlreadyOnFloorPlan;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Floor plan editor: pick one or more tables that are not unavailable and not already placed on any floor plan.</summary>
    public class FloorPlanTableSelectionDialogViewModel : BaseViewModel
    {
        public ObservableCollection<FloorPlanTableSelectionRow> Rows { get; }

        public bool HasSelectedPickable => Rows.Any(r => r.IsSelected && r.IsPickEnabled);

        public RelayCommand<FloorPlanTableSelectionRow> ToggleRowSelectionCommand { get; }
        public RelayCommand SelectAllPickableCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public FloorPlanTableSelectionDialogViewModel(IEnumerable<TableModel> tables, HashSet<int> tableIdsUsedOnAnyFloorPlan)
        {
            Rows = new ObservableCollection<FloorPlanTableSelectionRow>(
                tables.OrderBy(t => t.Name).Select(t =>
                    new FloorPlanTableSelectionRow(t, tableIdsUsedOnAnyFloorPlan.Contains(t.ApiId))));

            ToggleRowSelectionCommand = new RelayCommand<FloorPlanTableSelectionRow>(ToggleRowSelection, row => row != null && row.IsPickEnabled);
            SelectAllPickableCommand = new RelayCommand(SelectAllPickable, () => Rows.Any(r => r.IsPickEnabled));
            SaveCommand = new RelayCommand(Save, () => HasSelectedPickable);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void ToggleRowSelection(FloorPlanTableSelectionRow row)
        {
            if (row == null || !row.IsPickEnabled)
            {
                return;
            }

            row.IsSelected = !row.IsSelected;
            OnPropertyChanged(nameof(HasSelectedPickable));
            SaveCommand.RaiseCanExecuteChanged();
        }

        private void SelectAllPickable()
        {
            foreach (var r in Rows.Where(r => r.IsPickEnabled))
            {
                r.IsSelected = true;
            }

            OnPropertyChanged(nameof(HasSelectedPickable));
            SaveCommand.RaiseCanExecuteChanged();
        }

        private void Save()
        {
            var picked = Rows.Where(r => r.IsSelected && r.IsPickEnabled).Select(r => r.Table).ToList();
            if (picked.Count == 0)
            {
                return;
            }

            DialogHost.CloseDialogCommand.Execute(picked, null);
        }

        private void Cancel()
        {
            DialogHost.CloseDialogCommand.Execute(null, null);
        }
    }
}
