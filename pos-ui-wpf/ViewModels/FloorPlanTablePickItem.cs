using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using POS_UI.Converters;
using POS_UI.Models;

namespace POS_UI.ViewModels
{
    /// <summary>One placed table on the canvas, bound to live <see cref="TableModel"/> for status / selection rules.</summary>
    public sealed class FloorPlanTablePickItem : INotifyPropertyChanged, IDisposable
    {
        private readonly Action _onTableModelPropertyChanged;

        public TableModel Table { get; }
        public FloorPlanTablePlacementModel Placement { get; }
        public int? IncomingOrderSessionId { get; }

        /// <summary>When true (Tables page floor layout), any table can be tapped for sidebar details; cashier selection rules do not apply.</summary>
        private readonly bool _tablesPageBrowseMode;

        public ICommand TapCommand { get; }

        public string DisplayName => string.IsNullOrWhiteSpace(Table.Name) ? $"T{Table.ApiId}" : Table.Name;

        public bool IsSelectable =>
            _tablesPageBrowseMode || TableSelectionTableEnabledConverter.IsTableSelectable(Table, IncomingOrderSessionId);

        public string StatusSwatchHex => TableSelectionTableEnabledConverter.GetStatusIndicatorHex(Table, IncomingOrderSessionId);

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

        public FloorPlanTablePickItem(
            TableModel table,
            FloorPlanTablePlacementModel placement,
            int? incomingOrderSessionId,
            Action<TableModel> onSelected,
            bool tablesPageBrowseMode = false)
        {
            Table = table;
            Placement = placement;
            IncomingOrderSessionId = incomingOrderSessionId;
            _tablesPageBrowseMode = tablesPageBrowseMode;
            _onTableModelPropertyChanged = () =>
            {
                OnPropertyChanged(nameof(IsSelectable));
                OnPropertyChanged(nameof(StatusSwatchHex));
                OnPropertyChanged(nameof(DisplayName));
                CommandManager.InvalidateRequerySuggested();
            };
            Table.PropertyChanged += OnTablePropertyChanged;
            TapCommand = new RelayCommand(() =>
            {
                if (!_tablesPageBrowseMode && !TableSelectionTableEnabledConverter.IsTableSelectable(Table, IncomingOrderSessionId))
                {
                    return;
                }

                onSelected(Table);
            }, () => true);
        }

        private void OnTablePropertyChanged(object? sender, PropertyChangedEventArgs e) => _onTableModelPropertyChanged();

        public void Dispose()
        {
            Table.PropertyChanged -= OnTablePropertyChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
