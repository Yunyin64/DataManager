## Context

阶段 2 已交付完整的数据层：`DataWorkspace`（工作区）、`JsonDataFile`（文件抽象）、`JsonNodeVM`（JSON 节点 ViewModel 包装）、`JsonFileService`（文件读写服务）。所有 ViewModel 继承自 `ViewModelBase`（`ObservableObject`），项目使用 `CommunityToolkit.Mvvm` 8.x 和 `Newtonsoft.Json` 13.x。

现有的 UI 文件（`MainWindow.xaml`、`FileTreeView.xaml`、`JsonEditorView.xaml`）和对应 ViewModel 均为空壳占位。目标是将数据层接入 WPF 界面，实现基本的 JSON 浏览工作流。

项目为 .NET 8.0 WPF 应用，使用 MVVM 模式。

## Goals / Non-Goals

**Goals:**
- 用户可以通过界面打开文件夹并浏览其中所有 JSON 文件
- 选中文件后以 DataGrid 表格形式展示 JSON 数据（Array of Objects → 行列映射）
- 简单值直接显示，复杂值（嵌套 Object/Array）浓缩为一行文本
- 状态栏实时反映当前工作状态
- 基础样式区分不同数据类型

**Non-Goals:**
- 不实现 JSON 编辑功能（本阶段只读浏览）
- 不实现深层嵌套数据的展开/弹窗交互（复杂值只做浓缩文本显示）
- 不实现搜索、过滤、排序功能
- 不处理非 Array-of-Objects 的 JSON 结构（如单个 Object 或纯值）——后续阶段扩展

## Decisions

### D1: ViewModel 通信模式 — 嵌套持有

**决策**：`MainViewModel` 直接持有 `FileTreeViewModel` 和 `JsonEditorViewModel` 实例，通过属性绑定和方法调用进行通信。

**替代方案**：
- WeakReferenceMessenger（CommunityToolkit.Mvvm 提供）——解耦好但对 3 个 VM 过度设计
- 事件/委托——经典但引入额外的事件声明

**理由**：项目规模小（3 个 ViewModel），直接持有最简单直观。文件选中变化通过 MainViewModel 中转，调用 JsonEditorViewModel 的加载方法。

### D2: JSON 展示方式 — DataGrid 混合列（思路 B）

**决策**：使用 WPF `DataGrid`，根据 JSON Array 中第一个 Object 的属性名动态生成列。

列值处理规则：
- 简单值（string/number/bool/null）→ 直接显示原始值
- 嵌套 Array → 显示 `[N items] {浓缩内容}`，如 `[1] {move_speed, flat, 0.3}`
- 嵌套 Object → 显示 `{N fields} {浓缩内容}`

**替代方案**：
- TreeView + HierarchicalDataTemplate——适合深层嵌套但不直观
- 完全展平所有嵌套路径为列——列数会爆炸

**理由**：游戏数据 JSON 通常是"扁平表 + 少量嵌套"结构，DataGrid 表格视图最符合策划习惯。

### D3: DataGrid 数据绑定 — 行 ViewModel 包装

**决策**：创建 `JsonRowViewModel` 类，将每个 JSON Object 包装为一行。每行持有一个 `Dictionary<string, object?>` 存储列值，通过 Indexer `this[columnName]` 供 DataGrid 绑定。

**理由**：DataGrid 的 `DataGridTextColumn` 可以绑定 `[ColumnName]` 路径，配合自定义 `ICustomTypeDescriptor` 或 `DynamicObject` 实现动态属性访问。使用 `DynamicObject` 最简洁。

### D4: 文件夹选择对话框 — WinForms FolderBrowserDialog

**决策**：直接使用 `System.Windows.Forms.FolderBrowserDialog`，通过在 csproj 中添加 `<UseWindowsForms>true</UseWindowsForms>` 启用。

**替代方案**：
- Ookii.Dialogs.Wpf 第三方包——功能好但多一个依赖
- P/Invoke SHBrowseForFolder——代码复杂

**理由**：.NET 8 SDK 已内置 WinForms 支持，无需额外 NuGet 包，一行配置即可。

### D5: View 与 ViewModel 绑定 — DataContext 注入

**决策**：`MainWindow` 在构造函数中创建 `MainViewModel` 并设为 `DataContext`。子 UserControl（FileTreeView、JsonEditorView）通过 XAML 绑定 MainViewModel 的子 ViewModel 属性。

### D6: 列类型颜色区分 — 单元格样式

**决策**：在 `Styles.xaml` 中定义不同 JSON 值类型的前景色。DataGrid 单元格通过 ValueConverter 根据值的类型返回对应颜色。

颜色方案：
- String → 橙色 `#D19A66`
- Number → 紫色 `#C678DD`
- Boolean true → 绿色 `#98C379`，false → 红色 `#E06C75`
- Null → 灰色 `#5C6370`
- 复杂值（Object/Array）→ 蓝色 `#61AFEF`

## Risks / Trade-offs

- **[风险] 非表格形 JSON**：如果 JSON 根节点不是 Array of Objects（如单个 Object、纯数组、嵌套树），当前方案无法展示。→ 缓解：本阶段只处理 Array of Objects，其他结构显示提示文本"不支持的 JSON 结构"，后续阶段扩展。

- **[风险] 列数过多**：如果 Object 属性非常多（>20列），DataGrid 水平滚动体验差。→ 缓解：DataGrid 原生支持水平滚动，暂不优化。

- **[风险] 大文件性能**：如果 JSON 数组有数千条记录，DataGrid 可能卡顿。→ 缓解：DataGrid 默认启用虚拟化（`VirtualizingStackPanel`），对几千行数据足够。

- **[权衡] 只读 vs 可编辑**：本阶段 DataGrid 设为 `IsReadOnly=True`，牺牲编辑能力换取实现简单性。后续阶段加编辑功能。
