## ADDED Requirements

### Requirement: MainViewModel 持有子 ViewModel
MainViewModel SHALL 持有 FileTreeViewModel 和 JsonEditorViewModel 实例，并作为属性暴露给 View 层绑定。

#### Scenario: 子 ViewModel 可访问
- **WHEN** MainViewModel 初始化
- **THEN** MainViewModel.FileTreeVM SHALL 不为 null
- **THEN** MainViewModel.JsonEditorVM SHALL 不为 null

### Requirement: 文件选中联动
当用户在 FileTreeView 中选中一个文件时，MainViewModel SHALL 通知 JsonEditorViewModel 加载并显示该文件的数据。

#### Scenario: 选中文件触发内容切换
- **WHEN** 用户在文件列表中选中 `Traits.json`
- **THEN** JsonEditorViewModel SHALL 接收到该文件的 JsonDataFile
- **THEN** JsonEditorView SHALL 显示 Traits.json 的表格数据

#### Scenario: 切换选中文件
- **WHEN** 用户从 `Traits.json` 切换选中到 `Items.json`
- **THEN** JsonEditorView SHALL 清除 Traits.json 的数据并显示 Items.json 的数据

### Requirement: 状态栏信息显示
MainViewModel SHALL 维护状态栏文本，显示当前工作状态信息。

#### Scenario: 无工作区时状态栏
- **WHEN** 应用启动且未打开任何文件夹
- **THEN** 状态栏 SHALL 显示 "就绪"

#### Scenario: 打开文件夹后状态栏
- **WHEN** 用户打开包含 3 个 JSON 文件的文件夹
- **THEN** 状态栏 SHALL 显示 "已加载 3 个文件"

#### Scenario: 选中文件后状态栏
- **WHEN** 用户选中 Traits.json（根节点为含 8 个元素的 Array）
- **THEN** 状态栏 SHALL 显示 "已加载 3 个文件 | Traits.json - 8 条记录"

### Requirement: 保存命令
SaveCommand SHALL 调用 DataWorkspace.Save() 保存所有脏文件。

#### Scenario: 保存所有脏文件
- **WHEN** 用户点击"保存"按钮
- **THEN** 系统 SHALL 调用 DataWorkspace.Save()
- **THEN** 所有脏文件的 IsDirty SHALL 变为 false

#### Scenario: 无工作区时保存
- **WHEN** 未打开任何文件夹时用户点击"保存"按钮
- **THEN** 系统 SHALL 不做任何操作
