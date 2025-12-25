我将通过创建一个专用的 `CosReleaseTool` 项目，使用腾讯云 SDK 实现 COS 上传逻辑，并更新相关文档，来帮您实现自动化发布流程。

以下是实施计划：

### 1. 项目重构与创建
- **新建控制台项目**：在解决方案目录下创建名为 `CosReleaseTool` 的新控制台项目。
- **迁移代码**：将现有的 CLI 入口文件 (`FgccHelper/CosReleaseTool.cs`) 移动到新项目并重命名为 `Program.cs`。
- **链接共享代码**：从主项目 `FgccHelper` 链接必要的模型 (Models) 和服务 (Services) 代码，避免代码重复。
- **添加依赖**：为新项目安装 `Tencent.QCloud.Cos.Sdk` 和 `Newtonsoft.Json`。
- **更新解决方案**：将新的 `CosReleaseTool` 项目添加到 `FgccHelper.sln` 中。

### 2. 实现 COS 上传逻辑
- **修改 `Program.cs`**：
  - 添加 COS 凭证相关的命令行参数（`--secret-id`, `--secret-key`, `--bucket`, `--region`）。
  - 使用 `Tencent.QCloud.Cos.Sdk` 实现 `UploadRelease` 方法。
  - 工具将自动上传以下文件：
    - `versions/v<Version>.json`
    - `versions/latest.json`
    - `packages/FgccHelper_v<Version>.zip`
    - `checksums/FgccHelper_v<Version>.zip.sha256`
  - 确保在上传时设置正确的 Content-Type 和权限（公有读）。

### 3. 更新文档
- **修改 `VERSION_RELEASE_SOP.md`**：
  - 更新编译步骤，加入 `CosReleaseTool` 的编译说明。
  - 将原本的手动“上传到 COS”步骤替换为使用 `upload` 命令的自动化操作说明。

### 4. 验证
- 验证代码能否成功编译，并检查项目结构是否正确。
- 检查代码逻辑以确保上传路径和权限设置符合要求。
