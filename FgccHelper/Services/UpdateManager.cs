using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using FgccHelper.Models;
using FgccHelper.Services;

namespace FgccHelper.Services
{
    /// <summary>
    /// 更新管理器
    /// </summary>
    public class UpdateManager : IDisposable
    {
        private readonly UpdateService _updateService;
        private readonly UpdateConfig _config;
        private UpdateWindow _updateWindow;

        public UpdateManager(UpdateConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _updateService = new UpdateService(config);
        }

        /// <summary>
        /// 检查并执行更新
        /// </summary>
        public async Task<UpdateResult> CheckAndUpdateAsync(bool silentMode = false)
        {
            try
            {
                // 获取最新版本信息
                var latestVersion = await _updateService.GetLatestVersionAsync();
                
                // 如果 latestVersion 为 null，说明没有检测到任何版本信息（例如 COS 返回 404），则视为无需更新
                if (latestVersion == null)
                {
                    if (!silentMode)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("当前已是最新版本。", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }
                    return UpdateResult.AlreadyLatest;
                }
                
                // 检查是否需要更新
                if (!_updateService.IsUpdateAvailable(_config.CurrentVersion, latestVersion.Version))
                {
                    if (!silentMode)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("当前已是最新版本。", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }
                    return UpdateResult.AlreadyLatest;
                }

                // 检查版本兼容性
                if (!IsVersionCompatible(latestVersion.MinVersion))
                {
                    return UpdateResult.VersionNotSupported;
                }

                // 显示更新提示
                if (!silentMode)
                {
                    var userResponse = await ShowUpdateNotificationAsync(latestVersion);
                    if (!userResponse)
                    {
                        return UpdateResult.NewVersionAvailableUserCancelled;
                    }
                }

                // 下载并安装更新
                return await DownloadAndInstallUpdateAsync(latestVersion);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新检查失败: {ex.Message}");
                
                if (!silentMode)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"检查更新失败: {ex.Message}",
                            "更新错误",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                }
                
                return UpdateResult.NetworkError;
            }
        }

        /// <summary>
        /// 显示更新提示窗口
        /// </summary>
        private async Task<bool> ShowUpdateNotificationAsync(VersionInfo latestVersion)
        {
            var tcs = new TaskCompletionSource<bool>();

            Application.Current.Dispatcher.Invoke(() =>
            {
                _updateWindow = new UpdateWindow(latestVersion, _config.CurrentVersion)
                {
                    Owner = Application.Current.MainWindow
                };

                _updateWindow.UpdateAccepted += (sender, e) =>
                {
                    tcs.SetResult(true);
                    _updateWindow.Close();
                };

                _updateWindow.UpdateDeclined += (sender, e) =>
                {
                    tcs.SetResult(false);
                    _updateWindow.Close();
                };

                _updateWindow.ShowDialog();
            });

            return await tcs.Task;
        }

        /// <summary>
        /// 下载并安装更新
        /// </summary>
        private async Task<UpdateResult> DownloadAndInstallUpdateAsync(VersionInfo latestVersion)
        {
            try
            {
                // 生成下载文件名
                var fileName = $"FgccHelper_v{latestVersion.Version}.zip";
                var downloadPath = UpdateService.GetTempDownloadPath(fileName);

                // 显示下载进度窗口
                DownloadProgressWindow downloadWindow = null;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    downloadWindow = new DownloadProgressWindow
                    {
                        Owner = Application.Current.MainWindow
                    };
                    downloadWindow.Show();
                });

                // 下载更新包
                var progress = new Progress<DownloadProgressInfo>(info =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        downloadWindow.UpdateProgress(info);
                    });
                });

                var downloadSuccess = await _updateService.DownloadUpdateAsync(
                    latestVersion.DownloadUrl, downloadPath, progress);

                if (!downloadSuccess)
                {
                    Application.Current.Dispatcher.Invoke(() => downloadWindow.Close());
                    return UpdateResult.UpdateFailed;
                }

                // 验证下载文件
                if (!_updateService.VerifyUpdatePackage(downloadPath, latestVersion.Checksum))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        downloadWindow.Close();
                        MessageBox.Show(
                            "下载文件校验失败，可能是文件损坏或被篡改",
                            "更新错误",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                    
                    return UpdateResult.VerificationFailed;
                }

                // 安装更新
                Application.Current.Dispatcher.Invoke(() =>
                {
                    downloadWindow.UpdateStatus("正在安装更新...");
                });

                var installSuccess = await InstallUpdateAsync(downloadPath, latestVersion.Version);
                
                Application.Current.Dispatcher.Invoke(() => downloadWindow.Close());

                if (installSuccess)
                {
                    // 更新配置文件中的版本号
                    _config.CurrentVersion = latestVersion.Version;
                    SaveUpdateConfig();

                    return UpdateResult.UpdateSuccess;
                }

                return UpdateResult.UpdateFailed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新安装失败: {ex.Message}");
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"更新安装失败: {ex.Message}",
                        "更新错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
                
                return UpdateResult.UpdateFailed;
            }
        }

        /// <summary>
        /// 安装更新
        /// </summary>
        private async Task<bool> InstallUpdateAsync(string updatePackagePath, string newVersion)
        {
            try
            {
                // 获取当前应用程序目录
                var currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var tempExtractPath = Path.Combine(Path.GetTempPath(), "FgccHelper", "UpdateExtract", newVersion);

                // 清理并创建临时目录
                if (Directory.Exists(tempExtractPath))
                {
                    Directory.Delete(tempExtractPath, true);
                }
                Directory.CreateDirectory(tempExtractPath);

                // 解压更新包
                ZipFile.ExtractToDirectory(updatePackagePath, tempExtractPath);

                // 创建更新脚本
                var updateScriptPath = CreateUpdateScript(currentDirectory, tempExtractPath);

                // 启动更新程序
                var updateProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = updateScriptPath,
                        Arguments = $"\"{currentDirectory}\" \"{tempExtractPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        Verb = "runas" // 以管理员权限运行
                    }
                };

                // 启动更新进程并退出当前应用
                updateProcess.Start();
                
                // 延迟关闭以确保更新进程启动
                await Task.Delay(1000);
                
                // 关闭当前应用程序
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.Shutdown();
                });

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"安装更新失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 创建更新脚本
        /// </summary>
        private string CreateUpdateScript(string targetDirectory, string sourceDirectory)
        {
            var scriptPath = Path.Combine(Path.GetTempPath(), "FgccHelper", "update.bat");

            // 确保路径末尾没有反斜杠，避免与引号冲突导致批处理解析错误
            targetDirectory = targetDirectory.TrimEnd('\\');
            sourceDirectory = sourceDirectory.TrimEnd('\\');

            var scriptContent = $@"
@echo off
chcp 65001 > nul
echo 正在安装 FgccHelper 更新...
timeout /t 3 /nobreak > nul

:: 停止相关进程
taskkill /F /IM FgccHelper.exe > nul 2>&1

:: 备份当前版本
set ""backupDir={targetDirectory}.backup.{DateTime.Now:yyyyMMddHHmmss}""
if exist ""{targetDirectory}"" (
    echo 正在备份旧版本...
    xcopy /E /I /Y ""{targetDirectory}"" ""%backupDir%"" > nul
)

:: 复制新文件
echo 正在复制新文件...
xcopy /E /I /Y ""{sourceDirectory}"" ""{targetDirectory}"" > nul

:: 启动应用程序
echo 正在启动应用程序...
cd /d ""{targetDirectory}""
start """" ""FgccHelper.exe""

:: 清理临时文件
rmdir /S /Q ""{sourceDirectory}""
del /F /Q ""%~f0""
";

            File.WriteAllText(scriptPath, scriptContent, Encoding.UTF8);
            return scriptPath;
        }

        /// <summary>
        /// 检查版本兼容性
        /// </summary>
        private bool IsVersionCompatible(string minVersion)
        {
            try
            {
                if (string.IsNullOrEmpty(minVersion))
                    return true;

                var current = new Version(_config.CurrentVersion);
                var minimum = new Version(minVersion);
                return current >= minimum;
            }
            catch
            {
                return true; // 如果版本解析失败，默认兼容
            }
        }

        /// <summary>
        /// 保存更新配置
        /// </summary>
        private void SaveUpdateConfig()
        {
            try
            {
                var configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FgccHelper",
                    "update.config");

                var directory = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(_config, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(configPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存更新配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载更新配置
        /// </summary>
        public static UpdateConfig LoadUpdateConfig()
        {
            try
            {
                var configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FgccHelper",
                    "update.config");

                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath, Encoding.UTF8);
                    var config = Newtonsoft.Json.JsonConvert.DeserializeObject<UpdateConfig>(json);
                    if (config != null)
                    {
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载更新配置失败: {ex.Message}");
            }

            // 返回默认配置
            return new UpdateConfig
            {
                CurrentVersion = GetCurrentApplicationVersion()
            };
        }

        /// <summary>
        /// 获取当前应用程序版本
        /// </summary>
        public static string GetCurrentApplicationVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
            catch
            {
                return "1.0.0";
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _updateService?.Dispose();
            _updateWindow?.Close();
        }
    }
}