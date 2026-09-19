using System.Windows;

namespace LogGrokX.Settings
{
    public partial class SettingsWindow
    {
        public SettingsWindow(SettingsViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel viewModel && viewModel.Save())
                Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}