# FgccHelper 开发日志

## 阶段一：基础框架与核心功能开发 (进行中)

### 1. 项目初始化与模型类创建
*   **日期**: 2024-07-26
*   **任务**:
    *   根据需求文档，定义了核心数据模型：
        *   `Models/DetailEntry.cs`: 用于存储每个统计项的详细条目信息 (名称, 大小, 文件类型)。
        *   `Models/StatisticItem.cs`: 用于存储单个统计项的信息 (名称, 数量, 描述, 详细列表)。
        *   `Models/Project.cs`: 用于存储整个工程的统计信息 (工程名, 设计器版本, 统计项列表)。
*   **状态**: 已完成

### 2. UI 界面初步设计 (XAML)
*   **日期**: 2024-07-26
*   **任务**:
    *   创建了主窗口 `MainWindow.xaml`。
    *   设计了基本的UI布局，包括：
        *   顶部操作区域：放置"选择工程目录"按钮、显示已选路径的文本块、以及"刷新"按钮。
        *   中部统计卡片区域：使用 `WrapPanel` 来动态展示各个统计项的卡片。
        *   底部详细信息列表区域：使用 `ListView` (配合 `GridView`) 来展示选中统计项的详细清单。
*   **状态**: 已完成

### 3. 核心数据统计逻辑实现
*   **日期**: 2024-07-26
*   **任务**:
    *   创建了服务类 `Services/ProjectAnalysisService.cs` 用于封装所有的数据统计逻辑。
    *   实现了 `AnalyzeProject(string projectPath)` 方法作为入口，统筹各项统计。
    *   实现了 `GetDesignerVersion(string projectPath)` 方法：
        *   读取工程根目录下的 `DocumentInfo` 文件。
        *   使用 `Newtonsoft.Json` 解析JSON内容，提取 `VersionString`。
        *   增加了错误处理，确保在文件不存在、无法解析或属性缺失时返回空字符串。
    *   创建了辅助方法 `GetFolderFileStatistics(...)`：
        *   用于统一处理基于文件夹内文件数量的统计（如页面、表、流程等）。
        *   统计指定目录下特定后缀文件的数量。
        *   为每个文件生成 `DetailEntry` (文件名, 大小, 文件类型)。
        *   增加了错误处理，确保在目录不存在或读取文件出错时，数量为0。
    *   基于 `GetFolderFileStatistics` 实现了以下统计项的逻辑：
        *   页面数量 (`Pages/*.json`)
        *   数据表数量 (`Tables/*.json`)
        *   流程数量 (`Process/*.bpmn`)
        *   报表数量 (`Reports/*.json`)
        *   接口数量 (`ServerCommands/*.json`)
        *   自定义插件数量 (`Plugin/*.zip`)
        *   自定义组件数量 (`UserControlPages/*.json`)
        *   计划任务数量 (`SchedulerTasks/*.json`)
        *   扩展JavaScript数量 (`UserJaveScript/*.js`) - *注意：文件夹名 `UserJaveScript` 根据文档设定，待确认是否为 `UserJavaScript`。*
    *   实现了 `GetExternalJsStatistics(string projectPath)` 和 `GetExternalCssStatistics(string projectPath)` 方法：
        *   读取工程根目录下的 `CustomLibraries.json` 文件。
        *   使用 `Newtonsoft.Json` 解析JSON内容。
        *   分别统计 `UserJSFileList` 和 `UserCSSFileList` 数组成员数量。
        *   为每个条目生成 `DetailEntry` (名称, "N/A"大小, 文件类型基于 `IsLink` 或URL判断)。
        *   增加了错误处理，确保在文件不存在、无法解析或对应列表不存在时，数量为0。
*   **状态**: 已完成 (需要添加 `Newtonsoft.Json` NuGet包)

### 4. UI逻辑与数据绑定实现
*   **日期**: 2024-07-26
*   **任务**:
    *   在 `MainWindow.xaml.cs` 中：
        *   实例化 `ProjectAnalysisService`。
        *   实现了 `SelectFolderButton_Click` 事件：
            *   使用 `System.Windows.Forms.FolderBrowserDialog` (需添加 `System.Windows.Forms.dll` 引用) 允许用户选择工程目录。
            *   获取选定路径后，调用 `LoadProjectData`。
        *   实现了 `LoadProjectData(string projectPath)` 方法：
            *   调用 `_analysisService.AnalyzeProject` 获取统计数据。
            *   调用 `UpdateUIWithProjectData` 更新界面。
            *   增加了基本的错误处理和用户提示 (MessageBox)。
            *   添加了等待光标反馈。
        *   实现了 `UpdateUIWithProjectData(Project project)` 方法：
            *   更新窗口标题，显示工程名和设计器版本。
            *   清空 `StatisticsWrapPanel`，然后根据 `project.Statistics` 动态创建统计卡片 (使用 `Border` 和 `TextBlock`)。
            *   为每个卡片设置 `Tag` 为对应的 `StatisticItem`，并附加 `MouseLeftButtonUp` 事件。
            *   卡片UI进行了初步美化（圆角、阴影、内外边距、字体）。
        *   实现了 `StatisticCard_MouseLeftButtonUp` 事件：
            *   当用户点击统计卡片时，获取对应的 `StatisticItem`。
            *   将其 `Details` 列表绑定到 `DetailsListView.ItemsSource`。
            *   更新 `DetailsGroupBox` 的 `Header` 以显示当前查看的统计项名称和数量。
            *   尝试自动调整 `GridView` 列宽。
        *   实现了 `RefreshButton_Click` 事件：
            *   重新调用 `LoadProjectData` 加载当前选定目录的数据。
            *   处理了未选择目录时的情况。
*   **状态**: 已完成 (需要添加 `System.Windows.Forms.dll` 引用)

### 5. 后续任务与说明
*   **测试**: 需要进行全面的功能测试，确保所有统计项准确，错误处理符合预期。
*   **依赖**:
    *   `Newtonsoft.Json`: 用于JSON解析，需通过NuGet包管理器添加到项目中。
    *   `System.Windows.Forms.dll`: 用于文件夹选择对话框，需手动添加到项目引用中。
*   **文件夹名称确认**: `UserJaveScript` 文件夹名称需要与实际活字格工程确认，如果实际为 `UserJavaScript`，需在 `ProjectAnalysisService.cs` 中进行调整。
*   **UI美化**: 当前UI为基础实现，未来可以考虑引入WPF UI库 (如 MahApps.Metro, HandyControl) 进行深度美化。
*   **打包与分发**: 最终目标是生成单文件绿色免安装的exe，需要配置项目的发布选项。

--- 