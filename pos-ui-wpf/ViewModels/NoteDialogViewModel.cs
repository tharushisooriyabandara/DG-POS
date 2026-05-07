using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace POS_UI.ViewModels
{
    public class NoteDialogViewModel : INotifyPropertyChanged
    {
        private string _note;
        public string Note
        {
            get => _note;
            set
            {
                _note = value;
                OnPropertyChanged();
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<string> NoteSaved;
        public event Action DialogClosed;

        public NoteDialogViewModel(string existingNote = null)
        {
            Note = existingNote;
            SaveCommand = new CashierRelayCommand(Save);
            CancelCommand = new CashierRelayCommand(Cancel);
        }

        private void Save()
        {
            NoteSaved?.Invoke(Note);
            DialogClosed?.Invoke();
        }

        private void Cancel()
        {
            DialogClosed?.Invoke();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
} 