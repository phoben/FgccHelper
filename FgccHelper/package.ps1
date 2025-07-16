# 获取项目版本号
$projectFileContent = Get-Content -Path ".\FgccHelper.csproj" -Raw # 使用 -Raw 确保正确处理多行
$versionPattern = '<Version>(.*?)<\/Version>'
$version = [regex]::Match($projectFileContent, $versionPattern).Groups[1].Value

Write-Host "准备打包版本: $version"

# 定义输出目录，Squirrel默认会把Releases放到项目根目录，我们这里保持一致
$outputDir = ".\Releases"

# 构建应用
# 根据你的项目配置，可能需要调整 target framework 和 runtime identifier
# 例如 net6.0-windows, win-x64, win-x86 等
# 如果你的项目不是 .NET Core/5+ SDK-style project，dotnet publish 可能不适用
# 你可能需要使用 msbuild 或者直接指定编译好的可执行文件路径
# 鉴于项目文件是 ToolsVersion="15.0" 且 TargetFrameworkVersion 为 v4.7.2，我们将使用 nuget pack 的方式

# 清理旧的发布文件和Releases目录 (可选，但推荐)
if (Test-Path $outputDir) {
    Remove-Item -Recurse -Force $outputDir
}
if (Test-Path ".\FgccHelper.$version.nupkg") {
    Remove-Item -Force ".\FgccHelper.$version.nupkg"
}
if (Test-Path ".\FgccHelper.nuspec") {
    Remove-Item -Force ".\FgccHelper.nuspec"
}

# 创建nuspec文件
# 注意：<files> 部分需要根据你的项目实际输出结构进行调整
# 通常对于 .NET Framework 项目，会将编译输出的DLLs和EXE放到 lib\netXX 目录下
# 这里的 target="lib\net472" 是根据你的项目 TargetFrameworkVersion v4.7.2 来的
$nuspecContent = @"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>FgccHelper</id>
    <version>$version</version>
    <title>FgccHelper</title>
    <authors>phoben</authors>
    <description>FgccHelper - 增加自动更新功能</description>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <!-- 如果有图标，可以添加 <iconUrl> 或 <icon> -->
  </metadata>
  <files>
    <!-- 请确保这里的路径指向你项目编译后的主要输出文件 -->
    <!-- 通常是 bin\Release 目录下的内容 -->
    <!-- 排除 .pdb 和 .xml 文件可以减小包体积 -->
    <file src="bin\Release\FgccHelper.exe" target="lib\net472" />
    <file src="bin\Release\*.dll" target="lib\net472" exclude="**\*.pdb;**\*.xml;**\*.vshost.*" />
    <!-- 如果有其他依赖的内容文件，也需要在这里添加 -->
    <!-- 例如：<file src="bin\Release\somecontent.txt" target="content" /> -->
  </files>
</package>
"@

Set-Content -Path ".\FgccHelper.nuspec" -Value $nuspecContent -Encoding UTF8

Write-Host "FgccHelper.nuspec 文件已创建。"

# 构建项目以确保输出文件是最新的
msbuild .\FgccHelper.csproj /t:Rebuild /p:Configuration=Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "MSBuild 构建失败。"
    exit 1
}

Write-Host "项目构建完成 (Release模式)。"

# 创建NuGet包
# 你需要确保 nuget.exe 在你的 PATH 环境变量中，或者提供完整路径
nuget pack .\FgccHelper.nuspec -NoPackageAnalysis
if ($LASTEXITCODE -ne 0) {
    Write-Error "NuGet 包创建失败。"
    exit 1
}

Write-Host "NuGet 包 FgccHelper.$version.nupkg 已创建。"

# 创建Squirrel安装包
# 你需要确保 squirrel.exe 在你的 PATH 环境变量中，或者提供完整路径
# --no-msi 选项表示不创建传统的 MSI 安装包
# --no-delta 选项可以禁用增量包的创建，如果你不需要的话
squirrel --releasify ".\FgccHelper.$version.nupkg" --releaseDir $outputDir --no-msi
if ($LASTEXITCODE -ne 0) {
    Write-Error "Squirrel 安装包创建失败。"
    exit 1
}

Write-Host "Squirrel 安装包已创建在 $outputDir 目录。"

# 创建Release Notes文件
$releaseNotesContent = @"
# 版本 $version 更新说明

增加自动更新功能

## 新功能
- 集成 Squirrel.Windows 实现自动更新
- 程序启动时自动检查更新
- 提供更新弹窗提示
- 支持手动检查更新

## 修复问题
- (如果本次有修复特定问题，请在此列出)
"@

Set-Content -Path "$outputDir\RELEASE-NOTES.md" -Value $releaseNotesContent -Encoding UTF8

Write-Host "RELEASE-NOTES.md 文件已创建。"
Write-Host "打包完成！请检查 $outputDir 目录中的文件，并将它们上传到 GitHub Releases。"
Write-Host "需要上传的文件通常包括：RELEASES, Setup.exe, FgccHelper-$version-full.nupkg (以及对应的delta包，如果生成了)" 