using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using MarketScanner.UI.Wpf.ViewModels;
using System.Diagnostics;

namespace MarketScanner.UI.Views
{
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Critical;    }
}

}
