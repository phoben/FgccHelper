using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FgccHelper.Models
{
    public class StatisticItem : INotifyPropertyChanged
    {
        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private int _count;
        public int Count
        {
            get => _count;
            set { _count = value; OnPropertyChanged(); }
        }

        private string _description;
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        private List<DetailEntry> _details;
        public List<DetailEntry> Details // Details 列表本身的更改通常通过 ObservableCollection 或手动刷新UI
        {
            get => _details;
            set { _details = value; OnPropertyChanged(); }
        }

        public StatisticItem()
        {
            Details = new List<DetailEntry>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 