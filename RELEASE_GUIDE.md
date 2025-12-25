# FgccHelper 版本发布简易 SOP

本文档记录了 FgccHelper 项目版本发布的标准操作流程。

## 1. 更新版本号

修改 `FgccHelper/Properties/AssemblyInfo.cs` 文件，更新以下两个属性为新版本号（例如 `2.0.1`）：

```csharp
[assembly: AssemblyVersion("2.0.1.0")]
[assembly: AssemblyFileVersion("2.0.1.0")]
```

## 2. 编译项目

使用 MSBuild 重新编译项目（Release 模式）。由于项目是 .NET Framework 4.7.2，推荐使用 MSBuild 而非 `dotnet build`。

**PowerShell 命令：**
```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\Msbuild\Current\Bin\MSBuild.exe" "FgccHelper.sln" /t:Rebuild /p:Configuration=Release
```

> **注意**：如果你的 VS 安装路径不同，请相应调整 `MSBuild.exe` 的路径（例如可能是 `Community`、`Professional` 或 `Enterprise`）。

## 3. 创建发布包

使用项目自带的 `CosReleaseTool` 工具创建发布包、校验和及版本信息文件。

**PowerShell 命令：**
```powershell
# 配置参数
$Version = "2.0.1"                 # <--- 修改为新版本号
$Notes = "优化自动更新逻辑"           # <--- 修改为本次更新内容
$ToolPath = ".\CosReleaseTool\bin\Release\net472\CosReleaseTool.exe"
$SourcePath = ".\FgccHelper\bin\Release"

# 执行创建命令
& $ToolPath create $SourcePath $Version $Notes
```

执行成功后，控制台会输出发布包的生成路径（默认为 `%TEMP%\FgccHelper\Releases`）。

## 4. 上传发布

将生成的发布包自动上传到腾讯云 COS。工具会自动读取代码中配置的密钥。

**PowerShell 命令：**
```powershell
# 指定发布包目录（通常是上一步生成的目录）
$ReleaseDir = "$env:TEMP\FgccHelper\Releases"
$ToolPath = ".\CosReleaseTool\bin\Release\net472\CosReleaseTool.exe"

# 执行上传命令
& $ToolPath upload $ReleaseDir
```

## 5. 验证

上传完成后，文件会自动归档到 COS 的 `FgccHelper/packages` 和 `FgccHelper/versions` 目录下。
客户端重启后即可通过读取 `latest.json` 检测到新版本。
