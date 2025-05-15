using System;
using System.Collections.Generic; // For List<string>
using System.Diagnostics; // For Process
using System.Text; // For StringBuilder
using System.Windows;
using System.Windows.Controls; // For ComboBox
using System.Windows.Input; // For Cursors
using System.Linq; // For OrderBy

namespace FgccHelper
{
    public partial class GitCloneWindow : Window
    {
        public string RepoUrl { get; private set; }
        public string SelectedBranch { get; private set; }

        private string _gitUsername; // Store credentials passed from MainWindow
        private string _gitPassword;

        public GitCloneWindow(string gitUsername, string gitPassword)
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            _gitUsername = gitUsername;
            _gitPassword = gitPassword;
        }

        public static string BuildGitUrlWithCredentials(string baseUrl, string username, string password)
        {
            if (string.IsNullOrEmpty(username) || !baseUrl.ToLower().StartsWith("https://"))
            {
                return baseUrl; // Return original URL if no username or not HTTPS
            }

            string credentials = Uri.EscapeDataString(username);
            if (!string.IsNullOrEmpty(password))
            {
                credentials += ":" + Uri.EscapeDataString(password);
            }
            // Ensure there are two slashes after https:
            int insertIndex = baseUrl.IndexOf("//");
            if (insertIndex == -1) return baseUrl; // Should not happen for valid https urls

            return baseUrl.Insert(insertIndex + 2, credentials + "@");
        }

        private void CloneButton_Click(object sender, RoutedEventArgs e)
        {
            RepoUrl = RepoUrlTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(RepoUrl))
            {
                MessageBox.Show(this, "仓库地址不能为空。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                RepoUrlTextBox.Focus();
                return;
            }
            if (!Uri.TryCreate(RepoUrl, UriKind.Absolute, out _))
            {
                MessageBox.Show(this, "仓库地址格式无效。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                RepoUrlTextBox.Focus();
                return;
            }

            if (BranchComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.IsEnabled)
            {
                SelectedBranch = selectedItem.Content.ToString();
            }
            else if (BranchComboBox.SelectedItem != null && BranchComboBox.SelectedItem is string selectedStringItem) // Direct string item
            {
                 SelectedBranch = selectedStringItem;
            } 
            else
            {
                SelectedBranch = null; 
            }

            this.DialogResult = true;
        }

        private async void FetchBranchesButton_Click(object sender, RoutedEventArgs e)
        {
            string repoUrl = RepoUrlTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(repoUrl))
            {
                MessageBox.Show(this, "请先输入仓库地址。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                RepoUrlTextBox.Focus();
                return;
            }
            if (!Uri.TryCreate(repoUrl, UriKind.Absolute, out _))
            {
                 MessageBox.Show(this, "仓库地址格式无效。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                RepoUrlTextBox.Focus();
                return;
            }

            SetInputsEnabled(false);
            BranchComboBox.ItemsSource = null;
            BranchComboBox.Items.Clear(); 
            BranchComboBox.Items.Add(new ComboBoxItem { Content = "正在获取分支...", IsEnabled = false });
            BranchComboBox.SelectedIndex = 0;
            BranchComboBox.IsEnabled = true; 
            this.Cursor = Cursors.Wait;

            string lsRemoteUrl = repoUrl;
            lsRemoteUrl = BuildGitUrlWithCredentials(repoUrl, _gitUsername, _gitPassword);

            ProcessStartInfo gitInfo = new ProcessStartInfo("git", $"ls-remote --heads \"{lsRemoteUrl}\"");
            gitInfo.CreateNoWindow = true;
            gitInfo.UseShellExecute = false;
            gitInfo.RedirectStandardOutput = true;
            gitInfo.RedirectStandardError = true;
            gitInfo.StandardOutputEncoding = Encoding.UTF8; 
            gitInfo.StandardErrorEncoding = Encoding.UTF8;

            StringBuilder output = new StringBuilder();
            StringBuilder errorOutput = new StringBuilder();
            List<string> branches = new List<string>();

            try
            {
                using (Process gitProcess = new Process { StartInfo = gitInfo })
                {
                    gitProcess.OutputDataReceived += (s, args) => { if (args.Data != null) output.AppendLine(args.Data); };
                    gitProcess.ErrorDataReceived += (s, args) => { if (args.Data != null) errorOutput.AppendLine(args.Data); };

                    await System.Threading.Tasks.Task.Run(() => 
                    {
                        gitProcess.Start();
                        gitProcess.BeginOutputReadLine();
                        gitProcess.BeginErrorReadLine();
                        gitProcess.WaitForExit(); 
                    });
                    
                    if (gitProcess.ExitCode == 0)
                    {
                        string[] lines = output.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string line in lines)
                        {
                            if (line.Contains("refs/heads/"))
                            {
                                branches.Add(line.Substring(line.LastIndexOf("refs/heads/") + "refs/heads/".Length).Trim());
                            }
                        }
                        if (branches.Count == 0 && output.Length > 0 && errorOutput.Length == 0) 
                        {
                           errorOutput.AppendLine("成功连接到仓库，但未能解析出分支列表。请检查仓库是否为空或输出格式。");
                           errorOutput.AppendLine("原始输出:\n" + output.ToString());
                        } 
                    }
                    else
                    {
                        if (errorOutput.Length == 0 && output.Length > 0) errorOutput.AppendLine("Git命令输出(可能包含错误):\n" + output.ToString());
                        else if (errorOutput.Length == 0) errorOutput.AppendLine("未知错误。请检查Git是否安装并正确配置，以及仓库URL和凭据是否正确。");
                    }
                }
            }
            catch (Exception ex)
            {
                errorOutput.AppendLine($"执行Git命令时发生异常: {ex.Message}");
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
                SetInputsEnabled(true);
                BranchComboBox.Items.Clear(); 

                if (branches.Any())
                {
                    foreach (var branchName in branches.OrderBy(b => b))
                    {
                        BranchComboBox.Items.Add(new ComboBoxItem { Content = branchName, IsEnabled = true });
                    }
                    BranchComboBox.SelectedIndex = 0; 
                    BranchComboBox.IsEnabled = true;
                }
                else
                {
                    BranchComboBox.Items.Add(new ComboBoxItem { Content = "无可用分支或获取失败", IsEnabled = false });
                    BranchComboBox.SelectedIndex = 0;
                    BranchComboBox.IsEnabled = false;
                    if (errorOutput.Length > 0)
                    {
                        MessageBox.Show(this, $"获取分支失败:\n{errorOutput.ToString()}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else if (!branches.Any() && output.Length ==0) 
                    {
                        MessageBox.Show(this, "获取分支失败，且无详细错误信息。请检查网络连接、仓库地址及凭据。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void SetInputsEnabled(bool isEnabled)
        {
            RepoUrlTextBox.IsEnabled = isEnabled;
            // UsernameTextBox and PasswordBox are removed
            FetchBranchesButton.IsEnabled = isEnabled;
            CloneButton.IsEnabled = isEnabled;
            BranchComboBox.IsEnabled = isEnabled; // Also manage BranchComboBox state
        }
    }
} 