using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MarketScanner.UI.Wpf.ViewModels
{
    public class SymbolViewModel : INotifyPropertyChanged
    {
        public SymbolViewModel() { }

        public SymbolViewModel(string symbol)
        {
            Symbol = symbol;
        }

        private string _symbol;
        public string Symbol
        {
            get => _symbol;
            set { _symbol = value;OnPropertyChanged(); }
        }

        private double _price;
        public double Price
        {
            get => _price;
            set {_price = value; OnPropertyChanged(); }
        }

        private double _rsi;
        public double RSI
        {
            get => _rsi;
            set { _rsi = value; OnPropertyChanged(); }
        }

        private double _sma;
        public double SMA
        {
            get => _sma;
            set { _sma = value; OnPropertyChanged(); }
        }

        private double _volume;
        public double Volume
        {
            get => _volume;
            set { _volume = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // small helper to reduce boilerplate
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }
}
