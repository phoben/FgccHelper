using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace FgccHelper.Models
{
    /// <summary>
    /// 自动更新配置模型
    /// </summary>
    public class UpdateConfig : INotifyPropertyChanged
    {
        // 硬编码配置 - 使用只读属性
        [JsonIgnore]
        public string CosBucketUrl => "https://gridfriend-1257098086.cos.ap-shanghai.myqcloud.com";

        [JsonIgnore]
        public string CosBucketName => "gridfriend-1257098086";

        [JsonIgnore]
        public string CosRegion => "ap-shanghai";
        
        /// <summary>
        /// COS SecretId
        /// </summary>
        [JsonIgnore]
        public string CosSecretId { get; private set; }
        
        /// <summary>
        /// COS SecretKey
        /// 注意: 请在 secrets.json 中配置，不要提交到公开仓库
        /// </summary>
        [JsonIgnore]
        public string CosSecretKey { get; private set; }

        [JsonIgnore]
        public bool AutoCheckUpdate => true;

        [JsonIgnore]
        public int CheckIntervalHours => 2;

        private string _currentVersion = "1.0.0";

        /// <summary>
        /// 当前应用版本
        /// </summary>
        public string CurrentVersion
        {
            get => _currentVersion;
            set { _currentVersion = value; OnPropertyChanged(); }
        }

        public UpdateConfig()
        {
            LoadSecrets();
        }

        private void LoadSecrets()
        {
            try 
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "secrets.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var config = JsonConvert.DeserializeObject<SecretConfig>(json);
                    if (config != null)
                    {
                        CosSecretId = config.CosSecretId;
                        CosSecretKey = config.CosSecretKey;
                    }
                }
            }
            catch (Exception)
            {
                // 忽略加载错误
            }
        }

        private class SecretConfig
        {
            public string CosSecretId { get; set; }
            public string CosSecretKey { get; set; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
