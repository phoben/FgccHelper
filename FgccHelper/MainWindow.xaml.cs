using FgccHelper.Models;
using FgccHelper.Services;
using Microsoft.Win32; // For OpenFileDialog
using System;
using System.Collections.Generic; // Added for List<StatisticItem>
using System.Diagnostics; // For Process
using System.IO;
using System.IO.Compression; // For ZipFile
using System.Linq; // Added for project.Statistics.Any()
using System.Text; // For StringBuilder
using System.Threading.Tasks; // For Task.Run
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; 
using System.Windows.Media; 
using Newtonsoft.Json; // For AppSettings

// Important: Add reference to System.Windows.Forms.dll for FolderBrowserDialog
// In Solution Explorer: Right-click References > Add Reference > Assemblies > Framework > System.Windows.Forms

namespace FgccHelper
{
    public enum ProjectType
    {
        Folder,
        FgccFile,
        GitRepo
    }

    public class RecentProjectEntry
    {
        public string Path { get; set; } // For Folder & .fgcc (file path), for Git (repo URL)
        public ProjectType Type { get; set; }
        public string DisplayName { get; set; } // Name to show in the menu
        public DateTime Timestamp { get; set; } // For sorting and managing count
        public string GitBranch { get; set; } // Optional: for Git projects, if a specific branch was used

        // Parameterless constructor for JSON deserialization
        public RecentProjectEntry() { }

        public RecentProjectEntry(string path, ProjectType type, string gitBranch = null)
        {
            Path = path;
            Type = type;
            Timestamp = DateTime.Now;
            GitBranch = gitBranch;

            // Generate DisplayName based on type and path
            switch (Type)
            {
                case ProjectType.Folder:
                    DisplayName = $"目录 - {new DirectoryInfo(Path).Name}";
                    break;
                case ProjectType.FgccFile:
                    DisplayName = $"文件 - {System.IO.Path.GetFileNameWithoutExtension(Path)}";
                    break;
                case ProjectType.GitRepo:
                    string repoNameStr;
                    try 
                    {
                        Uri uri = new Uri(Path);
                        string repoNameFromUri = uri.Segments.LastOrDefault()?.TrimEnd('/');
                        if (repoNameFromUri != null && repoNameFromUri.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                        {
                            repoNameFromUri = repoNameFromUri.Substring(0, repoNameFromUri.Length - 4);
                        }
                        repoNameStr = string.IsNullOrWhiteSpace(repoNameFromUri) ? Path : repoNameFromUri;
                    }
                    catch
                    {
                        repoNameStr = Path; // Fallback to full path if URL parsing fails
                    }
                    DisplayName = $"仓库 - {repoNameStr}";
                    if (!string.IsNullOrEmpty(GitBranch))
                    {
                        DisplayName += $" ({GitBranch})";
                    }
                    break;
                default:
                    DisplayName = Path;
                    break;
            }
        }
    }

    public class AppSettings
    {
        public string GitUsername { get; set; }
        public string GitPassword { get; set; } // Consider encrypting this in a real app
        public List<RecentProjectEntry> RecentProjects { get; set; } = new List<RecentProjectEntry>();
    }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private ProjectAnalysisService _analysisService;
        private ExcelExportService _excelExportService; // Added for Excel export
        private Project _currentProject;
        private string _currentProjectPath; // This will now store the actual path being analyzed (original or temp)
        private string _originalOpenedPath; // For fgcc or git, this is the fgcc file or git url
        private bool _isTempPath = false;
        private string _tempDirectoryPath = null; // Stores path to the temporary directory for .fgcc or git

        private string _baseTitle = "FgccHelper";

        // For card styling
        private Border _selectedCardBorder = null;
        private readonly Brush _normalCardBackground = Brushes.White;
        private readonly Brush _hoverCardBackground = new SolidColorBrush(Color.FromArgb(255, 220, 235, 255)); // Light AliceBlue like
        private readonly Brush _selectedCardBackground = new SolidColorBrush(Color.FromArgb(255, 190, 220, 255)); // Slightly darker blue

        private AppSettings _appSettings;
        private readonly string _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        private void SetMainUILoadingState(bool isLoading)
        {
            this.Cursor = isLoading ? Cursors.Wait : Cursors.Arrow;
            LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            MainContentGrid.IsEnabled = !isLoading; 

            // MenuRefresh is enabled only when a project is loaded and not currently loading.
            MenuRefresh.IsEnabled = !isLoading && _currentProject != null && !string.IsNullOrEmpty(_currentProjectPath);
        }

        public MainWindow()
        {
            InitializeComponent();
            LoadAppSettings();
            _analysisService = new ProjectAnalysisService();
            _excelExportService = new ExcelExportService(); // Initialize the service
            InitializeDefaultStatisticsDisplay(); 
            UpdateRecentProjectsMenu(); // Initial population of the recent projects menu
            this.Closing += MainWindow_Closing; 
            _selectedCardBorder = null; 
            MenuExportExcel.IsEnabled = false; // Disable export if no project loaded initially
        }

        private void LoadAppSettings()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    _appSettings = JsonConvert.DeserializeObject<AppSettings>(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading app settings: {ex.Message}");
                // Optionally inform user or log to a file
            }
            if (_appSettings == null) // If file doesn't exist or failed to load/deserialize
            {
                _appSettings = new AppSettings();
            }
            SaveAppSettings();
            // UpdateRecentProjectsMenu(); // This will be called later after menu is created
            UpdateRecentProjectsMenu();
        }

        private void SaveAppSettings()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_appSettings, Formatting.Indented);
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving app settings: {ex.Message}");
                MessageBox.Show($"保存配置失败: {ex.Message}", "配置错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            CleanUpTempDirectory();
            // SaveAppSettings(); // Consider if settings should be saved on close, or only when explicitly changed.
        }

        private void CleanUpTempDirectory(bool isNewProjectLoading = false)
        {
            if (_isTempPath && !string.IsNullOrEmpty(_tempDirectoryPath) && Directory.Exists(_tempDirectoryPath))
            {
                try
                {
                    Directory.Delete(_tempDirectoryPath, true);
                    // Console.WriteLine($"Temporary directory deleted: {_tempDirectoryPath}");
                }
                catch (Exception ex)
                {
                    // Log or inform the user if cleanup fails, but don't block UI.
                    Console.WriteLine($"Error deleting temporary directory '{_tempDirectoryPath}': {ex.Message}");
                    if (!isNewProjectLoading) // Only show message if not part of loading a new project
                    {
                         MessageBox.Show($"无法完全清理临时文件目录: {_tempDirectoryPath}\n请手动删除。错误: {ex.Message}", 
                                        "清理提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            _isTempPath = false;
            _tempDirectoryPath = null;
        }

        private void AddRecentProject(string path, ProjectType type, string gitBranch = null)
        {
            if (string.IsNullOrEmpty(path)) return;

            // Remove existing entry if any (match by path and type)
            _appSettings.RecentProjects.RemoveAll(r => r.Path == path && r.Type == type);

            // Create new entry
            var newEntry = new RecentProjectEntry(path, type, gitBranch);

            // Add to the beginning of the list
            _appSettings.RecentProjects.Insert(0, newEntry);

            // Keep only the top 10
            if (_appSettings.RecentProjects.Count > 10)
            {
                _appSettings.RecentProjects = _appSettings.RecentProjects.Take(10).ToList();
            }

            SaveAppSettings();
            UpdateRecentProjectsMenu(); // This will be called later after menu is created
        }

        private void UpdateRecentProjectsMenu()
        {
            MenuRecentProjects.Items.Clear();
            if (_appSettings == null || _appSettings.RecentProjects == null || !_appSettings.RecentProjects.Any())
            {
                var noRecentItem = new MenuItem { Header = "(无最近项目)", IsEnabled = false };
                MenuRecentProjects.Items.Add(noRecentItem);
            }
            else
            {
                foreach (var entry in _appSettings.RecentProjects)
                {
                    var menuItem = new MenuItem
                    {
                        Header = entry.DisplayName,
                        Tag = entry // Store the full entry for the click handler
                    };
                    menuItem.Click += RecentProject_Click; // Attach click handler
                    MenuRecentProjects.Items.Add(menuItem);
                }
            }
        }

        private async void RecentProject_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is RecentProjectEntry entry)
            {
                CleanUpTempDirectory(true); // Clean up any existing temp directory first
                SetMainUILoadingState(true);

                try
                {
                    switch (entry.Type)
                    {
                        case ProjectType.Folder:
                            _originalOpenedPath = entry.Path;
                            _currentProjectPath = entry.Path;
                            _isTempPath = false;
                            await LoadAndDisplayProject(_currentProjectPath);
                            AddRecentProject(entry.Path, ProjectType.Folder); // Re-add to top
                            break;

                        case ProjectType.FgccFile:
                            _originalOpenedPath = entry.Path;
                            _isTempPath = true;
                            await Task.Run(async () =>
                            {
                                _tempDirectoryPath = Path.Combine(Path.GetTempPath(), "FgccHelper_Temp", Guid.NewGuid().ToString());
                                Directory.CreateDirectory(_tempDirectoryPath);
                                ZipFile.ExtractToDirectory(entry.Path, _tempDirectoryPath);
                                _currentProjectPath = _tempDirectoryPath;
                                await LoadProjectData(_currentProjectPath);
                            });
                            if (_currentProject != null && _currentProject.ProjectName != "未选择工程") // Check if load was successful from LoadProjectData's perspective
                            {
                                AddRecentProject(entry.Path, ProjectType.FgccFile);
                            }
                            break;

                        case ProjectType.GitRepo:
                            _originalOpenedPath = entry.Path;
                            _isTempPath = true;
                            string repoUrlToClone = entry.Path;
                            string branchToClone = entry.GitBranch;

                            if (string.IsNullOrWhiteSpace(_appSettings.GitUsername)) 
                            {
                                MessageBox.Show("请先配置Git账户信息（用户名）。", "Git配置提示", MessageBoxButton.OK, MessageBoxImage.Information);
                                MenuGitAccountSettings_Click(this, new RoutedEventArgs()); 
                                if (string.IsNullOrWhiteSpace(_appSettings.GitUsername)) 
                                {
                                    SetMainUILoadingState(false);
                                    return; 
                                }
                            }
                            
                            _tempDirectoryPath = Path.Combine(Path.GetTempPath(), "FgccHelper_Temp_Git", Guid.NewGuid().ToString());
                            Directory.CreateDirectory(_tempDirectoryPath);
                            _currentProjectPath = _tempDirectoryPath;
                            
                            string cloneUrl = GitCloneWindow.BuildGitUrlWithCredentials(repoUrlToClone, _appSettings.GitUsername, _appSettings.GitPassword);
                            string gitArguments;
                            if (!string.IsNullOrEmpty(branchToClone))
                            {
                                gitArguments = $"clone --depth 1 -b \"{branchToClone}\" \"{cloneUrl}\" \"{_currentProjectPath}\"";
                            }
                            else
                            {
                                gitArguments = $"clone --depth 1 \"{cloneUrl}\" \"{_currentProjectPath}\"";
                            }

                            ProcessStartInfo gitInfo = new ProcessStartInfo("git", gitArguments) 
                            { 
                                CreateNoWindow = true, 
                                UseShellExecute = false, 
                                RedirectStandardOutput = true, 
                                RedirectStandardError = true 
                            };
                            
                            bool cloneSuccess = false;
                            StringBuilder output = new StringBuilder();
                            StringBuilder errorOutput = new StringBuilder();

                            await Task.Run(() =>
                            {
                                using (Process gitProcess = new Process { StartInfo = gitInfo })
                                {
                                    gitProcess.OutputDataReceived += (s, args) => { if (args.Data != null) output.AppendLine(args.Data); };
                                    gitProcess.ErrorDataReceived += (s, args) => { if (args.Data != null) errorOutput.AppendLine(args.Data); };
                                    gitProcess.Start();
                                    gitProcess.BeginOutputReadLine();
                                    gitProcess.BeginErrorReadLine();
                                    gitProcess.WaitForExit();
                                    cloneSuccess = gitProcess.ExitCode == 0;
                                }
                            });

                            if (cloneSuccess)
                            {
                                await LoadAndDisplayProject(_currentProjectPath);
                                if (_currentProject != null && _currentProject.ProjectName != "未选择工程")
                                {
                                    AddRecentProject(repoUrlToClone, ProjectType.GitRepo, branchToClone);
                                }
                            }
                            else
                            {
                                string errorMsg = "Git克隆失败。\n";
                                if (errorOutput.Length > 0) errorMsg += "错误信息:\n" + errorOutput.ToString();
                                else if (output.Length > 0) errorMsg += "输出信息:\n" + output.ToString();
                                else errorMsg += "未知Git错误。";
                                MessageBox.Show(errorMsg, "Git 克隆错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                CleanUpTempDirectory();
                                InitializeDefaultStatisticsDisplay();
                                _currentProjectPath = null;
                                _originalOpenedPath = repoUrlToClone;
                                UpdateWindowTitle();
                                 SetMainUILoadingState(false); // Explicitly set false on clone failure AFTER potential LoadProjectData
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // General error handling for opening recent project
                    Application.Current.Dispatcher.Invoke(() => {
                        MessageBox.Show($"打开最近项目 '{entry.DisplayName}' 时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        InitializeDefaultStatisticsDisplay();
                    });
                }
                finally
                {
                    Application.Current.Dispatcher.Invoke(() => SetMainUILoadingState(false));
                }
            }
        }

        private List<StatisticItem> GetInitialStatisticItems()
        {
            return new List<StatisticItem>
            {
                new StatisticItem { Name = "页面数量", Description = "工程中所有页面的总数量", Count = 0, Details = new List<DetailEntry>() },
                new StatisticItem { Name = "数据表数量", Description = "工程中所有数据表的总数量", Count = 0, Details = new List<DetailEntry>() },
                new StatisticItem { Name = "流程数量", Description = "工程中所有业务流程的总数量", Count = 0, Details = new List<DetailEntry>() },
                new StatisticItem { Name = "报表数量", Description = "工程中所有报表的总数量", Count = 0, Details = new List<DetailEntry>() },
                new StatisticItem { Name = "接口数量", Description = "工程中所有服务端命令（接口）的总数量", Count = 0, Details = new List<DetailEntry>() },
                new StatisticItem { Name = "自定义插件数量", Description = "工程中所有自定义插件 (.zip) 的总数量", Count = 0, Details = new List<DetailEntry>() },
                new StatisticItem { Name = "自定义组件数量", Description = "工程中所有自定义组件 (.json) 的总数量", Count = 0, Details = new List<DetailEntry>() },
                new StatisticItem { Name = "计划任务数量", Description = "工程中所有计划任务的总数量", Count = 0, Details = new List<DetailEntry>() },
                new StatisticItem { Name = "扩展JavaScript数量", Description = "工程中所有扩展JavaScript (.js) 文件的总数量", Count = 0, Details = new List<DetailEntry>() },
                new StatisticItem { Name = "外部JS文件数量", Description = "工程中引用的外部JavaScript文件的总数量", Count = 0, Details = new List<DetailEntry>() },
                new StatisticItem { Name = "外部CSS文件数量", Description = "工程中引用的外部CSS文件的总数量", Count = 0, Details = new List<DetailEntry>() }
            };
        }

        private void InitializeDefaultStatisticsDisplay()
        {
            CleanUpTempDirectory(true); 
            var initialProject = new Project
            {
                ProjectName = "未选择工程",
                DesignerVersion = "N/A",
                Statistics = GetInitialStatisticItems()
            };
            _currentProject = initialProject; 
            _currentProjectPath = null; 
            _originalOpenedPath = null;
            UpdateUIWithProjectData(initialProject, true); 
            this.Title = _baseTitle + " - 未选择工程";
            MenuRefresh.IsEnabled = false; // Initially no project, so refresh is disabled
            _selectedCardBorder = null; 
        }

        private async void MenuOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            CleanUpTempDirectory(true);
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "请选择活字格工程根目录";
            dialog.ShowNewFolderButton = false;
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string defaultPath = Path.Combine(documentsPath, "ForguncyCollaboration");
            if (Directory.Exists(defaultPath)) dialog.SelectedPath = defaultPath;
            
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _originalOpenedPath = dialog.SelectedPath; 
                _currentProjectPath = dialog.SelectedPath; 
                _isTempPath = false; 
                await LoadAndDisplayProject(_currentProjectPath);
                // Add to recent projects if successful (LoadAndDisplayProject doesn't throw)
                if (_currentProject != null && _currentProject.ProjectName != "未选择工程") // Check if project actually loaded
                {
                    AddRecentProject(_originalOpenedPath, ProjectType.Folder);
                }
            }
            Application.Current.Dispatcher.Invoke(() => SetMainUILoadingState(false));
        }

        private async void MenuOpenFgccFile_Click(object sender, RoutedEventArgs e)
        {
            CleanUpTempDirectory(true); 

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "活字格文件 (*.fgcc)|*.fgcc|所有文件 (*.*)|*.*";
            openFileDialog.Title = "选择活字格工程文件";

            if (openFileDialog.ShowDialog() == true)
            {
                string fgccFilePath = openFileDialog.FileName;
                _originalOpenedPath = fgccFilePath; 
                _isTempPath = true;
                
                SetMainUILoadingState(true);
                try
                {
                    await Task.Run(async () => // Run extraction and loading on a background thread
                    {
                        _tempDirectoryPath = Path.Combine(Path.GetTempPath(), "FgccHelper_Temp", Guid.NewGuid().ToString());
                        Directory.CreateDirectory(_tempDirectoryPath);
                        ZipFile.ExtractToDirectory(fgccFilePath, _tempDirectoryPath);
                        _currentProjectPath = _tempDirectoryPath; // Set current path to the temp directory
                        
                        // LoadProjectData is now async and handles its own UI thread marshalling for updates
                        await LoadProjectData(_currentProjectPath); 
                    });
                    // Add to recent projects if successful (LoadProjectData within Task.Run doesn't throw leading to overall success here)
                    if (_currentProject != null && _currentProject.ProjectName != "未选择工程")
                    {
                        AddRecentProject(fgccFilePath, ProjectType.FgccFile);
                    }
                }
                catch (Exception ex)
                {
                    // This catch is for exceptions during Task.Run or operations before it (like ShowDialog)
                    // Exceptions from LoadProjectData should be handled within LoadProjectData itself if they are specific to analysis
                    // Or rethrown and caught here if they are critical for the loading process.
                    Application.Current.Dispatcher.Invoke(() => {
                        MessageBox.Show($"打开或解压活字格文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        CleanUpTempDirectory(); 
                        InitializeDefaultStatisticsDisplay(); 
                        _currentProjectPath = null; 
                        // _originalOpenedPath = fgccFilePath; // Already set
                        UpdateWindowTitle(); 
                    });
                }
                finally
                {
                    Application.Current.Dispatcher.Invoke(() => SetMainUILoadingState(false)); // Ensure loading state is reset
                }
            }
        }

        private async void MenuOpenGitProject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_appSettings.GitUsername)) // No password check here, allow empty password
            {
                MessageBox.Show("请先配置Git账户信息（用户名）。", "Git配置提示", MessageBoxButton.OK, MessageBoxImage.Information);
                MenuGitAccountSettings_Click(this, new RoutedEventArgs()); // Show settings dialog
                if (string.IsNullOrWhiteSpace(_appSettings.GitUsername)) // Check again if user cancelled or didn't save
                {
                    return; // User did not configure, so abort opening Git project
                }
            }

            CleanUpTempDirectory(true);

            // GitCloneWindow will now only need URL and branch. Username/Password come from _appSettings.
            GitCloneWindow gitDialog = new GitCloneWindow(_appSettings.GitUsername, _appSettings.GitPassword);
            
            if (gitDialog.ShowDialog() == true)
            {
                string repoUrl = gitDialog.RepoUrl;
                string selectedBranch = gitDialog.SelectedBranch; 

                _originalOpenedPath = repoUrl;
                _isTempPath = true;

                _tempDirectoryPath = Path.Combine(Path.GetTempPath(), "FgccHelper_Temp", Guid.NewGuid().ToString());
                Directory.CreateDirectory(_tempDirectoryPath);
                _currentProjectPath = _tempDirectoryPath; 
                SetMainUILoadingState(true);
                
                string cloneUrl = GitCloneWindow.BuildGitUrlWithCredentials(repoUrl, _appSettings.GitUsername, _appSettings.GitPassword);

                string gitArguments;
                if (!string.IsNullOrEmpty(selectedBranch))
                {
                    gitArguments = $"clone --depth 1 -b \"{selectedBranch}\" \"{cloneUrl}\" \"{_currentProjectPath}\"";
                }
                else
                {
                    gitArguments = $"clone --depth 1 \"{cloneUrl}\" \"{_currentProjectPath}\"";
                }

                ProcessStartInfo gitInfo = new ProcessStartInfo("git", gitArguments);
                gitInfo.CreateNoWindow = true;
                gitInfo.UseShellExecute = false;
                gitInfo.RedirectStandardOutput = true;
                gitInfo.RedirectStandardError = true;
                
                StringBuilder output = new StringBuilder();
                StringBuilder errorOutput = new StringBuilder();
                bool cloneSuccess = false;

                try
                {
                    await Task.Run(() =>
                    {
                        using (Process gitProcess = new Process { StartInfo = gitInfo })
                        {
                            gitProcess.OutputDataReceived += (s, args) => { if(args.Data != null) output.AppendLine(args.Data); };
                            gitProcess.ErrorDataReceived += (s, args) => { if(args.Data != null) errorOutput.AppendLine(args.Data); };
                            
                            gitProcess.Start();
                            gitProcess.BeginOutputReadLine();
                            gitProcess.BeginErrorReadLine();
                            gitProcess.WaitForExit(); 
                            cloneSuccess = gitProcess.ExitCode == 0;
                        }
                    });

                    if (cloneSuccess)
                    {
                        await LoadAndDisplayProject(_currentProjectPath);
                        if (_currentProject != null && _currentProject.ProjectName != "未选择工程")
                        {
                             AddRecentProject(repoUrl, ProjectType.GitRepo, selectedBranch);
                        }
                    }
                    else
                    {
                        string errorMsg = "Git克隆失败。\n";
                        if (errorOutput.Length > 0) errorMsg += "错误信息:\n" + errorOutput.ToString();
                        else if (output.Length > 0) errorMsg += "输出信息:\n" + output.ToString();
                        else errorMsg += "未知Git错误。";
                        MessageBox.Show(errorMsg, "Git 克隆错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        CleanUpTempDirectory();
                        InitializeDefaultStatisticsDisplay();
                        _currentProjectPath = null;
                        _originalOpenedPath = repoUrl;
                        UpdateWindowTitle();
                         SetMainUILoadingState(false); // Explicitly set false on clone failure AFTER potential LoadProjectData
                    }
                }
                catch (Exception ex) 
                {
                    MessageBox.Show($"执行Git克隆时发生错误: {ex.Message}", "执行错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    CleanUpTempDirectory();
                    InitializeDefaultStatisticsDisplay();
                    _currentProjectPath = null;
                    _originalOpenedPath = repoUrl;
                    UpdateWindowTitle();
                    SetMainUILoadingState(false); 
                }
                finally
                {
                    // If LoadProjectData is called and it's synchronous, its finally block handles SetMainUILoadingState(false).
                    // If clone failed before LoadProjectData, it's handled above.
                    // If LoadProjectData becomes async, it must handle SetMainUILoadingState(false) itself.
                    // This is a final check, primarily for if cloneSuccess but LoadProjectData failed early (if it were async).
                    if (!cloneSuccess) // If clone failed, we've already set loading state to false.
                    {
                       // SetMainUILoadingState(false); // Already handled in the else block of cloneSuccess check
                    }
                    // If LoadProjectData is still sync and called, its finally block will handle it.
                }
            }
            else 
            {
                 _originalOpenedPath = null; 
                 // If dialog is cancelled, ensure loading state is false if it was somehow set true before.
                 if (LoadingOverlay.Visibility == Visibility.Visible) SetMainUILoadingState(false);
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MenuGitAccountSettings_Click(object sender, RoutedEventArgs e)
        {
            // Pass current settings to the dialog
            GitAccountSettingsWindow settingsDialog = new GitAccountSettingsWindow(_appSettings.GitUsername, _appSettings.GitPassword);
            if (settingsDialog.ShowDialog() == true)
            {
                _appSettings.GitUsername = settingsDialog.Username;
                _appSettings.GitPassword = settingsDialog.Password;
                SaveAppSettings();
                MessageBox.Show("Git账户信息已保存。", "配置成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow aboutDialog = new AboutWindow();
            aboutDialog.ShowDialog();
        }

        private async void MenuRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentProjectPath) && string.IsNullOrEmpty(_originalOpenedPath))
            {
                MessageBox.Show("没有已加载的工程可以刷新。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Determine project type for refresh logic
            // Simplified: if _isTempPath is true, it's either an FGCC or Git that needs re-extraction/re-clone.
            // If _isTempPath is false, it's a direct folder, just re-analyze.

            SetMainUILoadingState(true);
            try
            {
                if (_isTempPath && !string.IsNullOrEmpty(_originalOpenedPath))
                {
                    // This implies it was an FGCC or Git project.
                    // We need to identify which one to re-process.
                    // For simplicity, let's assume _originalOpenedPath helps distinguish.
                    // A more robust way would be to store the type of project.
                    
                    CleanUpTempDirectory(true); // Clean up old temp stuff first

                    if (_originalOpenedPath.EndsWith(".fgcc", StringComparison.OrdinalIgnoreCase))
                    {
                        // Re-extract FGCC
                        _tempDirectoryPath = Path.Combine(Path.GetTempPath(), "FgccHelper_Temp", Guid.NewGuid().ToString());
                        Directory.CreateDirectory(_tempDirectoryPath);
                        
                        await Task.Run(() => ZipFile.ExtractToDirectory(_originalOpenedPath, _tempDirectoryPath));
                        _currentProjectPath = _tempDirectoryPath;
                    }
                    else if (_originalOpenedPath.StartsWith("http", StringComparison.OrdinalIgnoreCase) || _originalOpenedPath.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
                    {
                        // Re-clone Git Project
                        // This requires re-showing GitCloneWindow or having a non-interactive clone method.
                        // For now, let's prompt again or use stored credentials if available.
                         if (string.IsNullOrWhiteSpace(_appSettings.GitUsername))
                        {
                            MessageBox.Show("请先配置Git账户信息（用户名）。", "Git配置提示", MessageBoxButton.OK, MessageBoxImage.Information);
                            MenuGitAccountSettings_Click(this, new RoutedEventArgs());
                            if (string.IsNullOrWhiteSpace(_appSettings.GitUsername))
                            {
                                SetMainUILoadingState(false);
                                return; 
                            }
                        }

                        _tempDirectoryPath = Path.Combine(Path.GetTempPath(), "FgccHelper_Temp_Git", Guid.NewGuid().ToString());
                        Directory.CreateDirectory(_tempDirectoryPath); // Create parent for clone

                        string gitUrlWithCreds = GitCloneWindow.BuildGitUrlWithCredentials(_originalOpenedPath, _appSettings.GitUsername, _appSettings.GitPassword);
                        // Assuming no specific branch for refresh, or need to store branch if selected previously.
                        // For simplicity, cloning default branch.
                        string cloneCommand = $"clone --depth 1 \"{gitUrlWithCreds}\" \"{_tempDirectoryPath}\"";
                        
                        bool cloneSuccess = false;
                        await Task.Run(() =>
                        {
                            ProcessStartInfo gitInfo = new ProcessStartInfo("git", cloneCommand)
                            {
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                // WorkingDirectory = _tempDirectoryPath // Not needed as full path is in clone command
                            };
                            using (Process gitProcess = Process.Start(gitInfo))
                            {
                                // Consider async reading of output/error if it's very verbose
                                string output = gitProcess.StandardOutput.ReadToEnd();
                                string error = gitProcess.StandardError.ReadToEnd();
                                gitProcess.WaitForExit();
                                if (gitProcess.ExitCode == 0)
                                {
                                    cloneSuccess = true;
                                }
                                else
                                {
                                    // Dispatch UI updates to the main thread
                                    Dispatcher.Invoke(() => {
                                        MessageBox.Show($"Git重新克隆失败: {error}\n{output}", "Git错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                    });
                                }
                            }
                        });

                        if (!cloneSuccess) {
                            CleanUpTempDirectory();
                            InitializeDefaultStatisticsDisplay();
                            SetMainUILoadingState(false);
                            return;
                        }
                        _currentProjectPath = _tempDirectoryPath;
                    }
                    else
                    {
                        // Should not happen if _isTempPath is true with a valid _originalOpenedPath
                        MessageBox.Show("无法确定临时项目的来源，无法刷新。", "刷新错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        SetMainUILoadingState(false);
                        return;
                    }
                }
                // After potential re-extraction or re-clone, or if it's a direct folder, load data.
                if (!string.IsNullOrEmpty(_currentProjectPath))
                {
                    await LoadProjectData(_currentProjectPath);
                }
                else
                {
                     // This case should ideally be caught earlier.
                    MessageBox.Show("项目路径无效，无法刷新。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    InitializeDefaultStatisticsDisplay(); // Reset if path is lost
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新工程失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                // Consider if InitializeDefaultStatisticsDisplay() is always appropriate here
                // If a project was loaded, and refresh fails, user might want to see old data.
                // For now, we'll keep the UI as is, but stop loading indication.
            }
            finally
            {
                SetMainUILoadingState(false);
            }
        }

        private void UpdateWindowTitle(Project project = null)
        {
            string title = _baseTitle;
            string pathToShow = _isTempPath ? _originalOpenedPath : _currentProjectPath; 

            if (project != null && !string.IsNullOrEmpty(project.ProjectName) && project.ProjectName != "未选择工程")
            {
                title += $" - {project.ProjectName}";
                if (!string.IsNullOrEmpty(project.DesignerVersion) && project.DesignerVersion != "N/A")
                {
                    title += $" ({project.DesignerVersion})";
                }
                if (!string.IsNullOrEmpty(pathToShow))
                {
                    title += $" [{Path.GetFileName(pathToShow)}]"; 
                }
            }
            else if (!string.IsNullOrEmpty(pathToShow))
            {
                 title += $" - [{Path.GetFileName(pathToShow)}] (加载中/失败)";
            }
            else
            {
                title += " - 未选择工程";
            }
            this.Title = title;
        }

        private async Task LoadAndDisplayProject(string projectPath)
        {
            SetMainUILoadingState(true);
            try
            {
                await LoadProjectData(projectPath);
            }
            catch (Exception ex) 
            {
                Application.Current.Dispatcher.Invoke(() => {
                    MessageBox.Show($"加载工程数据时发生错误: {ex.Message}", "加载错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    InitializeDefaultStatisticsDisplay(); // Reset to default state on critical load failure
                });
                // _currentProjectPath might be invalid or point to a problematic project
            }
            finally
            {
                Application.Current.Dispatcher.Invoke(() => SetMainUILoadingState(false));
            }
        }

        private async Task LoadProjectData(string projectPathToAnalyze) 
        {
            Project analysisResult = null;
            Exception analysisException = null;

            try
            {
                // Perform the potentially long-running analysis on a background thread
                analysisResult = await Task.Run(() => _analysisService.AnalyzeProject(projectPathToAnalyze));
            }
            catch (Exception ex)
            {
                analysisException = ex; // Capture any exception from the analysis task
            }

            // Now, dispatch all UI updates and result handling to the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (analysisException != null)
                {
                    // Handle exception from the analysis task
                    MessageBox.Show($"分析工程 '{Path.GetFileName(projectPathToAnalyze)}' 时发生错误: {analysisException.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    InitializeDefaultStatisticsDisplay(); // This also updates UI, will run on UI thread.
                    _currentProject = null; // Ensure state is clean
                    _currentProjectPath = null;
                }
                else if (analysisResult != null)
                {
                    // Analysis succeeded and returned a project
                    _currentProject = analysisResult;
                    UpdateUIWithProjectData(_currentProject); // UI update
                    MenuRefresh.IsEnabled = true;             // UI update
                    MenuExportExcel.IsEnabled = true;       // Enable export

                    if (_currentProject.Statistics != null && _currentProject.Statistics.Any())
                    {
                        var firstCard = StatisticsWrapPanel.Children.OfType<Border>().FirstOrDefault(); // UI access
                        if (firstCard != null)
                        {
                            SelectCard(firstCard); // UI update
                        }
                    }
                    else
                    {
                        DetailsListView.ItemsSource = null; // UI update
                        _selectedCardBorder = null;
                    }
                }
                else
                {
                    // Analysis completed without exception, but returned null (e.g., invalid project structure)
                    MessageBox.Show("无法分析工程或工程数据为空。", "分析错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    InitializeDefaultStatisticsDisplay(); // UI update
                    _currentProject = null; // Ensure consistent state if project was previously loaded
                    MenuExportExcel.IsEnabled = false; // Disable export
                }
                UpdateWindowTitle(_currentProject); // Update title in all cases on UI thread
            });
        }

        private void UpdateUIWithProjectData(Project project, bool isInitialLoad = false)
        {
            _selectedCardBorder = null; 
            if (project == null && !isInitialLoad) return;
            if (project == null && isInitialLoad) 
            {
                 project = new Project { ProjectName="Error", Statistics = GetInitialStatisticItems() };
            }

            if (!isInitialLoad) 
            {
                 UpdateWindowTitle(project);
            }
            
            StatisticsWrapPanel.Children.Clear();
            if (!isInitialLoad) 
            {
                DetailsListView.ItemsSource = null;
                DetailsGroupBox.Header = "详细信息";
            }
            

            if (project.Statistics == null || !project.Statistics.Any())
            {
                var noDataLabel = new TextBlock
                {
                    Text = isInitialLoad ? "请选择一个工程目录开始分析。" : "没有可显示的统计数据。",
                    Margin = new Thickness(10),
                    Padding = new Thickness(5),
                    FontStyle = FontStyles.Italic,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                StatisticsWrapPanel.Children.Add(noDataLabel);
                MenuRefresh.IsEnabled = false; // No data, disable refresh if this was the result of a load
                if (!isInitialLoad) MenuExportExcel.IsEnabled = project.Statistics != null && project.Statistics.Any(); // Enable/disable based on actual data for non-initial loads
                else MenuExportExcel.IsEnabled = false; // Always disable on initial (empty) load
                return;
            }
            MenuRefresh.IsEnabled = true; // Has data, enable refresh

            Border firstCard = null;
            foreach (var statItem in project.Statistics)
            {
                var cardBorder = new Border
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6), 
                    Padding = new Thickness(10),      
                    Margin = new Thickness(7),        
                    MinWidth = 190,                   
                    MinHeight = 75,                   
                    Background = _normalCardBackground,       
                    Effect = new System.Windows.Media.Effects.DropShadowEffect 
                    {
                        Color = Colors.Gainsboro,
                        Direction = 315,
                        ShadowDepth = 2,
                        Opacity = 0.25,
                        BlurRadius = 4
                    }
                };
                
                var mainStackPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                var nameTextBlock = new TextBlock
                {
                    Text = statItem.Name,
                    FontWeight = FontWeights.Normal, 
                    FontSize = 14, 
                    ToolTip = statItem.Description,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0,0,0,3) 
                };

                var countTextBlock = new TextBlock
                {
                    Text = statItem.Count.ToString(),
                    FontSize = 22, 
                    FontWeight = FontWeights.SemiBold, 
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Foreground = Brushes.DarkSlateGray 
                };
                
                mainStackPanel.Children.Add(nameTextBlock);
                mainStackPanel.Children.Add(countTextBlock);
                cardBorder.Child = mainStackPanel;
                cardBorder.Tag = statItem;
                
                cardBorder.MouseEnter += Card_MouseEnter;
                cardBorder.MouseLeave += Card_MouseLeave;
                cardBorder.MouseLeftButtonUp += StatisticCard_MouseLeftButtonUp;

                StatisticsWrapPanel.Children.Add(cardBorder);
                if (firstCard == null) firstCard = cardBorder;
            }

            if (!isInitialLoad && firstCard != null)
            {
                SelectCard(firstCard);
                if (firstCard.Tag is StatisticItem firstStatItem)
                {
                     DetailsGroupBox.Header = $"详细信息 - {firstStatItem.Name} ({firstStatItem.Count} 项)";
                     DetailsListView.ItemsSource = firstStatItem.Details;
                }
            }
        }

        private void Card_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border card && card != _selectedCardBorder)
            {
                card.Background = _hoverCardBackground;
            }
        }

        private void Card_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border card && card != _selectedCardBorder)
            {
                card.Background = _normalCardBackground;
            }
        }
        
        private void SelectCard(Border cardToSelect)
        {
            if (_selectedCardBorder != null && _selectedCardBorder != cardToSelect)
            {
                _selectedCardBorder.Background = _normalCardBackground;
            }

            if (cardToSelect != null) 
            {
                 cardToSelect.Background = _selectedCardBackground;
                _selectedCardBorder = cardToSelect;
            }
        }

        private void StatisticCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border card && card.Tag is StatisticItem statItem)
            {
                SelectCard(card);
                DetailsGroupBox.Header = $"详细信息 - {statItem.Name} ({statItem.Count} 项)";
                DetailsListView.ItemsSource = statItem.Details;
            }
        }

        private async void MenuExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_currentProject == null || _currentProject.ProjectName == "未选择工程")
            {
                MessageBox.Show("请先加载一个工程，然后再导出数据。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Excel 工作簿 (*.xlsx)|*.xlsx";
            saveFileDialog.Title = "导出工程统计信息为Excel";
            saveFileDialog.FileName = $"{_currentProject.ProjectName}_Statistics.xlsx"; // Default file name

            if (saveFileDialog.ShowDialog() == true)
            {
                string filePath = saveFileDialog.FileName;
                SetMainUILoadingState(true);
                try
                {
                    await _excelExportService.ExportProjectToExcelAsync(_currentProject, filePath);
                    MessageBox.Show($"工程统计信息已成功导出到:\n{filePath}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出Excel文件失败: {ex.Message}", "导出错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    SetMainUILoadingState(false);
                }
            }
        }
    }
}


