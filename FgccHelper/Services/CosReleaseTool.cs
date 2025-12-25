using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FgccHelper.Models;
using Newtonsoft.Json;

namespace FgccHelper.Services
{
    public class CosReleaseTool
    {
        private readonly UpdateConfig _config;

        public CosReleaseTool(UpdateConfig config)
        {
            _config = config;
        }

        public VersionInfo CreateReleasePackage(string sourceDir, string version, List<string> releaseNotes, string minVersion, bool forceUpdate)
        {
            // 1. Validate source directory
            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
            }

            // 2. Prepare output directory
            string tempPath = Path.Combine(Path.GetTempPath(), "FgccHelper", "Releases", version);
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }
            Directory.CreateDirectory(tempPath);

            // 3. Zip the source directory
            string zipFileName = $"FgccHelper_v{version}.zip";
            string zipFilePath = Path.Combine(tempPath, zipFileName);
            
            // ZipFile.CreateFromDirectory includes the root folder if not handled carefully, 
            // but in .NET Framework 4.5+ it zips contents of directory.
            ZipFile.CreateFromDirectory(sourceDir, zipFilePath, CompressionLevel.Optimal, false);

            // 4. Calculate SHA256
            string checksum = CalculateSha256(zipFilePath);
            string checksumFileName = zipFileName + ".sha256";
            File.WriteAllText(Path.Combine(tempPath, checksumFileName), checksum);

            // 5. Create VersionInfo
            var versionInfo = new VersionInfo
            {
                Version = version,
                ReleaseDate = DateTime.Now,
                DownloadUrl = $"{_config.CosBucketUrl}/FgccHelper/packages/{zipFileName}",
                FileSize = new FileInfo(zipFilePath).Length,
                Checksum = checksum,
                ReleaseNotes = releaseNotes,
                MinVersion = minVersion,
                ForceUpdate = forceUpdate
            };

            // 6. Save VersionInfo as json
            string versionJson = JsonConvert.SerializeObject(versionInfo, Formatting.Indented);
            string versionJsonPath = Path.Combine(tempPath, $"v{version}.json");
            File.WriteAllText(versionJsonPath, versionJson);

            // 7. Save latest.json (copy of version info)
            string latestJsonPath = Path.Combine(tempPath, "latest.json");
            File.WriteAllText(latestJsonPath, versionJson);

            return versionInfo;
        }

        public string GenerateReleaseReport(VersionInfo versionInfo, string sourceDir)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Release Report for FgccHelper v{versionInfo.Version}");
            sb.AppendLine("==================================================");
            sb.AppendLine($"Generated at: {DateTime.Now}");
            sb.AppendLine($"Source Directory: {sourceDir}");
            sb.AppendLine($"Output File: {Path.GetFileName(versionInfo.DownloadUrl)}");
            sb.AppendLine($"Size: {versionInfo.GetFormattedFileSize()} ({versionInfo.FileSize} bytes)");
            sb.AppendLine($"Checksum (SHA256): {versionInfo.Checksum}");
            sb.AppendLine();
            sb.AppendLine("Release Notes:");
            foreach (var note in versionInfo.ReleaseNotes)
            {
                sb.AppendLine($"- {note}");
            }
            sb.AppendLine();
            sb.AppendLine("Configuration:");
            sb.AppendLine($"Bucket URL: {_config.CosBucketUrl}");
            sb.AppendLine($"Force Update: {versionInfo.ForceUpdate}");
            sb.AppendLine($"Min Version: {versionInfo.MinVersion}");
            
            return sb.ToString();
        }

        private string CalculateSha256(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
    }
}
