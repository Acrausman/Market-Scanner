using System.Windows;
using MarketScanner.UI.Wpf.ViewModels;

namespace MarketScanner.UI.Views
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
