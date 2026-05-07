## Why

阶段 2 完成了数据层（DataWorkspace、JsonDataFile、JsonNodeVM、JsonFileService），但 UI 层全是空壳。用户无法通过界面浏览和查看 JSON 数据文件。需要将数据层接入 WPF 界面，实现"打开文件夹 → 选择文件 → 表格展示 JSON"的基本工作流。

## What Changes

- 实现 `MainWindow` 布局：工具栏（打开文件夹、保存）、左右分栏、底部状态栏
- 实现 `MainViewModel`：管理 DataWorkspace 生命周期，协调子 ViewModel
- 实现 `FileTreeView` + `FileTreeViewModel`：左侧文件导航列表，显示 JSON 文件名，脏文件标记 `*`
- 实现 `JsonEditorView` + `JsonEditorViewModel`：右侧 DataGrid 表格展示 JSON 数据
  - Array of Objects 结构：第一层属性名作为列，每个 Object 作为一行
  - 简单值（string/number/bool/null）直接显示
  - 复杂值（Object/Array）浓缩为一行文本（如 `[1] {move_speed, flat, 0.3}`）
- 实现文件选中 → 内容切换联动
- 实现状态栏信息（已加载文件数、当前文件名、记录数）
- 添加基础样式（不同类型值的颜色区分）

## Capabilities

### New Capabilities
- `main-window-layout`: MainWindow 整体布局、工具栏、状态栏、左右分栏结构
- `file-navigation`: 左侧文件导航面板，文件列表展示与选中交互
- `json-table-view`: 右侧 DataGrid 表格展示 JSON 数据（思路B混合列方案）
- `viewmodel-coordination`: ViewModel 层的协调机制（MainVM 持有子 VM，选中联动）

### Modified Capabilities
<!-- 无现有 specs 需要修改 -->

## Impact

- **代码影响**：`MainWindow.xaml`、`MainViewModel.cs`、`FileTreeView.xaml/.cs`、`FileTreeViewModel.cs`、`JsonEditorView.xaml/.cs`、`JsonEditorViewModel.cs`、`App.xaml`、`Styles.xaml`
- **依赖**：可能需要添加文件夹选择对话框方案（WinForms 引用或 Ookii.Dialogs.Wpf）
- **数据层**：`DataWorkspace` 和 `JsonDataFile` 不需要修改，直接使用现有接口
