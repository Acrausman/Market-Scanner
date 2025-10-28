using System.Windows;
using System.Runtime.InteropServices;

namespace MarketScanner.UI.Views
{
    public partial class MainWindow : Window
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        public MainWindow()
        {
            InitializeComponent();
            AllocConsole();
        }
    }
}
