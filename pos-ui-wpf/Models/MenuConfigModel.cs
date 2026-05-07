using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace POS_UI.Models
{
    /// <summary>
    /// Represents the complete menu configuration for a POS terminal
    /// </summary>
    public class MenuConfigModel : INotifyPropertyChanged
    {
        private int _brandId;
        private int _outletId;
        private string _terminalId;
        private List<MenuTabModel> _tabs;

        public int BrandId
        {
            get => _brandId;
            set { _brandId = value; OnPropertyChanged(); }
        }

        public int OutletId
        {
            get => _outletId;
            set { _outletId = value; OnPropertyChanged(); }
        }

        public string TerminalId
        {
            get => _terminalId;
            set { _terminalId = value; OnPropertyChanged(); }
        }

        public List<MenuTabModel> Tabs
        {
            get => _tabs ?? new List<MenuTabModel>();
            set { _tabs = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets the default tab (or creates one if missing)
        /// </summary>
        public MenuTabModel GetOrCreateDefaultTab()
        {
            var defaultTab = Tabs.FirstOrDefault(t => t.IsDefault);
            if (defaultTab == null)
            {
                defaultTab = new MenuTabModel
                {
                    Id = 1,
                    Name = "All Items",
                    Order = 1,
                    IsDefault = true,
                    ContentType = "categories",
                    CategoryIds = new List<int>(), // Empty = all categories
                    ItemIds = new List<int>()
                };
                Tabs.Add(defaultTab);
            }
            return defaultTab;
        }

        /// <summary>
        /// Validates the menu configuration
        /// </summary>
        public bool IsValid(out string errorMessage)
        {
            errorMessage = null;

            // Must have at least one tab (the default)
            if (Tabs == null || Tabs.Count == 0)
            {
                errorMessage = "Menu must have at least one tab";
                return false;
            }

            // Must have exactly one default tab
            var defaultCount = Tabs.Count(t => t.IsDefault);
            if (defaultCount == 0)
            {
                errorMessage = "Menu must have a default tab";
                return false;
            }
            if (defaultCount > 1)
            {
                errorMessage = "Menu can only have one default tab";
                return false;
            }

            // Cannot have more than 5 tabs
            if (Tabs.Count > 5)
            {
                errorMessage = "Menu cannot have more than 5 tabs";
                return false;
            }

            // Each tab must have a name
            if (Tabs.Any(t => string.IsNullOrWhiteSpace(t.Name)))
            {
                errorMessage = "All tabs must have a name";
                return false;
            }

            // Each tab must have a valid content type
            foreach (var tab in Tabs)
            {
                if (tab.ContentType != "categories" && tab.ContentType != "items" && tab.ContentType != "mixed")
                {
                    errorMessage = $"Tab '{tab.Name}' has invalid content type '{tab.ContentType}'";
                    return false;
                }
            }

            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
