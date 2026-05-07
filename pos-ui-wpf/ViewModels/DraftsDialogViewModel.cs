using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using POS_UI.Models;
using POS_UI.Services;
using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Linq;
using System.Windows.Threading;
using System.Collections.Specialized;
using System.ComponentModel;

namespace POS_UI.ViewModels
{
    public class DraftsDialogViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<DraftOrderModel> DraftOrders { get; }
        public ObservableCollection<DraftOrderModel> TicketsDrafts { get; }
        public ObservableCollection<DraftOrderModel> TablesDrafts { get; }
        public ICommand LoadDraftCommand { get; }
        public ICommand DeleteDraftCommand { get; }

        private DispatcherTimer _timer;
        private readonly DraftStorageService _draftStorageService = new DraftStorageService();

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool HasDrafts => DraftOrders.Count > 0;
        public bool HasTicketsDrafts => TicketsDrafts.Count > 0;
        public bool HasTablesDrafts => TablesDrafts.Count > 0;

        public DraftsDialogViewModel(ObservableCollection<DraftOrderModel> draftOrders)
        {
            try
            {
                DraftOrders = draftOrders;
                TicketsDrafts = new ObservableCollection<DraftOrderModel>();
                TablesDrafts = new ObservableCollection<DraftOrderModel>();
                
                // Initialize the filtered collections
                RefreshFilteredCollections();
                
                // Subscribe to collection changes to keep filtered collections in sync
                DraftOrders.CollectionChanged += DraftOrders_CollectionChanged;
                
                LoadDraftCommand = new RelayCommand<DraftOrderModel>(LoadDraft);
                DeleteDraftCommand = new RelayCommand<DraftOrderModel>(DeleteDraft);

                // Start timer to update elapsed time
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromMinutes(1);
                _timer.Tick += (s, e) =>
                {
                    foreach (var draft in DraftOrders)
                        draft.OnElapsedTimeChanged();
                };
                _timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error in DraftsDialogViewModel: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DraftOrders_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            try
            {
                Console.WriteLine($"DraftOrders collection changed. Action: {e.Action}, Count: {DraftOrders.Count}");
                
                // Refresh filtered collections when the main collection changes
                RefreshFilteredCollections();
                
                Console.WriteLine($"After refresh - TicketsDrafts: {TicketsDrafts.Count}, TablesDrafts: {TablesDrafts.Count}");
                
                // Notify property changes
                OnPropertyChanged(nameof(HasDrafts));
                OnPropertyChanged(nameof(HasTicketsDrafts));
                OnPropertyChanged(nameof(HasTablesDrafts));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DraftOrders_CollectionChanged: {ex.Message}");
            }
        }

        private void RefreshFilteredCollections()
        {
            try
            {
                Console.WriteLine($"RefreshFilteredCollections called. DraftOrders count: {DraftOrders.Count}");
                
                // Clear existing items
                TicketsDrafts.Clear();
                TablesDrafts.Clear();
                
                Console.WriteLine("Cleared filtered collections");
                
                // Re-populate based on current state
                foreach (var draft in DraftOrders)
                {
                    Console.WriteLine($"Processing draft: {draft.CustomerName} - {draft.OrderType}");
                    
                    if (draft.OrderType == "Dine In")
                    {
                        TablesDrafts.Add(draft);
                        Console.WriteLine($"Added to TablesDrafts: {draft.CustomerName}");
                    }
                    else
                    {
                        TicketsDrafts.Add(draft);
                        Console.WriteLine($"Added to TicketsDrafts: {draft.CustomerName}");
                    }
                }
                
                Console.WriteLine($"Final counts - TicketsDrafts: {TicketsDrafts.Count}, TablesDrafts: {TablesDrafts.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in RefreshFilteredCollections: {ex.Message}");
            }
        }

        private void LoadDraft(DraftOrderModel draft)
        {
            try
            {
                Console.WriteLine($"LoadDraft called for draft: {draft.CustomerName} - {draft.OrderType}");
                
                // Close the dialog and return the selected draft
                DialogHost.CloseDialogCommand.Execute(draft, null);
                
                Console.WriteLine($"DialogHost.CloseDialogCommand executed for draft: {draft.CustomerName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LoadDraft: {ex.Message}");
                MessageBox.Show($"Error loading draft: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteDraft(DraftOrderModel draft)
        {
            try
            {
                Console.WriteLine($"DeleteDraft called for: {draft.CustomerName} - {draft.OrderType}");
                
                // Remove from the main collection - the CollectionChanged event will handle updating filtered collections
                DraftOrders.Remove(draft);
                
                // Additional safety check to ensure the item is removed from filtered collections
                if (draft.OrderType == "Dine In")
                {
                    TablesDrafts.Remove(draft);
                }
                else
                {
                    TicketsDrafts.Remove(draft);
                }
                
                // Save updated drafts to file
                _draftStorageService.SaveDrafts(DraftOrders);
                
                Console.WriteLine($"After deletion - DraftOrders: {DraftOrders.Count}, TicketsDrafts: {TicketsDrafts.Count}, TablesDrafts: {TablesDrafts.Count}");
                
                // If no drafts remain, close the dialog
                if (DraftOrders.Count == 0)
                {
                    Console.WriteLine("No drafts remaining, closing dialog");
                    DialogHost.CloseDialogCommand.Execute(null, null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting draft: {ex.Message}");
                MessageBox.Show($"Error deleting draft: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 