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
            
            // 动态设置版本号
            InfoTextBlock.Text = $"软件名称：活字格工程评价小工具\n版本号：v{FgccHelper.Services.UpdateManager.GetCurrentApplicationVersion()}\n作者：超哥\n邮箱：phoben@qq.com\n微信：13972707111";
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