using FgccHelper.Models;
using Newtonsoft.Json; // 确保已添加此 NuGet 包
using Newtonsoft.Json.Linq; // 用于更灵活地处理JSON
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FgccHelper.Services
{
    public class ProjectAnalysisService
    {
        private static readonly List<string> BuiltInTableNames = new List<string>
        {
            "ClientInfo",
            "CustomizedPropertiesTable",
            "DatabaseInfo",
            "folder",
            "MFATrustedDeviceInfo",
            "migration_records",
            "organization_role",
            "OrganizationLevelTable",
            "OrganizationMemberInRoles",
            "OrganizationMemberListTable",
            "OrganizationNodeListTable",
            "PropertyListTable",
            "role_folder",
            "role_folder_order",
            "role_inherited",
            "UserProfile",
            "webpages_Membership",
            "webpages_Roles",
            "webpages_UsersInRoles",
            "windows_users",
            "windows_UsersInRoles"
        };

        public Project AnalyzeProject(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                // 或者抛出异常，或者返回一个表示错误的 Project 对象
                return null; 
            }

            Project project = new Project
            {
                ProjectName = new DirectoryInfo(projectPath).Name,
                DesignerVersion = GetDesignerVersion(projectPath)
            };

            // 获取并填充各项统计数据
            var pageStats = GetPageStatistics(projectPath);
            project.Statistics.Add(pageStats);
            project.ProjectStats.PageCount = pageStats.Count;

            var tableStats = GetTableStatistics(projectPath);
            project.Statistics.Add(tableStats);
            project.ProjectStats.TableCount = tableStats.Count;

            var processStats = GetProcessStatistics(projectPath);
            project.Statistics.Add(processStats);
            project.ProjectStats.BusinessProcessCount = processStats.Count;

            var reportStats = GetReportStatistics(projectPath);
            project.Statistics.Add(reportStats);
            project.ProjectStats.ReportCount = reportStats.Count;

            var serverCommandStats = GetServerCommandStatistics(projectPath);
            project.Statistics.Add(serverCommandStats);
            project.ProjectStats.ServerCommandCount = serverCommandStats.Count;

            var pluginStats = GetPluginStatistics(projectPath);
            project.Statistics.Add(pluginStats);
            project.ProjectStats.CustomPluginCount = pluginStats.Count;

            var userControlStats = GetUserControlPageStatistics(projectPath);
            project.Statistics.Add(userControlStats);
            project.ProjectStats.CustomComponentCount = userControlStats.Count;

            var schedulerStats = GetSchedulerTaskStatistics(projectPath);
            project.Statistics.Add(schedulerStats);
            project.ProjectStats.ScheduledTaskCount = schedulerStats.Count;

            var userJsStats = GetUserJavaScriptStatistics(projectPath);
            project.Statistics.Add(userJsStats);
            project.ProjectStats.ExtendedJsFileCount = userJsStats.Count;

            var externalJsStats = GetExternalJsStatistics(projectPath);
            project.Statistics.Add(externalJsStats);
            project.ProjectStats.ExternalJsFileCount = externalJsStats.Count;

            var externalCssStats = GetExternalCssStatistics(projectPath);
            project.Statistics.Add(externalCssStats);
            project.ProjectStats.ExternalCssFileCount = externalCssStats.Count;
            
            // Note: project.ComplexityScore will be calculated later in MainWindow by ComplexityCalculator
            // project.ProjectType should be set in MainWindow where the project opening logic resides.

            return project;
        }

        private string GetDesignerVersion(string projectPath)
        {
            var docInfoPath = Path.Combine(projectPath, "DocumentInfo");
            if (!File.Exists(docInfoPath)) return string.Empty;

            try
            {
                var jsonContent = File.ReadAllText(docInfoPath);
                var jObject = JObject.Parse(jsonContent);
                return jObject["VersionString"]?.ToString() ?? string.Empty;
            }
            catch (Exception) // 例如: JsonReaderException, NullReferenceException
            {
                // Log error if needed: Console.WriteLine($"Error reading DocumentInfo: {ex.Message}");
                return string.Empty; // 文件无法解析或属性缺失
            }
        }

        private StatisticItem GetFolderFileStatistics(string projectPath, string subFolderName, string fileExtensionFilter, string statisticName, string statisticDescription)
        {
            var item = new StatisticItem { Name = statisticName, Description = statisticDescription, Count = 0 };
            var targetPath = Path.Combine(projectPath, subFolderName);

            if (Directory.Exists(targetPath))
            {
                try
                {
                    var files = Directory.GetFiles(targetPath, $"*.{fileExtensionFilter}", SearchOption.AllDirectories);
                    // This is where the Page-specific filter would ideally go if we made this method more generic
                    // For now, GetPageStatistics will do its own filtering on the results of this method.

                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        item.Details.Add(new DetailEntry
                        {
                            Name = Path.GetFileNameWithoutExtension(file), // Name without extension
                            Size = $"{((double)fileInfo.Length / (1024 * 1024)):F2} MB",
                            FileType = fileInfo.Extension.TrimStart('.') // Actual file extension
                        });
                    }
                    item.Count = item.Details.Count; // Count after adding all details
                }
                catch (Exception) // 例如: UnauthorizedAccessException, IOException
                {
                    // Log error if needed: Console.WriteLine($"Error scanning folder {targetPath}: {ex.Message}");
                    item.Count = 0; 
                    item.Details.Clear();
                }
            }
            return item;
        }

        // Helper method to check if a string consists only of letters or digits
        private bool IsAlphaNumeric(string str)
        {
            if (string.IsNullOrEmpty(str)) return false;
            foreach (char c in str)
            {
                if (!char.IsLetterOrDigit(c))
                    return false;
            }
            return true;
        }

        private StatisticItem GetPageStatistics(string projectPath)
        {
            // Step 1: Get all .json files from the Pages directory recursively initially using a temporary name/description
            // The fileExtensionFilter for GetFolderFileStatistics should be "json"
            var tempStatItem = GetFolderFileStatistics(projectPath, "Pages", "json", "页面数量 (原始)", "工程中所有页面的总数量 (原始)");

            // Step 2: Filter these details based on the specific rules for pages
            var finalStatItem = new StatisticItem 
            { 
                Name = "页面数量", 
                Description = "工程中所有页面的总数量" 
            };

            if (tempStatItem.Details != null)
            {
                foreach (var detailEntry in tempStatItem.Details)
                {
                    // detailEntry.Name is already without extension here due to changes in GetFolderFileStatistics
                    string fileNameWithoutExtension = detailEntry.Name; 

                    bool toBeExcluded = fileNameWithoutExtension.Length > 30 && IsAlphaNumeric(fileNameWithoutExtension);

                    if (!toBeExcluded)
                    {
                        finalStatItem.Details.Add(detailEntry);
                    }
                }
            }
            finalStatItem.Count = finalStatItem.Details.Count;
            return finalStatItem;
        }

        private StatisticItem GetTableStatistics(string projectPath)
        {
            // Step 1: Get tables from the 'Tables' folder
            var fileBasedTablesStatItem = GetFolderFileStatistics(projectPath, "Tables", "json", "数据表数量", "工程中所有数据表的总数量");
            
            // Step 2: Add built-in tables
            foreach (var builtInName in BuiltInTableNames)
            {
                fileBasedTablesStatItem.Details.Add(new DetailEntry
                {
                    Name = builtInName,
                    Size = "N/A",
                    FileType = "内置表"
                });
            }
            
            // Step 3: Update the count
            fileBasedTablesStatItem.Count = fileBasedTablesStatItem.Details.Count; // Recalculate count after adding built-in tables
            
            return fileBasedTablesStatItem;
        }

        private StatisticItem GetProcessStatistics(string projectPath)
        {
             return GetFolderFileStatistics(projectPath, "Process", "bpmn", "流程数量", "工程中所有业务流程的总数量");
        }

        private StatisticItem GetReportStatistics(string projectPath)
        {
            return GetFolderFileStatistics(projectPath, "Reports", "json", "报表数量", "工程中所有报表的总数量");
        }

        private StatisticItem GetServerCommandStatistics(string projectPath)
        {
            return GetFolderFileStatistics(projectPath, "ServerCommands", "json", "接口数量", "工程中所有服务端命令（接口）的总数量");
        }
        
        private StatisticItem GetPluginStatistics(string projectPath)
        {
            return GetFolderFileStatistics(projectPath, "Plugin", "zip", "自定义插件数量", "工程中所有自定义插件 (.zip) 的总数量");
        }

        private StatisticItem GetUserControlPageStatistics(string projectPath)
        {
            return GetFolderFileStatistics(projectPath, "UserControlPages", "json", "自定义组件数量", "工程中所有自定义组件 (.json) 的总数量");
        }

        private StatisticItem GetSchedulerTaskStatistics(string projectPath)
        {
            return GetFolderFileStatistics(projectPath, "SchedulerTasks", "json", "计划任务数量", "工程中所有计划任务的总数量");
        }

        private StatisticItem GetUserJavaScriptStatistics(string projectPath)
        {
            // 文档中为 UserJaveScript，请再次确认此名称
            return GetFolderFileStatistics(projectPath, "UserJaveScript", "js", "扩展JavaScript数量", "工程中所有扩展JavaScript (.js) 文件的总数量");
        }

        private JObject SafelyParseCustomLibraries(string projectPath)
        {
            var customLibrariesPath = Path.Combine(projectPath, "CustomLibraries.json");
            if (!File.Exists(customLibrariesPath)) return null;

            try
            {
                var jsonContent = File.ReadAllText(customLibrariesPath);
                return JObject.Parse(jsonContent);
            }
            catch (Exception) // 例如: JsonReaderException
            {
                // Log error if needed: Console.WriteLine($"Error reading CustomLibraries.json: {ex.Message}");
                return null; // 文件无法解析
            }
        }

        private StatisticItem GetExternalJsStatistics(string projectPath)
        {
            var item = new StatisticItem { Name = "外部JS文件数量", Description = "工程中引用的外部JavaScript文件的总数量", Count = 0 };
            var customLibraries = SafelyParseCustomLibraries(projectPath);

            if (customLibraries?["UserJSFileList"] is JArray jsFileList)
            {
                item.Count = jsFileList.Count;
                foreach (var jsEntry in jsFileList)
                {
                    var name = jsEntry["Name"]?.ToString();
                    if (string.IsNullOrEmpty(name)) continue;

                    var isLinkToken = jsEntry["IsLink"];
                    bool isLink = isLinkToken?.Type == JTokenType.Boolean ? isLinkToken.Value<bool>() : false;
                    
                    // 如果 IsLink 不存在或非布尔值，且 Name 是一个 URL，也认为是 Link
                    if ((isLinkToken == null || isLinkToken.Type != JTokenType.Boolean) && 
                        (name.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || name.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    {
                        isLink = true;
                    }

                    item.Details.Add(new DetailEntry
                    {
                        Name = name,
                        Size = "N/A",
                        FileType = isLink ? "外部JS链接" : "本地JS文件"
                    });
                }
            }
            return item;
        }

        private StatisticItem GetExternalCssStatistics(string projectPath)
        {
            var item = new StatisticItem { Name = "外部CSS文件数量", Description = "工程中引用的外部CSS文件的总数量", Count = 0 };
            var customLibraries = SafelyParseCustomLibraries(projectPath);

            if (customLibraries?["UserCSSFileList"] is JArray cssFileList)
            {
                item.Count = cssFileList.Count;
                foreach (var cssEntry in cssFileList)
                {
                    var name = cssEntry["Name"]?.ToString();
                    if (string.IsNullOrEmpty(name)) continue;
                    
                    var isLinkToken = cssEntry["IsLink"];
                    bool isLink = isLinkToken?.Type == JTokenType.Boolean ? isLinkToken.Value<bool>() : false;

                    // 如果 IsLink 不存在或非布尔值，且 Name 是一个 URL，也认为是 Link
                    if ((isLinkToken == null || isLinkToken.Type != JTokenType.Boolean) && 
                        (name.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || name.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    {
                         isLink = true;
                    }

                    item.Details.Add(new DetailEntry
                    {
                        Name = name,
                        Size = "N/A",
                        FileType = isLink ? "外部CSS链接" : "本地CSS文件"
                    });
                }
            }
            return item;
        }
    }
} 