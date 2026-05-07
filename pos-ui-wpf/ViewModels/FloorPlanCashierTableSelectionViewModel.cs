using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using POS_UI.Converters;
using POS_UI.Models;

namespace POS_UI.ViewModels
{
    /// <summary>Cashier dine-in table selection using configured floor plans (nav tabs + canvas).</summary>
    public class FloorPlanCashierTableSelectionViewModel : BaseViewModel, IDisposable
    {
        private readonly Action<TableModel> _selectTableFromItem;
        private bool _disposed;
        private bool _pickRebuildPosted;

        public ObservableCollection<FloorPlanModel> FloorPlans { get; } = new ObservableCollection<FloorPlanModel>();
        public ObservableCollection<FloorPlanTablePickItem> PickItems { get; } = new ObservableCollection<FloorPlanTablePickItem>();

        /// <summary>Non-table floor elements (decorative); drawn under interactive table tiles.</summary>
        public ObservableCollection<FloorPlanTablePlacementModel> FloorPlanDecorPlacements { get; } = new ObservableCollection<FloorPlanTablePlacementModel>();
        public ObservableCollection<TableModel> LiveTables { get; }

        private string? _incomingTableName;
        public string? IncomingTableName
        {
            get => _incomingTableName;
            set { _incomingTableName = value; OnPropertyChanged(); }
        }

        public int? IncomingOrderSessionId { get; }

        private FloorPlanModel? _selectedFloorPlan;
        public FloorPlanModel? SelectedFloorPlan
        {
            get => _selectedFloorPlan;
            set
            {
                if (ReferenceEquals(_selectedFloorPlan, value)) return;
                _selectedFloorPlan = value;
                OnPropertyChanged();
                RebuildPickItems();
                RecomputeCanvasSize();
            }
        }

        private TableModel? _selectedTable;
        public TableModel? SelectedTable
        {
            get => _selectedTable;
            set
            {
                _selectedTable = value;
                OnPropertyChanged();
                SaveCommand.RaiseCanExecuteChanged();
            }
        }

        public double CanvasHostWidth { get; private set; } = 640;
        public double CanvasHostHeight { get; private set; } = 420;

        public double DialogMaxWidth => Math.Min(1320, CanvasHostWidth + 160);
        public double DialogMaxHeight => Math.Min(920, CanvasHostHeight + 320);
        public double DialogScrollMaxWidth => DialogMaxWidth - 40;

        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public FloorPlanCashierTableSelectionViewModel(
            IEnumerable<FloorPlanModel> floorPlanClones,
            ObservableCollection<TableModel> liveTables,
            TableModel? preselectedTable,
            string? incomingTableName,
            int? incomingOrderSessionId)
        {
            LiveTables = liveTables;
            IncomingTableName = incomingTableName;
            IncomingOrderSessionId = incomingOrderSessionId;
            _selectTableFromItem = SelectTableFromPick;

            foreach (var fp in floorPlanClones)
            {
                FloorPlans.Add(fp.Clone());
            }

            SaveCommand = new RelayCommand(Save, () => SelectedTable != null);
            CancelCommand = new RelayCommand(Cancel);

            FloorPlanModel? initialPlan = FloorPlans.FirstOrDefault();
            TableModel? resolvedPreselect = null;
            if (preselectedTable != null)
            {
                resolvedPreselect = LiveTables.FirstOrDefault(t => t.ApiId == preselectedTable.ApiId) ?? preselectedTable;
                var matchPlan = FloorPlans.FirstOrDefault(fp => fp.Tables.Any(p => p.TableId == resolvedPreselect.ApiId));
                if (matchPlan != null)
                {
                    initialPlan = matchPlan;
                }
            }

            _selectedFloorPlan = initialPlan;
            OnPropertyChanged(nameof(SelectedFloorPlan));
            RebuildPickItems();
            RecomputeCanvasSize();

            if (resolvedPreselect != null)
            {
                SelectedTable = resolvedPreselect;
                SyncPickSelectionToSelectedTable();
            }

            // Tables load async after dialog opens; rebuild pick items when the shared collection fills.
            ((INotifyCollectionChanged)liveTables).CollectionChanged += OnLiveTablesCollectionChanged;
        }

        private void OnLiveTablesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_disposed) return;
            SchedulePickItemsRebuild();
        }

        /// <summary>Coalesce Clear + many Adds from <see cref="LoadTablesAsync"/> into a single rebuild.</summary>
        private void SchedulePickItemsRebuild()
        {
            if (_disposed) return;
            if (_pickRebuildPosted) return;
            _pickRebuildPosted = true;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                _pickRebuildPosted = false;
                RebuildPickItems();
                return;
            }

            dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (_disposed)
                {
                    _pickRebuildPosted = false;
                    return;
                }

                _pickRebuildPosted = false;
                RebuildPickItems();
            }));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ((INotifyCollectionChanged)LiveTables).CollectionChanged -= OnLiveTablesCollectionChanged;
            foreach (var p in PickItems.ToList())
            {
                p.Dispose();
            }

            PickItems.Clear();
        }

        private void SelectTableFromPick(TableModel table)
        {
            if (!TableSelectionTableEnabledConverter.IsTableSelectable(table, IncomingOrderSessionId))
            {
                return;
            }

            foreach (var p in PickItems)
            {
                p.IsSelected = ReferenceEquals(p.Table, table);
            }

            SelectedTable = table;
        }

        private void SyncPickSelectionToSelectedTable()
        {
            if (SelectedTable == null) return;
            foreach (var p in PickItems)
            {
                p.IsSelected = p.Table.ApiId == SelectedTable.ApiId;
            }
        }

        private void RebuildPickItems()
        {
            if (_disposed) return;

            foreach (var p in PickItems.ToList())
            {
                p.Dispose();
            }

            PickItems.Clear();
            if (SelectedFloorPlan == null)
            {
                return;
            }

            foreach (var placement in SelectedFloorPlan.Tables.OrderBy(t => t.Y).ThenBy(t => t.X))
            {
                if (placement.Kind != FloorPlanElementKind.Table)
                {
                    continue;
                }

                var live = LiveTables.FirstOrDefault(t => t.ApiId == placement.TableId);
                if (live == null)
                {
                    continue;
                }

                PickItems.Add(new FloorPlanTablePickItem(live, placement, IncomingOrderSessionId, _selectTableFromItem));
            }

            FloorPlanDecorPlacements.Clear();
            foreach (var placement in SelectedFloorPlan.Tables
                         .Where(p => p.Kind == FloorPlanElementKind.CustomItem)
                         .OrderBy(t => t.Y).ThenBy(t => t.X))
            {
                FloorPlanDecorPlacements.Add(placement);
            }

            SyncPickSelectionToSelectedTable();
        }

        private void RecomputeCanvasSize()
        {
            if (SelectedFloorPlan == null || SelectedFloorPlan.Tables.Count == 0)
            {
                CanvasHostWidth = 520;
                CanvasHostHeight = 360;
            }
            else
            {
                CanvasHostWidth = Math.Max(400, SelectedFloorPlan.Tables.Max(t => t.X + t.Width) + 80);
                CanvasHostHeight = Math.Max(280, SelectedFloorPlan.Tables.Max(t => t.Y + t.Height) + 80);
            }

            OnPropertyChanged(nameof(FloorPlanDecorPlacements));

            OnPropertyChanged(nameof(CanvasHostWidth));
            OnPropertyChanged(nameof(CanvasHostHeight));
            OnPropertyChanged(nameof(DialogMaxWidth));
            OnPropertyChanged(nameof(DialogMaxHeight));
            OnPropertyChanged(nameof(DialogScrollMaxWidth));
        }

        private void Save()
        {
            if (SelectedTable != null)
            {
                DialogHost.CloseDialogCommand.Execute(SelectedTable, null);
            }
        }

        private void Cancel()
        {
            DialogHost.CloseDialogCommand.Execute(null, null);
        }
    }
}
