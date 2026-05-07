# 阶段 3：基本功能实现 — 读取显示 JSON

把阶段 2 的数据模型接到 WPF 界面上，让用户能打开文件夹、浏览 JSON 文件、查看内容。

## 界面设计

```
┌─────────────────────────────────────────────────────────────┐
│  DataManager                                         [─][□][×]│
├───────────────────────────────────────┬─────────────────────┤
│  工具栏: [📂 打开文件夹] [💾 保存]                             │
├──────────────┬────────────────────────┴─────────────────────┤
│ 文件导航面板  │  JSON 内容视图                                 │
│              │                                              │
│ 📄 Traits    │  ┌─ [0] ─────────────────────────────┐      │
│ 📄 Items     │  │  id: "brave"                       │      │
│ 📄 Skills    │  │  displayName: "勇敢"               │      │
│              │  │  description: "面对危险时毫不退缩…"  │      │
│              │  │  ▶ StatModifiers: [1 item]          │      │
│              │  ├─ [1] ─────────────────────────────┤      │
│              │  │  id: "coward"                       │      │
│              │  │  displayName: "胆小"               │      │
│              │  │  ...                                │      │
│              │  └───────────────────────────────────┘      │
│              │                                              │
├──────────────┴──────────────────────────────────────────────┤
│ 状态栏: 已加载 3 个文件 | Traits.json - 8 条记录             │
└─────────────────────────────────────────────────────────────┘
```

## 任务清单

- [ ] 3.1 实现 MainWindow 布局
  - 顶部工具栏（ToolBar）
  - 左右分栏（Grid + GridSplitter）
  - 底部状态栏（StatusBar）
  - 绑定 `MainViewModel`

- [ ] 3.2 实现 MainViewModel
  - `OpenFolderCommand` — 打开文件夹对话框，创建 DataWorkspace 并加载
  - `SaveCommand` — 保存当前/全部脏文件
  - `CurrentWorkspace` — 当前工作区
  - `SelectedFile` — 当前选中的文件
  - 状态栏文本绑定

- [ ] 3.3 实现 FileTreeView
  - 左侧文件列表面板（UserControl）
  - `ListView` 或 `TreeView` 绑定 `DataWorkspace.Files`
  - 显示文件名，脏文件名后加 `*` 标记
  - 选中文件时通知 ViewModel

- [ ] 3.4 实现 JsonEditorView
  - 右侧 JSON 树形展示面板（UserControl）
  - `TreeView` + `HierarchicalDataTemplate` 递归绑定 `JsonNodeViewModel`
  - Object 节点显示 `{} key`，Array 节点显示 `[] key [N items]`
  - 叶节点显示 `key: value`

- [ ] 3.5 文件选中 → 内容显示
  - 点击左侧文件 → 右侧切换到该文件的 JSON 树
  - 文件未加载时触发懒加载

- [ ] 3.6 JSON 节点展开/折叠
  - Object / Array 节点默认折叠，可点击展开
  - 第一层默认展开（提升浏览体验）

- [ ] 3.7 状态栏信息
  - 显示已加载文件数量
  - 显示当前文件名和记录数（如果根节点是数组）
  - 显示脏状态

- [ ] 3.8 基础样式与视觉区分
  - 不同节点类型用不同颜色/图标区分：
    - Object `{}` — 蓝色
    - Array `[]` — 绿色
    - String `""` — 橙色
    - Number `#` — 紫色
    - Boolean — 红/绿
    - Null — 灰色

## 完成标准

- 打开应用 → 选择文件夹 → 左侧显示所有 JSON 文件
- 点击文件 → 右侧展示 JSON 树形结构
- 树形结构能正确递归展开任意嵌套的 JSON
- 用 `Traits.json` 实际验证显示效果
