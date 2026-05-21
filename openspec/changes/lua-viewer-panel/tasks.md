## 1. 依赖引入

- [x] 1.1 在 `DataManager.csproj` 中添加 `<PackageReference Include="AvalonEdit" Version="6.*" />`
- [x] 1.2 执行 `dotnet restore` 确认包安装成功

## 2. LuaViewer Module 搭建

- [x] 2.1 创建 `Domain/LuaViewer/LuaViewerViewModel.cs`：包含 Content、IsVisible、IsDirty、FileName 属性，以及 SwitchToId 和 Save 方法
- [x] 2.2 创建 `Domain/LuaViewer/LuaViewerView.xaml`：功能按钮栏（预留按钮 1、2、4、5）+ 文件名标签 + AvalonEdit 编辑器（Lua 语法高亮、行号显示）
- [x] 2.3 创建 `Domain/LuaViewer/LuaViewerView.xaml.cs`：code-behind，初始化 AvalonEdit 配置及 TextChanged 事件绑定

## 3. JsonEditor 行选中信号

- [x] 3.1 在 `JsonEditorViewModel.cs` 新增 `[ObservableProperty] string? _selectedRowId` 属性
- [x] 3.2 在 `JsonEditorView.xaml.cs` 监听 DataGrid 的 `CurrentCellChanged` 事件，从当前行取 "ID" 列值写入 ViewModel 的 SelectedRowId

## 4. MainViewModel 协调

- [x] 4.1 在 `MainViewModel.cs` 新增 `LuaViewerVM` 属性并初始化
- [x] 4.2 监听 `JsonEditorVM.PropertyChanged` 中 `SelectedRowId` 变化，调用 `LuaViewerVM.SwitchToId(workspace.RootPath, id)`

## 5. MainWindow 布局改造

- [x] 5.1 在 `MainWindow.xaml` 中右侧区域新增两列（GridSplitter + LuaViewer），LuaViewer 列固定宽度
- [x] 5.2 LuaViewer 列和 GridSplitter 通过绑定 `LuaViewerVM.IsVisible` 控制 Visibility
- [x] 5.3 面板显示时通过 code-behind 或 ViewModel 增加窗口宽度，面板隐藏时缩回原始宽度，确保 JsonEditor 宽度始终不变

## 6. 验证

- [ ] 6.1 打开含 `Lua/` 子文件夹的工作区，选中有对应 Lua 文件的行，验证面板显示并加载内容（含语法高亮和行号）
- [ ] 6.2 选中无对应 Lua 文件的行，验证面板隐藏
- [ ] 6.3 编辑 Lua 内容后切换行，验证修改被自动保存到磁盘
