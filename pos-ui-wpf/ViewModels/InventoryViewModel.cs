using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using POS_UI.Models;
using System;

namespace POS_UI.ViewModels
{
    public class InventoryViewModel : LoadingViewModelBase
    {
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    // Optionally filter InventoryItems here
                }
            }
        }
        public string CurrentPage { get; set; }

        public ObservableCollection<InventoryItemModel> InventoryItems { get; set; }

        public InventoryViewModel()
        {
            CurrentPage = "Inventory";
            // Sample data
            InventoryItems = new ObservableCollection<InventoryItemModel>
            {
                new InventoryItemModel { Name = "White Rice", Quantity = 11, Unit = "KG", LastUpdated = DateTime.Now.AddHours(-17) },
                new InventoryItemModel { Name = "Oil", Quantity = 11, Unit = "Liters", LastUpdated = DateTime.Now.AddHours(-17) },
                new InventoryItemModel { Name = "White Rice", Quantity = 11, Unit = "KG", LastUpdated = DateTime.Now.AddHours(-17) },
                new InventoryItemModel { Name = "White Rice", Quantity = 11, Unit = "KG", LastUpdated = DateTime.Now.AddHours(-17) },
                new InventoryItemModel { Name = "White Rice", Quantity = 11, Unit = "KG", LastUpdated = DateTime.Now.AddHours(-17) },
                new InventoryItemModel { Name = "White Rice", Quantity = 11, Unit = "KG", LastUpdated = DateTime.Now.AddHours(-17) },
                new InventoryItemModel { Name = "White Rice", Quantity = 11, Unit = "KG", LastUpdated = DateTime.Now.AddHours(-17) },
            };
        }


    }
} 