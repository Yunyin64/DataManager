## Context

DataManager 当前布局为三栏：FileTree | GridSplitter | JsonEditor。JsonEditor 将 JSON 数组显示为表格，用户通过点击行来查看数据。工作区文件夹内存在 `Lua/` 子目录，其中每个 `.lua` 文件以 JSON 数据的 `ID` 字段命名，一一对应。

当前用户需要在外部编辑器中手动查找并打开对应 Lua 文件，工作流被打断。

## Goals / Non-Goals

**Goals:**
- 选中 JSON 表格行时，自动在右侧面板加载对应的 `Lua/{ID}.lua` 文件
- 面板可编辑，切换行时自动保存上一个文件的修改
- 面板顶部预留功能按钮栏（编号 1、2、4、5），为后续针对 Lua 文件的操作功能留位
- 无对应 Lua 文件时面板自动隐藏

**Non-Goals:**
- 功能按钮的实际逻辑实现（当前只是占位预留）
- 创建新 Lua 文件（只有文件存在时才显示）
- Lua 文件的外部变更监听（不做 FileSystemWatcher）

## Decisions

### 1. 编辑器组件：AvalonEdit

**选择**: `AvaloniaEdit`（NuGet 包 `AvalonEdit`），提供语法高亮、行号显示、代码折叠等专业代码编辑体验。

**替代方案**: WPF 原生 TextBox（无需新包，但无语法高亮、无行号）。

**理由**: Lua 脚本查阅/编辑是核心使用场景，语法高亮和行号对代码可读性至关重要。AvalonEdit 是成熟的 WPF 代码编辑器控件，MIT 协议，支持 .NET 8。引入 NuGet 包 `AvalonEdit 6.*`。

### 2. 保存策略：切换行时自动保存

**选择**: 用户选中新行时，如果当前 Lua 内容有修改（IsDirty），立即 `File.WriteAllText` 写回磁盘。

**替代方案**:
- 手动 Ctrl+S：更安全但打断浏览流程
- 失焦保存：时机不明确

**理由**: 用户快速浏览多行数据时不想被弹窗打断。Lua 文件通常不大，写入开销低。

### 3. 面板布局：窗口扩展式

**选择**: Lua 面板作为窗口右侧的**扩展区域**，不占用 JsonEditor 的空间。实现方式：
- MainWindow Grid 新增第 4、5 列（GridSplitter + LuaViewer），LuaViewer 列 `Width` 为固定值（如 350）
- 面板显示时，通过调整 `Window.Width` 增加窗口宽度以容纳面板
- 面板隐藏时，缩回原始窗口宽度，LuaViewer 列 `Width=0`、Splitter `Visibility=Collapsed`
- JsonEditor 列始终为 `Width="*"`，其实际渲染宽度不受面板显隐影响

**替代方案**: 面板显隐时让 JsonEditor 和 Lua 共享固定窗口宽度（挤占式）。

**理由**: 用户明确要求 Lua 面板是"扩展"而非"挤占"。JsonEditor 在面板出现和消失时应保持相同的宽度体验。

### 4. 行选中信号传递：JsonEditorVM 暴露 SelectedRowId

**选择**: JsonEditorViewModel 新增 `[ObservableProperty] string? _selectedRowId`。JsonEditorView.xaml.cs 监听 DataGrid 的 `CurrentCellChanged` 事件，从当前行取 "ID" 列值写入 ViewModel。MainViewModel 监听该属性变化驱动 LuaViewer。

**理由**: 符合 MVVM 模式，View 只负责获取选中行 → 写入 VM 属性，逻辑在 VM 层流转。

### 5. ID 字段约定

**选择**: 固定精确匹配字段名 `"ID"`（大写）。映射路径：`{workspace}/Lua/{ID}.lua`。

## Risks / Trade-offs

- **[大文件性能]** AvalonEdit 对大文件性能良好（虚拟化渲染），基本无风险
- **[自动保存误写]** 用户意外修改内容后切换行导致误保存 → 接受此风险，Lua 文件应有版本控制兜底
- **[ID 字段缺失]** 某些 JSON 文件可能没有 "ID" 列 → 此时 SelectedRowId 为 null，面板不显示，不影响功能
- **[Lua 文件夹不存在]** 工作区内无 `Lua/` 目录 → File.Exists 自然返回 false，面板隐藏，无异常
- **[新 NuGet 依赖]** 引入 AvalonEdit 6.* → 包体积小、MIT 协议、维护活跃，风险可控
