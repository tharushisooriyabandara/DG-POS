using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using POS_UI.Converters;
using POS_UI.Models;
using POS_UI.Services;
using POS_UI.View;

namespace POS_UI.ViewModels
{
    /// <summary>Floor plan + right sidebar for the Tables (Table View) page when floor layouts are enabled.</summary>
    public sealed class TablesFloorPlanLayoutViewModel : BaseViewModel, IDisposable
    {
        private readonly TablesViewModel _owner;
        private readonly ApiService _apiService;
        private bool _disposed;
        private bool _pickRebuildPosted;
        private TableModel? _subscribedTable;

        private TableModel? _selectedTableForDetails;
        private FloorPlanModel? _selectedFloorPlan;
        private TablesSidebarOrderRow? _standaloneOrderRow;

        /// <summary>Last table (API id) chosen per floor plan tab so switching tabs restores sidebar + tile selection.</summary>
        private readonly Dictionary<int, int> _lastSelectedTableApiIdByFloorPlanId = new();

        public ObservableCollection<FloorPlanModel> FloorPlans { get; } = new();
        public ObservableCollection<FloorPlanTablePickItem> PickItems { get; } = new();

        /// <summary>Non-table floor elements for display only (not tappable for table details).</summary>
        public ObservableCollection<FloorPlanTablePlacementModel> FloorPlanDecorPlacements { get; } = new();
        public ObservableCollection<TableModel> LiveTables => _owner.Tables;
        public ObservableCollection<TablesSidebarOrderRow> OrderRows { get; } = new();

        private bool _showSessionSummary;
        private decimal? _sessionSummaryTotal;
        private string? _sessionSummaryPaymentStatus;

        public bool ShowSessionSummary
        {
            get => _showSessionSummary;
            private set
            {
                if (_showSessionSummary == value) return;
                _showSessionSummary = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowSessionSummaryPaymentLine));
            }
        }

        public decimal? SessionSummaryTotal
        {
            get => _sessionSummaryTotal;
            private set
            {
                if (_sessionSummaryTotal == value) return;
                _sessionSummaryTotal = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowSessionSummaryAmount));
            }
        }

        /// <summary>Aggregated payment label for the session (from session-orders API or per-order lines).</summary>
        public string? SessionSummaryPaymentStatus
        {
            get => _sessionSummaryPaymentStatus;
            private set
            {
                if (_sessionSummaryPaymentStatus == value) return;
                _sessionSummaryPaymentStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowSessionSummaryPaymentLine));
            }
        }

        public bool ShowSessionSummaryAmount => SessionSummaryTotal.HasValue;

        public bool ShowSessionSummaryPaymentLine =>
            ShowSessionSummary && !string.IsNullOrWhiteSpace(SessionSummaryPaymentStatus);

        public bool HasOrderRows => OrderRows.Count > 0;

        public ICommand ViewSessionOrderCommand { get; }
        private readonly RelayCommand _viewPrimaryOrderCommand;

        public ICommand ViewPrimaryOrderCommand => _viewPrimaryOrderCommand;

        public TablesFloorPlanLayoutViewModel(TablesViewModel owner)
        {
            _owner = owner;
            _apiService = new ApiService();
            _selectTableFromItem = SelectTableFromPick;

            ViewSessionOrderCommand = new RelayCommand<TablesSidebarOrderRow>(OnViewSessionOrderRow);
            _viewPrimaryOrderCommand = new RelayCommand(OnViewPrimaryOrder, () => ShowSingleViewOrderButton);

            RefreshFromGlobalCache();

            ((INotifyCollectionChanged)LiveTables).CollectionChanged += OnLiveTablesCollectionChanged;
        }

        private readonly Action<TableModel> _selectTableFromItem;

        /// <summary>ListBox uses stable plan id so tabs stay correct when the canvas rebuilds.</summary>
        public int? SelectedFloorPlanId
        {
            get => _selectedFloorPlan?.Id;
            set
            {
                if (!value.HasValue) return;
                var plan = FloorPlans.FirstOrDefault(p => p.Id == value.Value);
                if (plan == null) return;
                SelectedFloorPlan = plan;
            }
        }

        public FloorPlanModel? SelectedFloorPlan
        {
            get => _selectedFloorPlan;
            set
            {
                if (ReferenceEquals(_selectedFloorPlan, value)) return;

                var outgoingPlan = _selectedFloorPlan;
                if (outgoingPlan != null && SelectedTableForDetails != null)
                {
                    _lastSelectedTableApiIdByFloorPlanId[outgoingPlan.Id] = SelectedTableForDetails.ApiId;
                }

                _selectedFloorPlan = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedFloorPlanId));

                RestoreSelectionForCurrentFloorPlan();

                RebuildPickItems();
                RecomputeCanvasSize();
            }
        }

        private void RestoreSelectionForCurrentFloorPlan()
        {
            if (_selectedFloorPlan == null)
            {
                SelectedTableForDetails = null;
                return;
            }

            if (!_lastSelectedTableApiIdByFloorPlanId.TryGetValue(_selectedFloorPlan.Id, out var tableApiId))
            {
                SelectedTableForDetails = null;
                return;
            }

            var live = LiveTables.FirstOrDefault(t => t.ApiId == tableApiId);
            if (live == null)
            {
                _lastSelectedTableApiIdByFloorPlanId.Remove(_selectedFloorPlan.Id);
                SelectedTableForDetails = null;
                return;
            }

            SelectedTableForDetails = live;
        }

        public TableModel? SelectedTableForDetails
        {
            get => _selectedTableForDetails;
            private set
            {
                if (ReferenceEquals(_selectedTableForDetails, value)) return;
                SubscribeSelectedTable(null);
                _selectedTableForDetails = value;
                SubscribeSelectedTable(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedTable));
                OnPropertyChanged(nameof(SelectedTableDisplayName));
                OnPropertyChanged(nameof(SelectedSessionIdLabel));
                OnPropertyChanged(nameof(ShowSessionId));
                OnPropertyChanged(nameof(SelectedStatusSwatchHex));
                OnPropertyChanged(nameof(SelectedCustomerName));
                _ = LoadSidebarForSelectedTableAsync();
            }
        }

        public bool HasSelectedTable => SelectedTableForDetails != null;

        public string SelectedTableDisplayName =>
            SelectedTableForDetails == null
                ? ""
                : (string.IsNullOrWhiteSpace(SelectedTableForDetails.Name) ? $"T{SelectedTableForDetails.ApiId}" : SelectedTableForDetails.Name);

        public string? SelectedSessionIdLabel
        {
            get
            {
                var sid = SelectedTableForDetails?.Order?.OrderSessionId;
                if (sid is int i && i > 0) return $"Session ID: {i}";
                return null;
            }
        }

        public bool ShowSessionId => SelectedTableForDetails?.Order?.OrderSessionId is int s && s > 0;

        public string SelectedStatusSwatchHex =>
            SelectedTableForDetails == null
                ? "#E0E0E0"
                : TableSelectionTableEnabledConverter.GetStatusIndicatorHex(SelectedTableForDetails, null);

        public string SelectedCustomerName => SelectedTableForDetails?.CustomerName ?? "";

        /// <summary>Non-session table order preview (same fields as <see cref="TablesSidebarOrderRow"/> for one XAML template).</summary>
        public TablesSidebarOrderRow? StandaloneOrderRow
        {
            get => _standaloneOrderRow;
            private set
            {
                if (ReferenceEquals(_standaloneOrderRow, value)) return;
                _standaloneOrderRow = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowStandaloneOrderPreview));
            }
        }

        public bool ShowStandaloneOrderPreview => StandaloneOrderRow != null;

        public bool ShowPerRowOrderViewButtons => OrderRows.Count > 1;

        /// <summary>One primary “View order” at the bottom: single session row or standalone table order.</summary>
        public bool ShowSingleViewOrderButton =>
            HasSelectedTable
            && (
                (HasOrderRows && OrderRows.Count == 1 && OrderRows[0].OrderApiId > 0)
                || (StandaloneOrderRow != null && StandaloneOrderRow.OrderApiId > 0));

        public double CanvasHostWidth { get; private set; } = 640;
        public double CanvasHostHeight { get; private set; } = 420;

        public double FloorPlanScrollMaxWidth => 1200;

        public void RefreshFromGlobalCache()
        {
            if (_disposed) return;

            var previousPlanId = _selectedFloorPlan?.Id;

            foreach (var p in PickItems.ToList())
            {
                p.Dispose();
            }

            PickItems.Clear();
            FloorPlans.Clear();

            var cached = GlobalDataService.Instance.CachedFloorPlans;
            if (cached == null || cached.Count == 0)
            {
                _lastSelectedTableApiIdByFloorPlanId.Clear();
                _selectedFloorPlan = null;
                OnPropertyChanged(nameof(SelectedFloorPlan));
                OnPropertyChanged(nameof(SelectedFloorPlanId));
                SelectedTableForDetails = null;
                ResetOrderSidebarState();
                RecomputeCanvasSize();
                return;
            }

            foreach (var fp in cached)
            {
                FloorPlans.Add(fp.Clone());
            }

            FloorPlanModel? next = null;
            if (previousPlanId.HasValue)
            {
                next = FloorPlans.FirstOrDefault(p => p.Id == previousPlanId.Value);
            }

            next ??= FloorPlans.FirstOrDefault();
            _selectedFloorPlan = next;
            OnPropertyChanged(nameof(SelectedFloorPlan));
            OnPropertyChanged(nameof(SelectedFloorPlanId));
            RestoreSelectionForCurrentFloorPlan();
            RebuildPickItems();
            RecomputeCanvasSize();
        }

        private void SubscribeSelectedTable(TableModel? t)
        {
            if (_subscribedTable != null)
            {
                _subscribedTable.PropertyChanged -= OnSelectedTablePropertyChanged;
                _subscribedTable = null;
            }

            _subscribedTable = t;
            if (_subscribedTable != null)
            {
                _subscribedTable.PropertyChanged += OnSelectedTablePropertyChanged;
            }
        }

        private void OnSelectedTablePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TableModel.Order))
            {
                _ = LoadSidebarForSelectedTableAsync();
                return;
            }

            if (e.PropertyName is nameof(TableModel.Status)
                or nameof(TableModel.Name)
                or nameof(TableModel.SessionTotalAmount)
                or nameof(TableModel.DisplayAmount))
            {
                OnPropertyChanged(nameof(SelectedStatusSwatchHex));
                OnPropertyChanged(nameof(SelectedCustomerName));
                OnPropertyChanged(nameof(SelectedSessionIdLabel));
                OnPropertyChanged(nameof(ShowSessionId));
                OnPropertyChanged(nameof(SelectedTableDisplayName));
            }
        }

        private void ResetOrderSidebarState()
        {
            OrderRows.Clear();
            StandaloneOrderRow = null;
            ShowSessionSummary = false;
            SessionSummaryTotal = null;
            SessionSummaryPaymentStatus = null;
            OnPropertyChanged(nameof(ShowSessionSummaryAmount));
            OnPropertyChanged(nameof(HasOrderRows));
            OnPropertyChanged(nameof(ShowPerRowOrderViewButtons));
            OnPropertyChanged(nameof(ShowSingleViewOrderButton));
            _viewPrimaryOrderCommand.RaiseCanExecuteChanged();
        }

        private async Task LoadSidebarForSelectedTableAsync()
        {
            ResetOrderSidebarState();

            var table = SelectedTableForDetails;
            if (table == null)
            {
                return;
            }

            var order = table.Order;
            if (order != null && order.ApiId > 0
                && (order.IsTableOrder != true || order.OrderSessionId is not int sid || sid <= 0))
            {
                StandaloneOrderRow = TablesSidebarOrderRow.FromOrder(order, table.CustomerName);
                OnPropertyChanged(nameof(HasOrderRows));
                OnPropertyChanged(nameof(ShowPerRowOrderViewButtons));
                OnPropertyChanged(nameof(ShowSingleViewOrderButton));
                _viewPrimaryOrderCommand.RaiseCanExecuteChanged();
                return;
            }

            if (order == null || order.IsTableOrder != true || order.OrderSessionId is not int sessionId || sessionId <= 0)
            {
                OnPropertyChanged(nameof(ShowPerRowOrderViewButtons));
                OnPropertyChanged(nameof(ShowSingleViewOrderButton));
                _viewPrimaryOrderCommand.RaiseCanExecuteChanged();
                return;
            }

            try
            {
                var response = await _apiService.GetSessionOrdersAsync(sessionId);
                var data = response?.Data;
                if (data?.OrderDetails == null || data.OrderDetails.Count == 0)
                {
                    OnPropertyChanged(nameof(ShowPerRowOrderViewButtons));
                    OnPropertyChanged(nameof(ShowSingleViewOrderButton));
                    _viewPrimaryOrderCommand.RaiseCanExecuteChanged();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(data.TotalAmount)
                    && decimal.TryParse(data.TotalAmount, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sessionTotal))
                {
                    ShowSessionSummary = true;
                    SessionSummaryTotal = sessionTotal;
                }

                var apiPaymentStatus = (data.PaymentStatus ?? "").Trim();
                SessionSummaryPaymentStatus = string.IsNullOrEmpty(apiPaymentStatus) ? null : apiPaymentStatus;

                var tableCustomer = SelectedTableForDetails?.CustomerName;
                foreach (var d in data.OrderDetails)
                {
                    OrderRows.Add(TablesSidebarOrderRow.FromSessionDetail(d, data, tableCustomer));
                }

                OnPropertyChanged(nameof(HasOrderRows));
                OnPropertyChanged(nameof(ShowPerRowOrderViewButtons));
            }
            catch
            {
                ResetOrderSidebarState();
            }

            OnPropertyChanged(nameof(ShowPerRowOrderViewButtons));
            OnPropertyChanged(nameof(ShowSingleViewOrderButton));
            _viewPrimaryOrderCommand.RaiseCanExecuteChanged();
        }

        private async void OnViewSessionOrderRow(TablesSidebarOrderRow? row)
        {
            if (row == null || row.OrderApiId <= 0) return;
            var dialogViewModel = new KitchenOrderDetailsDialogViewModel(row.OrderApiId, KitchenOrderDetailsDialogViewModel.DialogMode.Tables);
            var dialog = new KitchenOrderDetailsDialog { DataContext = dialogViewModel };
            await DialogHost.Show(dialog, "RootDialogHost");
            await LoadSidebarForSelectedTableAsync();
        }

        private async void OnViewPrimaryOrder()
        {
            var id = OrderRows.Count == 1
                ? OrderRows[0].OrderApiId
                : (StandaloneOrderRow?.OrderApiId ?? (SelectedTableForDetails?.Order?.ApiId ?? 0));
            if (id <= 0) return;
            var dialogViewModel = new KitchenOrderDetailsDialogViewModel(id, KitchenOrderDetailsDialogViewModel.DialogMode.Tables);
            var dialog = new KitchenOrderDetailsDialog { DataContext = dialogViewModel };
            await DialogHost.Show(dialog, "RootDialogHost");
            await LoadSidebarForSelectedTableAsync();
        }

        private void SelectTableFromPick(TableModel table)
        {
            var live = LiveTables.FirstOrDefault(t => t.ApiId == table.ApiId) ?? table;
            foreach (var p in PickItems)
            {
                p.IsSelected = p.Table.ApiId == live.ApiId;
            }

            SelectedTableForDetails = live;
            if (_selectedFloorPlan != null)
            {
                _lastSelectedTableApiIdByFloorPlanId[_selectedFloorPlan.Id] = live.ApiId;
            }
        }

        private void OnLiveTablesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_disposed) return;
            SchedulePickItemsRebuild();
        }

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

                PickItems.Add(new FloorPlanTablePickItem(live, placement, null, _selectTableFromItem, tablesPageBrowseMode: true));
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

        private void SyncPickSelectionToSelectedTable()
        {
            if (SelectedTableForDetails == null) return;
            foreach (var p in PickItems)
            {
                p.IsSelected = p.Table.ApiId == SelectedTableForDetails.ApiId;
            }
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

            OnPropertyChanged(nameof(CanvasHostWidth));
            OnPropertyChanged(nameof(CanvasHostHeight));
            OnPropertyChanged(nameof(FloorPlanDecorPlacements));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            SubscribeSelectedTable(null);
            _selectedTableForDetails = null;
            ((INotifyCollectionChanged)LiveTables).CollectionChanged -= OnLiveTablesCollectionChanged;
            foreach (var p in PickItems.ToList())
            {
                p.Dispose();
            }

            PickItems.Clear();
        }
    }
}
