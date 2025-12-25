using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using FgccHelper.Models;

namespace FgccHelper
{
    /// <summary>
    /// DownloadProgressWindow.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadProgressWindow : Window
    {
        private CancellationTokenSource _cancellationTokenSource;

        public DownloadProgressWindow()
        {
            InitializeComponent();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // 设置窗口加载事件
            Loaded += DownloadProgressWindow_Loaded;
        }

        /// <summary>
        /// 更新下载进度
        /// </summary>
        public void UpdateProgress(DownloadProgressInfo progressInfo)
        {
            Dispatcher.Invoke(() =>
            {
                DownloadProgressBar.Value = progressInfo.ProgressPercentage;
                ProgressText.Text = $"{progressInfo.ProgressPercentage}%";
                
                // 更新文件大小信息
                var downloadedSize = FormatFileSize(progressInfo.BytesDownloaded);
                var totalSize = FormatFileSize(progressInfo.TotalBytes);
                FileSizeText.Text = $"{downloadedSize} / {totalSize}";
                
                // 更新下载速度
                var speed = FormatFileSize(progressInfo.SpeedBytesPerSecond);
                SpeedText.Text = $"{speed}/s";
                
                // 更新剩余时间
                if (progressInfo.RemainingSeconds > 0)
                {
                    var remainingTime = FormatTimeSpan(progressInfo.RemainingSeconds);
                    TimeRemainingText.Text = $"剩余时间: {remainingTime}";
                }
                else
                {
                    TimeRemainingText.Text = "剩余时间: 计算中...";
                }

                // 更新状态文本
                if (progressInfo.ProgressPercentage < 100)
                {
                    StatusText.Text = $"正在下载... {progressInfo.ProgressPercentage}%";
                }
                else
                {
                    StatusText.Text = "下载完成，正在准备安装...";
                    CancelButton.IsEnabled = false;
                }
            });
        }

        /// <summary>
        /// 更新状态文本
        /// </summary>
        public void UpdateStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = status;
            });
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 格式化时间跨度
        /// </summary>
        private string FormatTimeSpan(int seconds)
        {
            if (seconds < 60)
                return $"{seconds}秒";
            
            var minutes = seconds / 60;
            var remainingSeconds = seconds % 60;
            
            if (minutes < 60)
                return remainingSeconds > 0 ? $"{minutes}分{remainingSeconds}秒" : $"{minutes}分钟";
            
            var hours = minutes / 60;
            var remainingMinutes = minutes % 60;
            
            return remainingMinutes > 0 ? $"{hours}小时{remainingMinutes}分钟" : $"{hours}小时";
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "确定要取消下载吗？",
                "确认取消",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _cancellationTokenSource?.Cancel();
                DialogResult = false;
                Close();
            }
        }

        /// <summary>
        /// 窗口加载事件
        /// </summary>
        private void DownloadProgressWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 设置窗口为前台显示
            Topmost = true;
            Activate();
            
            // 确保进度条从0开始
            DownloadProgressBar.Value = 0;
            StatusText.Text = "正在连接到服务器...";
        }

        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            
            // 取消下载
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
            
            _cancellationTokenSource?.Dispose();
        }

        /// <summary>
        /// 获取取消令牌
        /// </summary>
        public CancellationToken GetCancellationToken()
        {
            return _cancellationTokenSource.Token;
        }
    }
}