using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using POS_UI.Models;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using POS_UI.Services;
using System.Windows;
using System.Threading.Tasks;
using POS_UI.ViewModels;

namespace POS_UI.ViewModels
{
    public class TablesViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private bool _isLoading;
        
        public string CurrentPage { get; set; }
        public ObservableCollection<TableModel> Tables { get; set; }
        public ICommand TableButtonClickCommand { get; }
        public ICommand RefreshTablesCommand { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
                NotifyTableViewLayoutProperties();
            }
        }

        private TablesFloorPlanLayoutViewModel? _floorPlanLayout;

        public TablesFloorPlanLayoutViewModel? FloorPlanLayout => _floorPlanLayout;

        public bool UseFloorPlanLayout => _floorPlanLayout != null;

        public bool ShowDefaultTableGrid => !IsLoading && !UseFloorPlanLayout;

        public bool ShowFloorPlanLayout => !IsLoading && UseFloorPlanLayout;

        private void NotifyTableViewLayoutProperties()
        {
            OnPropertyChanged(nameof(UseFloorPlanLayout));
            OnPropertyChanged(nameof(FloorPlanLayout));
            OnPropertyChanged(nameof(ShowDefaultTableGrid));
            OnPropertyChanged(nameof(ShowFloorPlanLayout));
        }

        /// <summary>Rebuild floor plan UI from <see cref="GlobalDataService"/> (after tables load or when returning to this page).</summary>
        public void RefreshFloorPlanLayoutState()
        {
            if (GlobalDataService.Instance.IsFloorPlanLayoutEnabled
                && GlobalDataService.Instance.CachedFloorPlans is { Count: > 0 })
            {
                if (_floorPlanLayout == null)
                {
                    _floorPlanLayout = new TablesFloorPlanLayoutViewModel(this);
                }
                else
                {
                    _floorPlanLayout.RefreshFromGlobalCache();
                }
            }
            else
            {
                _floorPlanLayout?.Dispose();
                _floorPlanLayout = null;
            }

            NotifyTableViewLayoutProperties();
        }

        /// <summary>Initial API load started from constructor (non-blocking for callers).</summary>
        public Task InitialLoadTask { get; private set; } = Task.CompletedTask;

        /// <summary>Required for WPF XAML (<c>&lt;vm:TablesViewModel/&gt;</c>); optional-parameter-only ctor is not a parameterless ctor for XAML activation.</summary>
        public TablesViewModel() : this(true)
        {
        }

        /// <param name="subscribeToGlobalEvents">When false (short-lived Cashier table picker), do not hook global refresh events to avoid duplicate handlers.</param>
        public TablesViewModel(bool subscribeToGlobalEvents)
        {
            CurrentPage = "Tables";
            _apiService = new ApiService();
            Tables = new ObservableCollection<TableModel>();
            TableButtonClickCommand = new RelayCommand<TableModel>(OnTableButtonClicked);
            RefreshTablesCommand = new RelayCommand(async () => await LoadTablesAsync());

            if (subscribeToGlobalEvents)
            {
                GlobalDataService.Instance.OrderStatusChanged += OnOrderStatusChanged;
                GlobalDataService.Instance.TablesRefreshRequested += OnTablesRefreshRequested;
            }

            InitialLoadTask = LoadTablesAsync();
        }

        private async void OnOrderStatusChanged(int orderId, string newStatus)
        {
            try
            {
                // Refresh tables when an order is cancelled
                if (newStatus == "CANCELLED")
                {
                    await LoadTablesAsync();
                }
            }
            catch (System.Exception ex)
            {
                // Silently handle errors to avoid disrupting the UI
                System.Diagnostics.Debug.WriteLine($"Error refreshing tables after order status change: {ex.Message}");
            }
        }

        private async void OnTablesRefreshRequested()
        {
            try
            {
                await LoadTablesAsync();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing tables: {ex.Message}");
            }
        }

        private async Task LoadTablesAsync()
        {
            try
            {
                IsLoading = true;
                var tables = await _apiService.GetTablesAsync();
                
                Tables.Clear();
                foreach (var table in tables)
                {
                    Tables.Add(table);
                }

                // Load session totals for table orders with a session (so table button shows session total)
                var sessionIds = tables
                    .Where(t => t.Order?.IsTableOrder == true && t.Order.OrderSessionId.HasValue && t.Order.OrderSessionId.Value > 0)
                    .Select(t => t.Order.OrderSessionId.Value)
                    .Distinct()
                    .ToList();
                foreach (var sessionId in sessionIds)
                {
                    try
                    {
                        var response = await _apiService.GetSessionOrdersAsync(sessionId);
                        if (response?.Data == null) continue;
                        decimal? total = null;
                        if (!string.IsNullOrWhiteSpace(response.Data.TotalAmount) && decimal.TryParse(response.Data.TotalAmount, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                            total = parsed;
                        if (!total.HasValue) continue;
                        foreach (var table in tables)
                        {
                            if (table.Order?.OrderSessionId == sessionId)
                            {
                                table.SessionTotalAmount = total;
                            }
                        }
                    }
                    catch
                    {
                        // Non-fatal: table will show order amount instead
                    }
                }
            }
            catch (System.Exception ex)
            {
                //MessageBox.Show($"Error loading tables: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                RefreshFloorPlanLayoutState();
            }
        }

        private async void OnTableButtonClicked(TableModel table)
        {
            try
            {
                if ((table.Status == TableStatus.Served || table.Status == TableStatus.Reserved) && table.Order != null)
                {
                    if (table.Order.IsTableOrder && table.Order.OrderSessionId.HasValue && table.Order.OrderSessionId.Value > 0)
                    {
                        var tableName = table.Name;
                        var tableID = table.ApiId;
                        var sessionId = table.Order?.OrderSessionId ?? 0;

                        var response = await _apiService.GetSessionOrdersAsync(sessionId);
                        var TBLDialogViewModel = TableOrderListDialogViewModel.CreateFromSessionOrders(tableName, tableID, response, sessionId);
                        Func<Task> reopenTableOrderList = null;
                        reopenTableOrderList = async () =>
                        {
                            var responseAgain = await _apiService.GetSessionOrdersAsync(sessionId);
                            var vmAgain = TableOrderListDialogViewModel.CreateFromSessionOrders(tableName, tableID, responseAgain, sessionId);
                            vmAgain.SetReopenTableOrderListCallback(reopenTableOrderList);
                            var dlgAgain = new POS_UI.View.TableOrderListDialog { DataContext = vmAgain };
                            await DialogHost.Show(dlgAgain, "RootDialogHost");
                        };
                        TBLDialogViewModel.SetReopenTableOrderListCallback(reopenTableOrderList);

                        var TBLDialog = new POS_UI.View.TableOrderListDialog { DataContext = TBLDialogViewModel };
                        await DialogHost.Show(TBLDialog, "RootDialogHost");

                        return;
                    }
                    // Use the order ID to fetch fresh order details from the API and show KitchenOrderDetailsDialog in Tables mode
                    var orderId = table.Order.ApiId;
                    var dialogViewModel = new KitchenOrderDetailsDialogViewModel(orderId, KitchenOrderDetailsDialogViewModel.DialogMode.Tables);
                    var dialog = new POS_UI.View.KitchenOrderDetailsDialog { DataContext = dialogViewModel };
                    
                    await DialogHost.Show(dialog, "RootDialogHost");
                }
                else if (table.Status == TableStatus.Available)
                {
                    // Handle available table - could open table selection or create new order
                    //MessageBox.Show($"Table {table.Name} is available. You can create a new order for this table.", "Table Available", MessageBoxButton.OK, MessageBoxImage.Information);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateInfo("Table Available", $"Table {table.Name} is available. You can create a new order for this table.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialogHost");
                }
                else if (table.Status == TableStatus.Reserved && table.Order == null)
                {
                    // Reserved table without order - show information
                    //MessageBox.Show($"Table {table.Name} is reserved but has no active order.", "Table Reserved", MessageBoxButton.OK, MessageBoxImage.Information);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateInfo("Table Reserved", $"Table {table.Name} is reserved but has no active order.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialogHost");
                }
                else if (table.Status == TableStatus.Drafted)
                {
                    // Handle drafted table
                    //MessageBox.Show($"Table {table.Name} has a draft order. You can continue editing the draft.", "Draft Order", MessageBoxButton.OK, MessageBoxImage.Information);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateInfo("Draft Order", $"Table {table.Name} has a draft order. You can continue editing the draft.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialogHost");
                }
                else
                {
                    //MessageBox.Show($"Table {table.Name} is {table.Status}. No action available.", "Table Status", MessageBoxButton.OK, MessageBoxImage.Information);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateInfo("Table Status", $"Table {table.Name} is {table.Status}. No action available.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialogHost");
                }
            }
            catch (System.Exception ex)
            {
                //System.Windows.MessageBox.Show($"Error showing order details: {ex.Message}\n{ex.StackTrace}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Showing Order Details", $"Error showing order details: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialogHost");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
} 