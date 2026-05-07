## ADDED Requirements

### Requirement: MainWindow 整体布局结构
MainWindow SHALL 包含三个区域：顶部工具栏（ToolBar）、中部左右分栏内容区、底部状态栏（StatusBar）。中部内容区 SHALL 使用 Grid + GridSplitter 实现左右分栏，左侧为文件导航面板，右侧为 JSON 内容视图。

#### Scenario: 应用启动时显示完整布局
- **WHEN** 用户启动应用程序
- **THEN** 窗口 SHALL 显示顶部工具栏、左右分栏区域和底部状态栏
- **THEN** 左侧面板 SHALL 占约 200px 宽度，右侧面板 SHALL 占据剩余空间

### Requirement: 工具栏按钮
工具栏 SHALL 包含"打开文件夹"按钮和"保存"按钮。

#### Scenario: 工具栏显示
- **WHEN** 应用程序启动
- **THEN** 工具栏 SHALL 显示"打开文件夹"按钮
- **THEN** 工具栏 SHALL 显示"保存"按钮

### Requirement: 窗口标题
MainWindow 的标题 SHALL 为 "DataManager"。

#### Scenario: 窗口标题显示
- **WHEN** 应用程序启动
- **THEN** 窗口标题栏 SHALL 显示 "DataManager"

### Requirement: DataContext 绑定
MainWindow SHALL 将 MainViewModel 设为其 DataContext，子 UserControl SHALL 通过数据绑定获取对应的子 ViewModel。

#### Scenario: ViewModel 绑定
- **WHEN** MainWindow 初始化
- **THEN** MainWindow.DataContext SHALL 是 MainViewModel 实例
- **THEN** FileTreeView 的 DataContext SHALL 绑定到 MainViewModel.FileTreeVM
- **THEN** JsonEditorView 的 DataContext SHALL 绑定到 MainViewModel.JsonEditorVM
