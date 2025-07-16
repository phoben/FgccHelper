using System.Windows;
using System.Text; // For StringBuilder in example data
using System.ComponentModel; // For INotifyPropertyChanged if needed for live updates
// using FgccHelper.Models; // We will fully qualify types for diagnostics
// using FgccHelper.Services; // We will fully qualify types for diagnostics
using System.Reflection; // For Assembly version
using System; // For Exception

namespace FgccHelper
{
    public partial class SubmitToLeaderboardWindow : Window
    {
        // private string _currentProjectStatsSummary; // This field might no longer be needed if TextBlockProjectStats is populated directly
        private FgccHelper.Models.ProjectStatisticsContainer _projectStatisticsContainer; // Corrected type
        private string _currentComplexityScoreValue; // The raw score value
        private string _defaultProjectName;
        private string _currentDesignerVersion; // Pass from MainWindow

        private FgccHelper.Services.LeaderboardApiService _apiService;
        private AppSettings _appSettings; // Added to store AppSettings
        private Action _saveAppSettingsCallback; // Added for callback to save settings

        // Constructor updated to accept AppSettings and a save callback
        public SubmitToLeaderboardWindow(
            FgccHelper.Models.ProjectStatisticsContainer statsContainer,
            string complexityScore,
            string defaultProjectName,
            string designerVersion,
            AppSettings appSettings, // Added AppSettings parameter
            Action saveAppSettingsCallback) // Added callback parameter
        {
            InitializeComponent();
            _apiService = new FgccHelper.Services.LeaderboardApiService();

            _projectStatisticsContainer = statsContainer;
            _currentComplexityScoreValue = complexityScore;
            _defaultProjectName = defaultProjectName;
            _currentDesignerVersion = designerVersion;
            _appSettings = appSettings; // Store AppSettings
            _saveAppSettingsCallback = saveAppSettingsCallback; // Store callback

            LoadProjectDataForDisplay();
        }

        private void LoadProjectDataForDisplay()
        {
            var summaryBuilder = new StringBuilder();
            if (_projectStatisticsContainer != null)
            {
                summaryBuilder.AppendLine($"页面数量: {_projectStatisticsContainer.PageCount}");
                summaryBuilder.AppendLine($"数据表数量: {_projectStatisticsContainer.TableCount}");
                summaryBuilder.AppendLine($"业务流程数量: {_projectStatisticsContainer.BusinessProcessCount}");
                summaryBuilder.AppendLine($"报表数量: {_projectStatisticsContainer.ReportCount}");
                summaryBuilder.AppendLine($"服务端命令数量: {_projectStatisticsContainer.ServerCommandCount}");
                summaryBuilder.AppendLine($"自定义插件数量: {_projectStatisticsContainer.CustomPluginCount}");
                summaryBuilder.AppendLine($"自定义组件数量: {_projectStatisticsContainer.CustomComponentCount}");
                summaryBuilder.AppendLine($"定时任务数量: {_projectStatisticsContainer.ScheduledTaskCount}");
                summaryBuilder.AppendLine($"扩展JS文件数量: {_projectStatisticsContainer.ExtendedJsFileCount}");
                summaryBuilder.AppendLine($"外部JS文件数量: {_projectStatisticsContainer.ExternalJsFileCount}");
                summaryBuilder.AppendLine($"外部CSS文件数量: {_projectStatisticsContainer.ExternalCssFileCount}");
            }
            else
            {
                summaryBuilder.AppendLine("无法加载详细统计信息。具体数值请查看主界面。\n确保项目已完全分析。如果问题持续，请重新打开项目。 ");
            }
            TextBlockProjectStats.Text = summaryBuilder.ToString().TrimEnd();
            TextBlockComplexityScore.Text = _currentComplexityScoreValue;
            TextBoxProjectName.Text = _defaultProjectName;

            // Load AuthorName and Email from AppSettings
            if (_appSettings != null)
            {
                TextBoxAuthorName.Text = _appSettings.AuthorName;
                TextBoxEmail.Text = _appSettings.Email;
            }
        }

        private string GetClientVersion()
        {
            try
            {
                return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private async void ButtonSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TextBoxProjectName.Text))
            {
                MessageBox.Show("项目名称不能为空。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                TextBoxProjectName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(TextBoxAuthorName.Text))
            {
                MessageBox.Show("作者昵称不能为空。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                TextBoxAuthorName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(TextBoxEmail.Text))
            {
                MessageBox.Show("联系邮箱不能为空。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                TextBoxEmail.Focus();
                return;
            }

            // Basic email format validation
            try
            {
                var mailAddress = new System.Net.Mail.MailAddress(TextBoxEmail.Text);
            }
            catch (FormatException)
            {
                MessageBox.Show("请输入有效的邮箱地址。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                TextBoxEmail.Focus();
                return;
            }

            if (CheckBoxAgreeTerms.IsChecked == false)
            {
                MessageBox.Show("请勾选同意才可以提交", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                CheckBoxAgreeTerms.Focus();
                return;
            }

            ButtonSubmit.IsEnabled = false;
            ButtonSubmit.Content = "提交中...";

            if (_projectStatisticsContainer == null)
            {
                MessageBox.Show("项目统计数据不完整，无法提交。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                ButtonSubmit.IsEnabled = true;
                ButtonSubmit.Content = "提交到排行榜";
                return;
            }

            int.TryParse(_currentComplexityScoreValue, out int complexityScoreInt);

            var submissionRequest = new FgccHelper.Models.RankingSubmissionRequest
            {
                ProjectName = TextBoxProjectName.Text,
                Author = TextBoxAuthorName.Text,
                Description = TextBoxProjectDescription.Text,
                Email = TextBoxEmail.Text,
                ComplexityScore = complexityScoreInt,

                PageCount = _projectStatisticsContainer.PageCount,
                TableCount = _projectStatisticsContainer.TableCount,
                BusinessProcessCount = _projectStatisticsContainer.BusinessProcessCount,
                ReportCount = _projectStatisticsContainer.ReportCount,
                ServerCommandCount = _projectStatisticsContainer.ServerCommandCount,
                CustomPluginCount = _projectStatisticsContainer.CustomPluginCount,
                CustomComponentCount = _projectStatisticsContainer.CustomComponentCount,
                ScheduledTaskCount = _projectStatisticsContainer.ScheduledTaskCount,
                ExtendedJsFileCount = _projectStatisticsContainer.ExtendedJsFileCount,
                ExternalJsFileCount = _projectStatisticsContainer.ExternalJsFileCount,
                ExternalCssFileCount = _projectStatisticsContainer.ExternalCssFileCount,

                ClientVersion = GetClientVersion(),
                DesignerVersion = _currentDesignerVersion ?? "unknown"
            };

            try
            {
                FgccHelper.Models.SubmitRankingApiResponse apiResponse = await _apiService.SubmitRankingAsync(submissionRequest);

                if (apiResponse.ErrorCode == 0)
                {
                    // Save AuthorName and Email to AppSettings before closing
                    if (_appSettings != null)
                    {
                        _appSettings.AuthorName = TextBoxAuthorName.Text;
                        _appSettings.Email = TextBoxEmail.Text;
                        _saveAppSettingsCallback?.Invoke(); // Call the save callback
                    }

                    MessageBox.Show((apiResponse.Message ?? "项目已成功提交到排行榜！") + "\n" + "复杂度评分：" + apiResponse.ComplexityScore, "提交成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true; // This will also trigger an update in MainWindow if needed
                    this.Close();
                }
                else
                {
                    MessageBox.Show($"提交失败: {apiResponse.Message}", "提交错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    ButtonSubmit.IsEnabled = true;
                    ButtonSubmit.Content = "提交到排行榜";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"提交过程中发生意外错误: {ex.Message}", "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
                ButtonSubmit.IsEnabled = true;
                ButtonSubmit.Content = "提交到排行榜";
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }

        private void TextBoxProjectName_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }
    }
}