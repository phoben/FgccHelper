using System;
using System.Collections.Generic;

namespace FgccHelper.Models
{
    /// <summary>
    /// 版本信息模型
    /// </summary>
    public class VersionInfo
    {
        /// <summary>
        /// 版本号
        /// </summary>
        public string Version { get; set; } = "";

        /// <summary>
        /// 发布日期
        /// </summary>
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// 下载地址
        /// </summary>
        public string DownloadUrl { get; set; } = "";

        /// <summary>
        /// 文件大小(字节)
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 文件校验和(SHA256)
        /// </summary>
        public string Checksum { get; set; } = "";

        /// <summary>
        /// 发布说明
        /// </summary>
        public List<string> ReleaseNotes { get; set; } = new List<string>();

        /// <summary>
        /// 最低支持版本
        /// </summary>
        public string MinVersion { get; set; } = "";

        /// <summary>
        /// 是否强制更新
        /// </summary>
        public bool ForceUpdate { get; set; }

        /// <summary>
        /// 获取发布说明文本
        /// </summary>
        public string GetReleaseNotesText()
        {
            return string.Join("\n", ReleaseNotes);
        }

        /// <summary>
        /// 获取格式化的文件大小
        /// </summary>
        public string GetFormattedFileSize()
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = FileSize;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// 更新结果枚举
    /// </summary>
    public enum UpdateResult
    {
        /// <summary>
        /// 已经是最新版本
        /// </summary>
        AlreadyLatest,

        /// <summary>
        /// 发现新版本，用户取消更新
        /// </summary>
        NewVersionAvailableUserCancelled,

        /// <summary>
        /// 更新成功
        /// </summary>
        UpdateSuccess,

        /// <summary>
        /// 更新失败
        /// </summary>
        UpdateFailed,

        /// <summary>
        /// 网络错误
        /// </summary>
        NetworkError,

        /// <summary>
        /// 文件校验失败
        /// </summary>
        VerificationFailed,

        /// <summary>
        /// 不支持的版本
        /// </summary>
        VersionNotSupported
    }

    /// <summary>
    /// 下载进度信息
    /// </summary>
    public class DownloadProgressInfo
    {
        /// <summary>
        /// 已下载字节数
        /// </summary>
        public long BytesDownloaded { get; set; }

        /// <summary>
        /// 总字节数
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// 下载进度百分比
        /// </summary>
        public int ProgressPercentage
        {
            get
            {
                if (TotalBytes <= 0) return 0;
                return (int)((BytesDownloaded * 100) / TotalBytes);
            }
        }

        /// <summary>
        /// 下载速度(字节/秒)
        /// </summary>
        public long SpeedBytesPerSecond { get; set; }

        /// <summary>
        /// 剩余时间(秒)
        /// </summary>
        public int RemainingSeconds
        {
            get
            {
                if (SpeedBytesPerSecond <= 0) return 0;
                long remainingBytes = TotalBytes - BytesDownloaded;
                return (int)(remainingBytes / SpeedBytesPerSecond);
            }
        }
    }
}