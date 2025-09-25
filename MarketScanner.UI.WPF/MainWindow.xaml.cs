using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MarketScanner.Core;


namespace MarketScanner.UI.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Engine _engine = new();
        public MainWindow()
        {
            InitializeComponent();
            _engine.TriggerHit += OnTriggerHit;
            _engine.start();
        }

        private void OnTriggerHit(object? sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                AlertsList.Items.Add(message);
            });
        }
    }
}