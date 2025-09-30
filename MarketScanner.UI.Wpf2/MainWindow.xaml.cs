using System.Windows;
using MarketScanner.UI.ViewModels;

namespace MarketScanner.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
