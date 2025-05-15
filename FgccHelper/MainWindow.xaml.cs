using FgccHelper.Models;
// using FgccHelper.Services; // Will fully qualify LeaderboardApiService
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
using System.Text.RegularExpressions; // Required for Git project name extraction improvement
using System.Collections.ObjectModel;
using FgccHelper.Services; // Added

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
        public string AuthorName { get; set; } // User's preferred author name
        public string Email { get; set; }      // User's preferred email
    }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private ProjectAnalysisService _analysisService;
        private ExcelExportService _excelExportService; // Added for Excel export
        private FgccHelper.Services.LeaderboardApiService _leaderboardApiService; // Fully qualified
        private Project _currentProject;
        private string _currentProjectPath; // This will now store the actual path being analyzed (original or temp)
        private string _originalOpenedPath; // For fgcc or git, this is the fgcc file or git url
        private bool _isTempPath = false;
        private string _tempDirectoryPath = null; // Stores path to the temporary directory for .fgcc or git
        private ObservableCollection<FgccHelper.Models.RankingEntry> _leaderboardRankings; // Fully qualified generic type

        private string _baseTitle = "FgccHelper";

        // For card styling
        private Border _selectedCardBorder = null;
        private readonly Brush _normalCardBackground = Brushes.White;
        private readonly Brush _hoverCardBackground = new SolidColorBrush(Color.FromArgb(255, 220, 235, 255)); // Light AliceBlue like
        private readonly Brush _selectedCardBackground = new SolidColorBrush(Color.FromArgb(255, 190, 220, 255)); // Slightly darker blue

        private AppSettings _appSettings;
        private readonly string _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        // Leaderboard pagination and loading state
        private int _currentLeaderboardPage = 1;
        private bool _isLoadingLeaderboard = false;
        private bool _hasMoreLeaderboardData = true;

        // Helper method to find a visual child of a specific type
        public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            T child = default(T);
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null)
                {
                    child = FindVisualChild<T>(v);
                }
                if (child != null)
                {
                    break;
                }
            }
            return child;
        }

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
            _leaderboardApiService = new FgccHelper.Services.LeaderboardApiService(); // Fully qualified instantiation
            _leaderboardRankings = new ObservableCollection<FgccHelper.Models.RankingEntry>(); // Fully qualified generic type
            RankingsListView.ItemsSource = _leaderboardRankings; // Bind to ListView in XAML

            InitializeDefaultStatisticsDisplay(); 
            UpdateRecentProjectsMenu(); // Initial population of the recent projects menu
            this.Closing += MainWindow_Closing; 
            _selectedCardBorder = null; 
            MenuExportExcel.IsEnabled = false; // Disable export if no project loaded initially

            // Load leaderboard on startup
            Loaded += async (s, e) => 
            {
                await RefreshLeaderboardDataAsync(isManualRefresh: true); // Changed from isInitialLoad
                // SetupLeaderboardRefreshTimer(); // REMOVED: Timer is removed
            };
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

        public void SaveAppSettings()
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
                            if (_currentProject != null) // Check if load was successful
                            {
                                _currentProject.ProjectName = System.IO.Path.GetFileNameWithoutExtension(entry.Path); // Set ProjectName from fgcc file
                                UpdateWindowTitle(_currentProject); // Explicitly update window title
                                if (_currentProject.ProjectName != "未选择工程") // Additional check if still default name
                                {
                                   AddRecentProject(entry.Path, ProjectType.FgccFile);
                                }
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
                Statistics = new System.Collections.ObjectModel.ObservableCollection<StatisticItem>(GetInitialStatisticItems()) 
            };
            _currentProject = initialProject; 
            _currentProjectPath = null; 
            _originalOpenedPath = null;
            UpdateUIWithProjectData(initialProject, true); 
            this.Title = _baseTitle + " - 未选择工程";
            MenuRefresh.IsEnabled = false; 
            MenuShareToLeaderboard.IsEnabled = false; // 禁用分享菜单项
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
                        
                        await LoadProjectData(_currentProjectPath); 
                    });
                    
                    if (_currentProject != null) // After LoadProjectData completes and _currentProject is set
                    {
                        _currentProject.ProjectName = System.IO.Path.GetFileNameWithoutExtension(fgccFilePath); // Override with fgcc file name
                        UpdateWindowTitle(_currentProject); // Explicitly update window title
                         if (_currentProject.ProjectName != "未选择工程") // Ensure it wasn't reset to default
                        {
                            AddRecentProject(fgccFilePath, ProjectType.FgccFile);
                        }
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
                await RefreshLeaderboardDataAsync(isManualRefresh: true); // Add leaderboard refresh to manual project refresh
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
            ProjectType projectType = ProjectType.Folder; // Default
            if (_isTempPath && !string.IsNullOrEmpty(_originalOpenedPath))
            {
                if (_originalOpenedPath.EndsWith(".fgcc", StringComparison.OrdinalIgnoreCase)) projectType = ProjectType.FgccFile;
                else if (_originalOpenedPath.StartsWith("http", StringComparison.OrdinalIgnoreCase) || 
                         _originalOpenedPath.StartsWith("git@", StringComparison.OrdinalIgnoreCase)) projectType = ProjectType.GitRepo;
            }

            try
            {
                analysisResult = await Task.Run(() => _analysisService.AnalyzeProject(projectPathToAnalyze));
                if (analysisResult != null) 
                { 
                    analysisResult.ProjectType = projectType; // Set the project type
                }
            }
            catch (Exception ex)
            {
                analysisException = ex; 
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (analysisException != null)
                {
                    MessageBox.Show($"分析工程 '{Path.GetFileName(projectPathToAnalyze)}' 时发生错误: {analysisException.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    InitializeDefaultStatisticsDisplay(); 
                    _currentProject = null; 
                    _currentProjectPath = null;
                     MenuShareToLeaderboard.IsEnabled = false; // 禁用分享菜单项
                }
                else if (analysisResult != null)
                {
                    _currentProject = analysisResult;
                    UpdateUIWithProjectData(_currentProject, true); 
                    MenuRefresh.IsEnabled = true;             
                    MenuExportExcel.IsEnabled = true;       
                    // MenuShareToLeaderboard.IsEnabled = true; // REMOVED: Logic moved to UpdateUIWithProjectData
                }
                else
                {
                    MessageBox.Show("无法分析工程或工程数据为空。", "分析错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    InitializeDefaultStatisticsDisplay(); 
                    _currentProject = null; 
                    MenuExportExcel.IsEnabled = false; 
                    MenuShareToLeaderboard.IsEnabled = false; // 禁用分享菜单项
                }
                UpdateWindowTitle(_currentProject); 
            });
        }

        private void UpdateUIWithProjectData(Project project, bool isInitialLoad = false)
        {
            SetMainUILoadingState(true); // Start loading UI state

            _currentProject = project;
            MenuShareToLeaderboard.IsEnabled = false; // Default to false, enable only after successful complexity calculation > 0

            if (_currentProject == null)
            {
                // ProjectNameTextBlock.Text = "未加载项目"; // Removed
                // DesignerVersionTextBlock.Text = "设计器版本: --"; // Removed
                ItemsControlStatistics.ItemsSource = null;
                DetailsListView.ItemsSource = null;
                // ProjectInfoPanel.Visibility = Visibility.Collapsed; // Removed
                MenuExportExcel.IsEnabled = false;
                SetMainUILoadingState(false); 
                return;
            }
            
            // ProjectNameTextBlock.Text = string.IsNullOrWhiteSpace(_currentProject.ProjectName) ? "(未命名项目)" : _currentProject.ProjectName; // Removed
            // DesignerVersionTextBlock.Text = string.IsNullOrWhiteSpace(_currentProject.DesignerVersion) ? "设计器版本: 未知" : $"设计器版本: {_currentProject.DesignerVersion}"; // Removed
            // ProjectInfoPanel.Visibility = Visibility.Visible; // Removed

            // --- 集成复杂度计算 ---
            const string complexityItemName = "项目复杂度"; 
            var complexityStatItem = _currentProject.Statistics.FirstOrDefault(s => s.Name == complexityItemName);

            if (isInitialLoad || complexityStatItem == null) // Always add/reset on initial load or if not present
            {
                if (complexityStatItem != null)
                {
                    // 如果是刷新，并且项已存在，先移除旧的再添加新的，确保它在列表末尾或者一个可预期的位置
                    // 或者直接更新现有项的状态
                    complexityStatItem.Description = "计算中...";
                    complexityStatItem.Count = 0; // Reset
                }
                else
                {
                    complexityStatItem = new StatisticItem
                    {
                        Name = complexityItemName,
                        Count = 0, 
                        Description = "计算中..." 
                    };
                    // 将复杂度项添加到列表的末尾，或者一个合适的位置
                    // 如果希望它在特定位置，需要更复杂的插入逻辑
                    _currentProject.Statistics.Add(complexityStatItem);
                }
            }
            else // For refresh, just update existing item to "Calculating..."
            {
                 complexityStatItem.Description = "计算中...";
                 complexityStatItem.Count = 0; // Reset
            }
            
            // 现在绑定 ItemsSource。由于 Statistics 是 ObservableCollection，
            // 之后对 complexityStatItem 属性的更改会通过 INotifyPropertyChanged 更新UI。
            // 如果 ItemsControlStatistics 在 XAML 中已绑定到 _currentProject.Statistics，则此行可能不需要显式调用，
            // 但为了确保，特别是在 isInitialLoad 时，可以设置。
            // 如果_currentProject本身是INotifyPropertyChanged并且Statistics属性触发了通知，则更好。
            // 但我们已经将Statistics改为了ObservableCollection，所以对集合的Add/Remove会通知，
            // 对StatisticItem内部属性的更改会通过StatisticItem的INotifyPropertyChanged通知。
            ItemsControlStatistics.ItemsSource = _currentProject.Statistics;


            if (_currentProject.Statistics.Any())
            {
                // 尝试选中第一个卡片（如果之前没有选中的话）
                // 或者，如果之前有选中的卡片，并且刷新后该卡片依然存在，则尝试恢复选中
                // 为了简单起见，这里默认不自动选中，或者选中第一个
                if (ItemsControlStatistics.Items.Count > 0 && _selectedCardBorder == null)
                {
                    // This logic might need adjustment based on how cards are generated and accessed
                    // SelectCard(ItemsControlStatistics.Items[0] as Border); // Example, likely needs refinement
                }
                DetailsListView.ItemsSource = null; // Clear details initially
                LabelNoDetails.Visibility = Visibility.Visible;
                LabelNoDetails.Text = "请选择一个统计卡片查看详细信息";
            }
            else
            {
                DetailsListView.ItemsSource = null;
                LabelNoDetails.Visibility = Visibility.Visible;
                LabelNoDetails.Text = "当前项目无统计信息";
            }
            
            UpdateWindowTitle(_currentProject);
            MenuExportExcel.IsEnabled = _currentProject != null && _currentProject.Statistics.Any();
            
            Dispatcher.InvokeAsync(async () => {
                SetMainUILoadingState(false); 

                var itemToUpdate = _currentProject.Statistics.FirstOrDefault(s => s.Name == complexityItemName);

                if (itemToUpdate != null)
                {
                    try
                    {
                        int score = await Services.ComplexityCalculator.CalculateComplexityAsync(_currentProject);
                        itemToUpdate.Count = score;
                        _currentProject.ComplexityScore = score; //确保更新 Project 对象自身的复杂度评分
                        itemToUpdate.Description = $"综合评分"; 
                        if (_currentProject.ComplexityScore > 0)
                        {
                            MenuShareToLeaderboard.IsEnabled = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (itemToUpdate != null) 
                        {                            
                            itemToUpdate.Description = "计算失败!";
                            itemToUpdate.Count = -1; 
                            _currentProject.ComplexityScore = -1; // 也更新 Project 对象的评分以反映失败
                        }
                        MessageBox.Show($"计算项目复杂度时发生错误: {ex.Message}", "复杂度计算失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        Console.WriteLine($"Complexity calculation error: {ex.Message}");
                    }
                }
            });
        }

        private void Card_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is ContentPresenter cp)
            {
                Border cardBorder = FindVisualChild<Border>(cp);
                if (cardBorder != null && cardBorder != _selectedCardBorder)
                {
                    cardBorder.Background = _hoverCardBackground;
                }
            }
        }

        private void Card_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is ContentPresenter cp)
            {
                Border cardBorder = FindVisualChild<Border>(cp);
                if (cardBorder != null && cardBorder != _selectedCardBorder)
                {
                    cardBorder.Background = _normalCardBackground;
                }
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
            if (sender is ContentPresenter cp && cp.Content is StatisticItem statItem)
            {
                Border cardBorder = FindVisualChild<Border>(cp);
                if (cardBorder != null)
                {
                    SelectCard(cardBorder);
                    DetailsGroupBox.Header = $"详细信息 - {statItem.Name} ({statItem.Count} 项)";
                    DetailsListView.ItemsSource = statItem.Details;
                    LabelNoDetails.Visibility = statItem.Details.Any() ? Visibility.Collapsed : Visibility.Visible;
                }
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

        private async void MenuShareToLeaderboard_Click(object sender, RoutedEventArgs e)
        {
            if (_currentProject == null || _currentProject.ProjectStats == null) 
            {
                MessageBox.Show("请先打开并成功分析一个活字格工程，然后再尝试分享。\n(工程统计数据 ProjectStats 未找到)", "无工程数据", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string effectiveProjectName = string.Empty;

            // Priority 1: Use ProjectName from analysis if valid
            if (!string.IsNullOrEmpty(_currentProject.ProjectName) && _currentProject.ProjectName != "未选择工程")
            {
                effectiveProjectName = _currentProject.ProjectName;
            }
            else // Fallback logic if ProjectName from analysis is not suitable
            {
                if (_currentProject.ProjectType == FgccHelper.ProjectType.GitRepo && !string.IsNullOrEmpty(_originalOpenedPath))
                {
                    try
                    {
                        Uri uri = new Uri(_originalOpenedPath);
                        string repoNameFromUri = uri.Segments.LastOrDefault()?.TrimEnd('/');
                        if (repoNameFromUri != null && repoNameFromUri.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                        {
                            repoNameFromUri = repoNameFromUri.Substring(0, repoNameFromUri.Length - 4);
                        }
                        if (!string.IsNullOrWhiteSpace(repoNameFromUri)) effectiveProjectName = repoNameFromUri;
                    }
                    catch 
                    { 
                        // If URI parsing fails or repoNameFromUri is empty, try from _currentProjectPath next
                    }
                }

                // If still empty (e.g., not Git or Git name extraction failed), try from current project path (could be temp path)
                if (string.IsNullOrEmpty(effectiveProjectName) && !string.IsNullOrEmpty(_currentProjectPath))
                {
                    try { effectiveProjectName = new DirectoryInfo(_currentProjectPath).Name; }
                    catch { effectiveProjectName = System.IO.Path.GetFileNameWithoutExtension(_currentProjectPath); }
                }
            }

            string designerVersion = _currentProject.DesignerVersion ?? "N/A"; 
            string complexityScoreStr = _currentProject.ComplexityScore.ToString();

            var submitWindow = new FgccHelper.SubmitToLeaderboardWindow(_currentProject.ProjectStats, 
                                                             complexityScoreStr, 
                                                             effectiveProjectName, 
                                                             designerVersion
                                                            )
            {
                Owner = this
            };

            bool? dialogResult = submitWindow.ShowDialog();

            if (dialogResult == true)
            {
                await RefreshLeaderboardDataAsync(isManualRefresh: true); // Refresh after submission
            }
        }

        private async Task RefreshLeaderboardDataAsync(bool isManualRefresh = false)
        {
            if (_isLoadingLeaderboard) return;
            if (!isManualRefresh && !_hasMoreLeaderboardData) return;

            _isLoadingLeaderboard = true;
            RankingEntry loadingIndicator = null;

            if (isManualRefresh)
            {
                _currentLeaderboardPage = 1;
                _hasMoreLeaderboardData = true;
                _leaderboardRankings.Clear();
                // Optional: Add a temporary loading item for manual refresh
                loadingIndicator = new FgccHelper.Models.RankingEntry { Rank = 0, ProjectName = "正在刷新排行榜...", ComplexityScore = 0, Author = "-", DominanceDuration = 0 };
                _leaderboardRankings.Add(loadingIndicator);
            }
            else if (_hasMoreLeaderboardData) // For scroll-loading, add indicator if not manual refresh
            {
                 loadingIndicator = new FgccHelper.Models.RankingEntry { Rank = 0, ProjectName = "加载更多...", ComplexityScore = 0, Author = "-", DominanceDuration = 0 };
                _leaderboardRankings.Add(loadingIndicator); // Add indicator for append
            }

            try
            {
                FgccHelper.Models.GetLeaderboardApiResponse response = await _leaderboardApiService.GetLeaderboardAsync(page: _currentLeaderboardPage);

                if (loadingIndicator != null && _leaderboardRankings.Contains(loadingIndicator))
                {
                    _leaderboardRankings.Remove(loadingIndicator); // Remove indicator before adding new items or final message
                }
                if (isManualRefresh && response.ErrorCode == 0) // Clear again if manual refresh and successful, to remove potential "loading" message added before try block if not using indicator.
                {
                     // If not using the indicator for manual refresh, ensure list is clear.
                     // With indicator, it's already removed.
                     // If manual refresh, we start with a clean slate before adding new items.
                     // This was: _leaderboardRankings.Clear(); -- but this is already done before the try if using an indicator and isManualRefresh
                     // If manual refresh, we want ONLY the new data or "no data" message.
                    if(_leaderboardRankings.Any() && isManualRefresh) _leaderboardRankings.Clear();


                }


                if (response.ErrorCode == 0 && response.Data != null)
                {
                    if (response.Data.Any())
                    {
                        int currentMaxRank = 0;
                        if (!isManualRefresh && _leaderboardRankings.Any())
                        {
                            currentMaxRank = _leaderboardRankings.Max(r => r.Rank);
                        }
                        
                        foreach (var entry in response.Data)
                        {
                            // If server provides rank, use it. Otherwise, assign client-side.
                            // For append, Rank should be relative to existing items or server should handle absolute rank.
                            // For now, if rank is 0, and we are appending, continue from last known rank.
                            if (entry.Rank == 0) 
                            {
                                if (isManualRefresh) entry.Rank = _leaderboardRankings.Count + 1; // Simple incremental rank for manual refresh
                                else entry.Rank = currentMaxRank + (_leaderboardRankings.Count(r=>r!=loadingIndicator) - _leaderboardRankings.IndexOf(entry) +1); //This logic needs to be robust
                                // A simpler way for append is just to add and let server handle ranks, or have client re-number all if needed.
                                // For now, let's assume server provides ranks or client simple appends.
                                // If server gives rank 0, assign based on current count for simplicity for now.
                                entry.Rank = _leaderboardRankings.Count +1;


                            }
                            _leaderboardRankings.Add(entry);
                        }
                        _currentLeaderboardPage++;
                        _hasMoreLeaderboardData = response.Data.Count == 20; // Assuming 20 items per page
                    }
                    else // No data in response.Data
                    {
                        _hasMoreLeaderboardData = false;
                        if (_currentLeaderboardPage == 1 && isManualRefresh) // Only show "No data" if it's the first page of a manual refresh
                        {
                            _leaderboardRankings.Add(new FgccHelper.Models.RankingEntry { Rank = 0, ProjectName = "排行榜暂无数据", ComplexityScore = 0, Author = "-", DominanceDuration = 0 });
                        }
                    }
                }
                else // API error or null data
                {
                    _hasMoreLeaderboardData = false;
                    if (isManualRefresh || !_leaderboardRankings.Any()) // Show error if manual refresh or list is empty
                    {
                         _leaderboardRankings.Clear(); // Clear previous items on error for manual refresh
                        string errorMessage = string.IsNullOrWhiteSpace(response.Message) ? "获取排行榜失败，请稍后重试。" : response.Message;
                        _leaderboardRankings.Add(new FgccHelper.Models.RankingEntry { Rank = 0, ProjectName = errorMessage, ComplexityScore = 0, Author = "-", DominanceDuration = 0 });
                    }
                    // Optionally, show a non-blocking message for scroll-load errors if list already has items
                    // else if (!isManualRefresh) { /* Maybe a toast or status bar message */ }
                    Debug.WriteLine($"Leaderboard load failed: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                 if (loadingIndicator != null && _leaderboardRankings.Contains(loadingIndicator))
                {
                    _leaderboardRankings.Remove(loadingIndicator);
                }
                _hasMoreLeaderboardData = false;
                if (isManualRefresh || !_leaderboardRankings.Any())
                {
                    _leaderboardRankings.Clear();
                    _leaderboardRankings.Add(new FgccHelper.Models.RankingEntry { Rank = 0, ProjectName = $"获取排行榜时发生意外错误。", ComplexityScore = 0, Author = "-", DominanceDuration = 0 });
                }
                MessageBox.Show($"获取排行榜数据时发生连接或意外错误: {ex.Message}", "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Exception during RefreshLeaderboardDataAsync: {ex.Message}");
            }
            finally
            {
                _isLoadingLeaderboard = false;
                if (loadingIndicator != null && _leaderboardRankings.Contains(loadingIndicator)) // Ensure indicator is removed in all cases
                {
                    _leaderboardRankings.Remove(loadingIndicator);
                }
            }
        }
        
        private void DetailsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void RankingsListView_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(RankingsListView);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollChanged += RankingsListView_ScrollChanged;
            }
        }

        private async void RankingsListView_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            // Check if scrolled to near the bottom, not currently loading, and more data might be available
            if (scrollViewer != null && e.VerticalChange > 0 && // Ensure it's a downward scroll or content height changed
                e.VerticalOffset >= scrollViewer.ScrollableHeight - 100 && // Near the bottom (100 is a threshold)
                !_isLoadingLeaderboard && 
                _hasMoreLeaderboardData)
            {
                Debug.WriteLine("Scrolled to bottom, attempting to load more leaderboard data.");
                await RefreshLeaderboardDataAsync(isManualRefresh: false);
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}


