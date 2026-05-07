using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace POS_UI.Models
{
    public class FloorPlanModel : INotifyPropertyChanged
    {
        public int Id { get; set; }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<FloorPlanTablePlacementModel> _tables;

        public FloorPlanModel()
        {
            _tables = new ObservableCollection<FloorPlanTablePlacementModel>();
            AttachTablesCollection(_tables);
        }

        /// <summary>
        /// Placed tables for this floor plan. Replacing the collection raises <see cref="TableCount"/> updates for list bindings.
        /// </summary>
        public ObservableCollection<FloorPlanTablePlacementModel> Tables
        {
            get => _tables;
            set
            {
                if (ReferenceEquals(_tables, value))
                {
                    return;
                }

                DetachTablesCollection(_tables);
                _tables = value ?? new ObservableCollection<FloorPlanTablePlacementModel>();
                AttachTablesCollection(_tables);
                OnPropertyChanged();
                OnPropertyChanged(nameof(TableCount));
            }
        }

        /// <summary>Count of placed <strong>tables</strong> only (excludes custom floor items in the same collection).</summary>
        public int TableCount => _tables?.Count(t => t.Kind == FloorPlanElementKind.Table) ?? 0;

        private void AttachTablesCollection(ObservableCollection<FloorPlanTablePlacementModel> col)
        {
            col.CollectionChanged += OnTablesCollectionChanged;
        }

        private void DetachTablesCollection(ObservableCollection<FloorPlanTablePlacementModel> col)
        {
            col.CollectionChanged -= OnTablesCollectionChanged;
        }

        private void OnTablesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(TableCount));
        }

        public FloorPlanModel Clone()
        {
            var clone = new FloorPlanModel
            {
                Id = Id,
                Name = Name
            };
            clone.Tables = new ObservableCollection<FloorPlanTablePlacementModel>(Tables.Select(t => t.Clone()));
            return clone;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
