using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using FgccHelper.Models;
using System.Diagnostics;

namespace FgccHelper.Services
{
    /// <summary>
    /// 自动更新服务
    /// </summary>
    public class UpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly UpdateConfig _config;
        private const int TIMEOUT_SECONDS = 30;

        public UpdateService(UpdateConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(TIMEOUT_SECONDS)
            };
        }

        /// <summary>
        /// 获取最新版本信息
        /// </summary>
        public async Task<VersionInfo> GetLatestVersionAsync()
        {
            try
            {
                // V2.0.0+ 之后使用新的路径结构
                string versionInfoUrl = $"{_config.CosBucketUrl}/FgccHelper/versions/latest.json";
                
                using (var response = await _httpClient.GetAsync(versionInfoUrl))
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // 如果返回 404，说明没有发布任何版本，返回 null 视为无更新
                        return null;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"无法获取版本信息: HTTP {response.StatusCode}");
                    }

                    string jsonContent = await response.Content.ReadAsStringAsync();
                    var versionInfo = JsonConvert.DeserializeObject<VersionInfo>(jsonContent);
                    
                    if (versionInfo == null)
                    {
                        throw new Exception("版本信息格式错误");
                    }

                    return versionInfo;
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new Exception("获取版本信息超时，请检查网络连接");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"网络请求失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"获取版本信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 比较版本号
        /// </summary>
        public bool IsUpdateAvailable(string currentVersion, string latestVersion)
        {
            try
            {
                var current = new Version(currentVersion);
                var latest = new Version(latestVersion);
                return latest > current;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"版本号比较失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 下载更新包
        /// </summary>
        public async Task<bool> DownloadUpdateAsync(string downloadUrl, string savePath, 
            IProgress<DownloadProgressInfo> progress = null)
        {
            try
            {
                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"下载失败: HTTP {response.StatusCode}");
                    }

                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    var progressInfo = new DownloadProgressInfo
                    {
                        TotalBytes = totalBytes,
                        BytesDownloaded = 0
                    };

                    // 确保目录存在
                    var directory = Path.GetDirectoryName(savePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        var bytesRead = 0;
                        var totalBytesRead = 0L;
                        var stopwatch = Stopwatch.StartNew();

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;

                            if (progress != null && totalBytes > 0)
                            {
                                progressInfo.BytesDownloaded = totalBytesRead;
                                
                                // 计算下载速度
                                if (stopwatch.ElapsedMilliseconds > 0)
                                {
                                    progressInfo.SpeedBytesPerSecond = (long)(totalBytesRead * 1000 / stopwatch.ElapsedMilliseconds);
                                }

                                progress.Report(progressInfo);
                            }
                        }

                        stopwatch.Stop();
                    }

                    return true;
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new Exception("下载超时，请检查网络连接");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"下载请求失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"下载更新包失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 验证下载文件完整性
        /// </summary>
        public bool VerifyUpdatePackage(string filePath, string expectedHash)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }

                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hashBytes = sha256.ComputeHash(stream);
                    var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    
                    return actualHash.Equals(expectedHash.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"文件校验失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 计算文件的SHA256哈希值
        /// </summary>
        public static string ComputeFileHash(string filePath)
        {
            try
            {
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hashBytes = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"计算文件哈希失败: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 获取临时下载目录
        /// </summary>
        public static string GetTempDownloadPath(string fileName)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "FgccHelper", "Updates");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
            return Path.Combine(tempDir, fileName);
        }

        /// <summary>
        /// 清理临时下载文件
        /// </summary>
        public static void CleanTempDownloads()
        {
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "FgccHelper", "Updates");
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清理临时文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}