using System.Diagnostics;
using System.Windows;

namespace FgccHelper
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
        }

        private void GitRepoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/phoben/FgccHelper.git",
                    UseShellExecute = true // Important for opening URLs in the default browser
                });
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("无法打开链接: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 