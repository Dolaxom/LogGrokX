using System.Windows;

namespace LogGrokX
{
    public partial class SupportWindow
    {
        public SupportWindow()
        {
            DataContext = new SupportViewModel();
            InitializeComponent();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
