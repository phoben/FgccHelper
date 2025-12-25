## 自动更新系统设计方案

### 1. 系统架构
- **版本检查服务**: 定期检查COS储存桶中的版本信息
- **更新下载服务**: 安全下载新版本安装包
- **安装执行服务**: 自动安装更新并重启应用
- **配置管理服务**: 管理更新设置和存储COS配置

### 2. 核心组件实现

#### 2.1 更新配置模型 (Models/UpdateConfig.cs)
```csharp
public class UpdateConfig
{
    public string CosBucketUrl { get; set; }      // COS储存桶URL
    public string CosSecretId { get; set; }     // API密钥ID
    public string CosSecretKey { get; set; }    // API密钥
    public string CurrentVersion { get; set; }    // 当前版本
    public bool AutoCheckUpdate { get; set; }   // 自动检查更新
    public int CheckIntervalHours { get; set; } // 检查间隔(小时)
}
```

#### 2.2 版本信息服务 (Services/UpdateService.cs)
```csharp
public class UpdateService
{
    // 从COS获取最新版本信息
    public async Task<VersionInfo> GetLatestVersionAsync()
    
    // 比较版本号
    public bool IsUpdateAvailable(string currentVersion, string latestVersion)
    
    // 下载更新包
    public async Task<bool> DownloadUpdateAsync(string downloadUrl, string savePath)
    
    // 验证下载文件完整性
    public bool VerifyUpdatePackage(string filePath, string expectedHash)
}
```

#### 2.3 更新管理器 (Services/UpdateManager.cs)
```csharp
public class UpdateManager
{
    // 检查并执行更新
    public async Task<UpdateResult> CheckAndUpdateAsync()
    
    // 显示更新提示窗口
    private void ShowUpdateNotification(VersionInfo latestVersion)
    
    // 执行更新安装
    private async Task<bool> InstallUpdateAsync(string updatePackagePath)
    
    // 重启应用程序
    private void RestartApplication()
}
```

#### 2.4 更新窗口 (UpdateWindow.xaml)
- 更新可用提示界面
- 下载进度显示
- 更新说明展示
- 用户确认按钮

### 3. COS储存桶结构设计
```
your-bucket/
├── versions/
│   ├── latest.json          # 最新版本信息
│   ├── v1.2.3.json         # 版本1.2.3详细信息
│   └── v1.2.4.json         # 版本1.2.4详细信息
├── packages/
│   ├── FgccHelper_v1.2.3.zip
│   └── FgccHelper_v1.2.4.zip
└── checksums/
    ├── FgccHelper_v1.2.3.sha256
    └── FgccHelper_v1.2.4.sha256
```

### 4. 版本信息格式 (latest.json)
```json
{
    "version": "1.2.4",
    "releaseDate": "2025-01-15",
    "downloadUrl": "https://your-bucket.cos.region.myqcloud.com/packages/FgccHelper_v1.2.4.zip",
    "fileSize": 15234567,
    "checksum": "a1b2c3d4e5f6...",
    "releaseNotes": [
        "新增自动更新功能",
        "修复已知问题",
        "优化性能"
    ],
    "minVersion": "1.2.0",
    "forceUpdate": false
}
```

### 5. 集成步骤

#### 5.1 在App.xaml.cs中集成更新检查
```csharp
protected override async void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    
    // 启动时检查更新
    var updateManager = new UpdateManager();
    await updateManager.CheckAndUpdateAsync();
}
```

#### 5.2 在主窗口添加更新菜单
- 检查更新按钮
- 更新设置对话框
- 更新历史记录

#### 5.3 后台定时检查
```csharp
// 使用定时器定期检查更新
var timer = new System.Timers.Timer(TimeSpan.FromHours(24).TotalMilliseconds);
timer.Elapsed += async (sender, e) => await CheckForUpdatesAsync();
```

### 6. 安全考虑
- **HTTPS通信**: 所有COS请求使用HTTPS
- **文件校验**: 下载后验证SHA256校验和
- **数字签名**: 对更新包进行数字签名验证
- **权限控制**: 限制更新安装程序的权限
- **回滚机制**: 更新失败时自动回滚

### 7. 错误处理
- 网络连接失败重试机制
- 下载中断恢复功能
- 安装失败日志记录
- 用户友好的错误提示

### 8. 配置管理
- 更新设置存储在本地配置文件
- 支持代理服务器配置
- 可配置的更新检查频率
- 用户偏好设置持久化

这个方案提供了完整的自动更新解决方案，包括版本检查、安全下载、自动安装和用户交互等核心功能。