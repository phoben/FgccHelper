using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Timers;
using FgccHelper.Models;
using FgccHelper.Services;

namespace FgccHelper
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private UpdateManager _updateManager;
        private Timer _updateCheckTimer;
        private UpdateConfig _updateConfig;

        /// <summary>
        /// 应用程序启动事件
        /// </summary>
        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);
                
                // 初始化更新配置
                _updateConfig = UpdateManager.LoadUpdateConfig();
                
                // 检查是否有更新参数传入（用于更新后的重启）
                if (e.Args.Length > 0 && e.Args[0] == "--updated")
                {
                    // 清理更新相关文件
                    CleanupAfterUpdate();
                    
                    // 显示更新成功消息
                    MessageBox.Show(
                        "软件更新成功！",
                        "更新完成",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                
                // 启动后台更新检查
                if (_updateConfig.AutoCheckUpdate)
                {
                    await InitializeUpdateChecker();
                }
                
                Debug.WriteLine("应用程序启动完成，更新检查已初始化");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"应用程序启动失败: {ex.Message}");
                // 即使更新系统初始化失败，也要继续启动主应用
                base.OnStartup(e);
            }
        }

        /// <summary>
        /// 初始化更新检查器
        /// </summary>
        private async Task InitializeUpdateChecker()
        {
            try
            {
                // 创建更新管理器
                _updateManager = new UpdateManager(_updateConfig);
                
                // 延迟启动更新检查，避免影响应用启动速度
                await Task.Delay(3000);
                
                // 执行静默更新检查
                await Task.Run(async () =>
                {
                    try
                    {
                        var result = await _updateManager.CheckAndUpdateAsync(silentMode: true);
                        Debug.WriteLine($"静默更新检查结果: {result}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"静默更新检查失败: {ex.Message}");
                    }
                });
                
                // 设置定时器进行定期检查
                if (_updateConfig.CheckIntervalHours > 0)
                {
                    _updateCheckTimer = new Timer(TimeSpan.FromHours(_updateConfig.CheckIntervalHours).TotalMilliseconds);
                    _updateCheckTimer.Elapsed += async (sender, e) =>
                    {
                        try
                        {
                            if (_updateManager != null)
                            {
                                await _updateManager.CheckAndUpdateAsync(silentMode: true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"定时更新检查失败: {ex.Message}");
                        }
                    };
                    _updateCheckTimer.Start();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化更新检查器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用程序退出事件
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // 清理资源
                _updateCheckTimer?.Stop();
                _updateCheckTimer?.Dispose();
                _updateManager?.Dispose();
                
                // 清理临时下载文件
                UpdateService.CleanTempDownloads();
                
                base.OnExit(e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"应用程序退出时发生错误: {ex.Message}");
                base.OnExit(e);
            }
        }

        /// <summary>
        /// 更新后清理
        /// </summary>
        private void CleanupAfterUpdate()
        {
            try
            {
                // 清理备份文件（保留最新的一个备份）
                var appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var parentDirectory = Directory.GetParent(appDirectory)?.FullName;
                
                if (!string.IsNullOrEmpty(parentDirectory))
                {
                    var backupDirectories = Directory.GetDirectories(parentDirectory, "*.backup.*")
                        .OrderByDescending(d => d)
                        .Skip(1); // 保留最新的备份
                    
                    foreach (var backupDir in backupDirectories)
                    {
                        try
                        {
                            Directory.Delete(backupDir, true);
                            Debug.WriteLine($"已清理备份目录: {backupDir}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"清理备份目录失败 {backupDir}: {ex.Message}");
                        }
                    }
                }
                
                // 清理临时更新文件
                UpdateService.CleanTempDownloads();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新后清理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 手动触发更新检查（供UI调用）
        /// </summary>
        public static async Task CheckForUpdatesAsync(bool silentMode = false)
        {
            try
            {
                var config = UpdateManager.LoadUpdateConfig();
                // 配置已硬编码，无需检查是否为空
                
                using (var updateManager = new UpdateManager(config))
                {
                    await updateManager.CheckAndUpdateAsync(silentMode);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"手动更新检查失败: {ex.Message}");
                if (!silentMode)
                {
                    MessageBox.Show(
                        $"更新检查失败: {ex.Message}",
                        "错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

    }
}
