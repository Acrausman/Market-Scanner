using System.ComponentModel;

namespace MarketScanner.UI.Wpf.ViewModels
{
    public class SymbolViewModel : INotifyPropertyChanged
    {
        public SymbolViewModel(string symbol)
        {
            Symbol = symbol;
        }

        public SymbolViewModel() { } // keep default constructor if needed

        private string symbol;
        public string Symbol
        {
            get => symbol;
            set { symbol = value; OnPropertyChanged(nameof(Symbol)); }
        }

        private double price;
        public double Price
        {
            get => price;
            set { price = value; OnPropertyChanged(nameof(Price)); }
        }

        private double rsi;
        public double RSI
        {
            get => rsi;
            set { rsi = value; OnPropertyChanged(nameof(RSI)); }
        }

        private double sma;
        public double SMA
        {
            get => sma;
            set { sma = value; OnPropertyChanged(nameof(SMA)); }
        }

        private double volume;
        public double Volume
        {
            get => volume;
            set { volume = value; OnPropertyChanged(nameof(Volume)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}
