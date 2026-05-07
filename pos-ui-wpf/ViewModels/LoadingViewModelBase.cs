using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace POS_UI.ViewModels
{
    public class LoadingViewModelBase : INotifyPropertyChanged
    {
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public async Task SetLoadingAsync(Func<Task> asyncAction)
        {
            IsLoading = true;
            try
            {
                await asyncAction();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 