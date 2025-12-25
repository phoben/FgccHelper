using System;
using System.Windows;
using FgccHelper.Models;

namespace FgccHelper
{
    /// <summary>
    /// UpdateWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UpdateWindow : Window
    {
        private readonly VersionInfo _versionInfo;
        private readonly string _currentVersion;

        /// <summary>
        /// 用户接受更新事件
        /// </summary>
        public event EventHandler UpdateAccepted;

        /// <summary>
        /// 用户拒绝更新事件
        /// </summary>
        public event EventHandler UpdateDeclined;

        /// <summary>
        /// 用户跳过此版本事件
        /// </summary>
        public event EventHandler VersionSkipped;

        public UpdateWindow(VersionInfo versionInfo, string currentVersion)
        {
            InitializeComponent();
            
            _versionInfo = versionInfo ?? throw new ArgumentNullException(nameof(versionInfo));
            _currentVersion = currentVersion ?? "1.0.0";
            
            LoadVersionInfo();
        }

        /// <summary>
        /// 加载版本信息
        /// </summary>
        private void LoadVersionInfo()
        {
            CurrentVersionText.Text = $"当前版本: {_currentVersion}";
            NewVersionText.Text = $"新版本: {_versionInfo.Version}";
            FileSizeText.Text = $"文件大小: {_versionInfo.GetFormattedFileSize()}";
            ReleaseDateText.Text = $"发布日期: {_versionInfo.ReleaseDate:yyyy年MM月dd日}";
            
            // 显示更新说明
            if (_versionInfo.ReleaseNotes != null && _versionInfo.ReleaseNotes.Count > 0)
            {
                ReleaseNotesText.Text = string.Join("\n", _versionInfo.ReleaseNotes);
            }
            else
            {
                ReleaseNotesText.Text = "暂无更新说明";
            }

            // 如果是强制更新，隐藏跳过按钮
            if (_versionInfo.ForceUpdate)
            {
                SkipButton.Visibility = Visibility.Collapsed;
                CancelButton.Content = "以后再说";
            }
            else
            {
                SkipButton.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 立即更新按钮点击事件
        /// </summary>
        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateAccepted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 稍后提醒按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateDeclined?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 跳过此版本按钮点击事件
        /// </summary>
        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                $"确定要跳过版本 {_versionInfo.Version} 吗？此版本将不再提示更新。",
                "跳过版本",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                VersionSkipped?.Invoke(this, EventArgs.Empty);
                Close();
            }
        }

        /// <summary>
        /// 窗口加载事件
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 设置窗口为前台显示
            Topmost = true;
            Activate();
        }
    }
}