## Why

用户选中 JSON 表格中的某条数据后，需要查看和编辑与该条目 ID 对应的 Lua 脚本文件。当前工具没有关联查阅机制，用户必须手动在外部编辑器中打开 Lua 文件，打断了数据编辑的工作流。

## What Changes

- 新增右侧 Lua 编辑面板，当选中行有对应 `Lua/{ID}.lua` 文件时自动显示
- 面板支持编辑和自动保存（切换行时自动写回磁盘）
- 面板顶部预留功能按钮栏（编号 1、2、4、5），当前为占位状态，为后续针对 Lua 文件的操作预留
- 无对应 Lua 文件时面板自动隐藏，JsonEditor 占满右侧空间
- JsonEditor 需暴露当前选中行的 ID 值，供面板联动

## Capabilities

### New Capabilities
- `lua-viewer`: 右侧 Lua 脚本查阅/编辑面板，根据 JSON 表格选中行的 ID 字段自动加载对应 Lua 文件，支持编辑和自动保存

### Modified Capabilities

## Impact

- `MainWindow.xaml`: 布局从 3 列扩展为 5 列（新增 GridSplitter + LuaViewer 列）
- `Domain/Main/MainViewModel.cs`: 新增 LuaViewerVM 属性，监听行选中变化驱动 Lua 面板
- `Domain/JsonEditor/JsonEditorViewModel.cs`: 新增 SelectedRowId 属性，暴露当前选中行的 ID 值
- `Domain/JsonEditor/JsonEditorView.xaml.cs`: 监听 DataGrid 行选中事件，通知 ViewModel
- 新增 `Domain/LuaViewer/` 模块（ViewModel + View）
- 新增 NuGet 依赖：`AvalonEdit 6.*`（代码编辑器控件，提供语法高亮和行号）
