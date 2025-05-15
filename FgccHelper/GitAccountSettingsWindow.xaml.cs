using System.Windows;

namespace FgccHelper
{
    public partial class GitAccountSettingsWindow : Window
    {
        public string Username { get; private set; }
        public string Password { get; private set; }

        // Constructor to load existing settings if available (will be implemented later with config file)
        public GitAccountSettingsWindow(string currentUsername = "", string currentPassword = "")
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            UsernameTextBox.Text = currentUsername;
            PasswordBox.Password = currentPassword;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Username = UsernameTextBox.Text.Trim();
            Password = PasswordBox.Password; // No trim for password
            
            // Basic validation (optional, can be enhanced)
            if (string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show(this, "用户名不能为空。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                UsernameTextBox.Focus();
                return;
            }

            this.DialogResult = true;
        }
    }
} 