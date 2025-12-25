using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FgccHelper.Models;
using FgccHelper.Services;
using COSXML;
using COSXML.Auth;
using COSXML.Model.Object;
using COSXML.Utils;
using COSXML.CosException;

namespace FgccHelper
{
    /// <summary>
    /// COS版本发布工具命令行程序
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("FgccHelper COS版本发布工具");
            Console.WriteLine("===========================");
            Console.WriteLine();

            try
            {
                // 显示帮助信息
                if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
                {
                    ShowHelp();
                    return;
                }

                // 解析命令
                switch (args[0].ToLower())
                {
                    case "create":
                        await CreateRelease(args);
                        break;
                    
                    case "upload":
                        await UploadRelease(args);
                        break;
                    
                    case "test":
                        await TestConnection(args);
                        break;
                    
                    default:
                        Console.WriteLine($"未知命令: {args[0]}");
                        ShowHelp();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        private static void ShowHelp()
        {
            Console.WriteLine("用法:");
            Console.WriteLine("  CosReleaseTool create <source_dir> <version> <release_notes> [options]");
            Console.WriteLine("  CosReleaseTool upload <release_dir>");
            Console.WriteLine("  CosReleaseTool test");
            Console.WriteLine();
            Console.WriteLine("命令:");
            Console.WriteLine("  create  - 创建版本发布包");
            Console.WriteLine("  upload  - 上传发布包到COS (使用代码内置配置)");
            Console.WriteLine("  test    - 测试COS连接");
            Console.WriteLine();
            Console.WriteLine("参数:");
            Console.WriteLine("  source_dir      - 源程序目录");
            Console.WriteLine("  version         - 版本号 (例如: 1.2.3)");
            Console.WriteLine("  release_notes   - 发布说明，多个说明用分号分隔");
            Console.WriteLine("  release_dir     - 发布包目录");
            Console.WriteLine();
            Console.WriteLine("选项 (create):");
            Console.WriteLine("  --min-version <ver> - 最低支持版本");
            Console.WriteLine("  --force             - 强制更新");
            Console.WriteLine("  --output <dir>      - 输出目录");
            Console.WriteLine();
            Console.WriteLine("示例:");
            Console.WriteLine("  CosReleaseTool create bin\\Release 1.2.3 \"修复bug;新增功能\"");
            Console.WriteLine("  CosReleaseTool upload temp\\releases\\v1.2.3");
        }

        /// <summary>
        /// 创建发布包
        /// </summary>
        private static async Task CreateRelease(string[] args)
        {
            if (args.Length < 4)
            {
                throw new ArgumentException("参数不足。用法: create <source_dir> <version> <release_notes>");
            }

            var sourceDir = args[1];
            var version = args[2];
            var releaseNotesText = args[3];
            
            // 解析选项
            var options = ParseOptions(args, 4);
            var outputDir = GetValueOrDefault(options, "output", Path.Combine(Path.GetTempPath(), "FgccHelper", "Releases"));
            var minVersion = GetValueOrDefault(options, "min-version", "");
            var forceUpdate = options.ContainsKey("force");

            // 获取默认配置 (硬编码在 UpdateConfig 中)
            var cosConfig = new UpdateConfig { CurrentVersion = "1.0.0" };

            Console.WriteLine($"创建版本发布包: {version}");
            Console.WriteLine($"源目录: {sourceDir}");
            Console.WriteLine($"输出目录: {outputDir}");
            Console.WriteLine($"COS配置: {cosConfig.CosBucketUrl}");
            Console.WriteLine();

            // 解析发布说明
            var releaseNotes = new List<string>(releaseNotesText.Split(';'));
            for (int i = 0; i < releaseNotes.Count; i++)
            {
                releaseNotes[i] = releaseNotes[i].Trim();
            }

            // 创建发布工具
            var releaseTool = new Services.CosReleaseTool(cosConfig);

            // 创建发布包
            var versionInfo = await Task.Run(() => releaseTool.CreateReleasePackage(
                sourceDir, version, releaseNotes, minVersion, forceUpdate));

            // 生成发布报告
            var report = releaseTool.GenerateReleaseReport(versionInfo, sourceDir);
            var reportPath = Path.Combine(outputDir, $"release_report_v{version}.txt");
            
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(reportPath, report, Encoding.UTF8);

            Console.WriteLine();
            Console.WriteLine("发布包创建成功！");
            Console.WriteLine($"发布报告: {reportPath}");
            Console.WriteLine();
            Console.WriteLine("发布文件:");
            Console.WriteLine($"  版本信息: {Path.Combine(outputDir, $"v{version}.json")}");
            Console.WriteLine($"  最新版本: {Path.Combine(outputDir, "latest.json")}");
            Console.WriteLine($"  更新包: {Path.Combine(outputDir, $"FgccHelper_v{version}.zip")}");
            Console.WriteLine($"  校验和: {Path.Combine(outputDir, $"FgccHelper_v{version}.zip.sha256")}");
            Console.WriteLine();
            Console.WriteLine("下一步:");
            Console.WriteLine($"  使用 upload 命令上传发布文件到COS");
            Console.WriteLine($"  例如: CosReleaseTool upload {outputDir}");
        }

        /// <summary>
        /// 上传到COS
        /// </summary>
        private static async Task UploadRelease(string[] args)
        {
            if (args.Length < 2)
            {
                throw new ArgumentException("参数不足。用法: upload <release_dir>");
            }

            var releaseDir = args[1];
            if (!Directory.Exists(releaseDir))
            {
                throw new ArgumentException($"发布目录不存在: {releaseDir}");
            }

            // 解析选项
            var options = ParseOptions(args, 2);
            
            // 获取配置默认值
            var config = new UpdateConfig { CurrentVersion = "1.0.0" };

            var secretId = GetValueOrDefault(options, "secret-id", config.CosSecretId);
            var secretKey = GetValueOrDefault(options, "secret-key", config.CosSecretKey);
            var bucket = GetValueOrDefault(options, "bucket", config.CosBucketName);
            var region = GetValueOrDefault(options, "region", config.CosRegion);

            if (string.IsNullOrEmpty(bucket)) bucket = "gridfriend-1257098086"; // 兜底
            if (string.IsNullOrEmpty(region)) region = "ap-shanghai"; // 兜底

            if (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey))
            {
                Console.WriteLine("警告: 未提供SecretId或SecretKey，将无法执行上传。");
                Console.WriteLine("请使用 --secret-id 和 --secret-key 参数，或者在 FgccHelper.Models.UpdateConfig.cs 中配置。");
                // 暂时不抛出异常，允许仅列出文件
            }

            Console.WriteLine($"准备上传发布文件到COS...");
            Console.WriteLine($"发布目录: {releaseDir}");
            Console.WriteLine($"Bucket: {bucket}");
            Console.WriteLine($"Region: {region}");
            Console.WriteLine();

            // 初始化COS客户端
            CosXml cosXml = null;
            if (!string.IsNullOrEmpty(secretId) && !string.IsNullOrEmpty(secretKey))
            {
                CosXmlConfig cosXmlConfig = new CosXmlConfig.Builder()
                    .SetRegion(region)
                    .SetDebugLog(false)
                    .Build();

                QCloudCredentialProvider cosCredentialProvider = new DefaultQCloudCredentialProvider(secretId, secretKey, 600);
                cosXml = new CosXmlServer(cosXmlConfig, cosCredentialProvider);
            }

            // 列出需要上传的文件
            var files = Directory.GetFiles(releaseDir, "*.*", SearchOption.AllDirectories);
            Console.WriteLine("正在处理文件:");
            
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                // 忽略报告文件
                if (fileName.StartsWith("release_report")) continue;

                var fileSize = new FileInfo(file).Length;
                
                // 确定COS路径
                string cosPath;
                if (fileName.EndsWith(".json"))
                {
                    cosPath = $"FgccHelper/versions/{fileName}";
                }
                else if (fileName.EndsWith(".zip"))
                {
                    cosPath = $"FgccHelper/packages/{fileName}";
                }
                else if (fileName.EndsWith(".sha256"))
                {
                    cosPath = $"FgccHelper/checksums/{fileName}";
                }
                else
                {
                    cosPath = $"FgccHelper/{fileName}";
                }

                Console.Write($"  [准备] {fileName} -> {cosPath} ({FormatFileSize(fileSize)})... ");

                if (cosXml != null)
                {
                    try
                    {
                        // 上传文件
                        PutObjectRequest request = new PutObjectRequest(bucket, cosPath, file);
                        
                        // 设置为公有读
                        request.SetCosACL("public-read");

                        PutObjectResult result = cosXml.PutObject(request);
                        Console.WriteLine("[成功]");

                        // 特殊处理：如果是 latest.json，同时上传一份到根目录的 versions/latest.json，以兼容旧版本客户端
                        if (fileName == "latest.json")
                        {
                            Console.Write($"  [兼容] {fileName} -> versions/{fileName}... ");
                            PutObjectRequest legacyRequest = new PutObjectRequest(bucket, $"versions/{fileName}", file);
                            legacyRequest.SetCosACL("public-read");
                            cosXml.PutObject(legacyRequest);
                            Console.WriteLine("[成功]");
                        }
                    }
                    catch (CosClientException clientEx)
                    {
                        Console.WriteLine($"[失败] 客户端错误: {clientEx.Message}");
                    }
                    catch (CosServerException serverEx)
                    {
                        Console.WriteLine($"[失败] 服务端错误: {serverEx.GetInfo()}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[失败] 未知错误: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("[跳过] (未配置凭证)");
                }
            }

            Console.WriteLine();
            if (cosXml == null)
            {
                Console.WriteLine("提示: 提供 --secret-id 和 --secret-key 参数以启用自动上传。");
            }
            else
            {
                Console.WriteLine("所有操作已完成。");
            }
        }

        /// <summary>
        /// 测试COS连接
        /// </summary>
        private static async Task TestConnection(string[] args)
        {
            var cosConfig = new UpdateConfig { CurrentVersion = "1.0.0" };

            Console.WriteLine("测试COS连接...");
            Console.WriteLine($"COS储存桶: {cosConfig.CosBucketUrl}");
            Console.WriteLine();

            try
            {
                var updateService = new UpdateService(cosConfig);
                var versionInfo = await updateService.GetLatestVersionAsync();

                Console.WriteLine("连接成功！");
                Console.WriteLine($"最新版本: {versionInfo.Version}");
                Console.WriteLine($"发布日期: {versionInfo.ReleaseDate:yyyy-MM-dd}");
                Console.WriteLine($"文件大小: {versionInfo.GetFormattedFileSize()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 解析命令行选项
        /// </summary>
        private static Dictionary<string, string> ParseOptions(string[] args, int startIndex)
        {
            var options = new Dictionary<string, string>();
            
            for (int i = startIndex; i < args.Length; i++)
            {
                if (args[i].StartsWith("--"))
                {
                    var key = args[i].Substring(2);
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        options[key] = args[i + 1];
                        i++; // 跳过值
                    }
                    else
                    {
                        options[key] = "true"; // 布尔选项
                    }
                }
            }
            
            return options;
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        private static string FormatFileSize(long bytes)
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

        private static string GetValueOrDefault(Dictionary<string, string> dict, string key, string defaultValue)
        {
            return dict.ContainsKey(key) ? dict[key] : defaultValue;
        }
    }
}
