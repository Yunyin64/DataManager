## ADDED Requirements

### Requirement: Lua 面板根据选中行自动加载对应文件
当用户在 JsonEditor 表格中选中一行时，系统 SHALL 读取该行 "ID" 字段的值，拼接路径 `{workspace}/Lua/{ID}.lua`，若文件存在则在右侧面板中显示其内容。

#### Scenario: 选中有对应 Lua 文件的行
- **WHEN** 用户在 JsonEditor 中选中一行，该行 "ID" 字段值为 "wolf_01"，且 `Lua/wolf_01.lua` 文件存在
- **THEN** 右侧 Lua 面板显示，加载并展示 `wolf_01.lua` 的文本内容

#### Scenario: 选中无对应 Lua 文件的行
- **WHEN** 用户在 JsonEditor 中选中一行，该行 "ID" 字段值为 "bat_02"，且 `Lua/bat_02.lua` 文件不存在
- **THEN** 右侧 Lua 面板隐藏

#### Scenario: JSON 文件无 ID 列
- **WHEN** 当前加载的 JSON 文件不包含 "ID" 字段
- **THEN** 右侧 Lua 面板隐藏

### Requirement: Lua 面板内容可编辑
系统 SHALL 提供可编辑的文本输入区域，用户可直接在面板中修改 Lua 文件内容。

#### Scenario: 编辑 Lua 内容
- **WHEN** Lua 面板显示了某个 Lua 文件内容
- **THEN** 用户可以直接在文本区域中输入、删除、修改文本

### Requirement: 切换行时自动保存
当用户切换选中行时，如果当前 Lua 文件内容有修改，系统 SHALL 自动将修改写回磁盘，然后再加载新行对应的 Lua 文件。

#### Scenario: 有修改时切换行
- **WHEN** 用户修改了当前 Lua 面板中的内容（IsDirty = true），然后选中了另一行
- **THEN** 系统自动将修改内容写入原 Lua 文件路径，再加载新行对应的 Lua 文件

#### Scenario: 无修改时切换行
- **WHEN** 用户未修改当前 Lua 面板中的内容（IsDirty = false），然后选中了另一行
- **THEN** 系统直接加载新行对应的 Lua 文件，不执行写入操作

### Requirement: 面板显隐联动布局
Lua 面板 SHALL 作为窗口右侧的扩展区域存在，不占用 JsonEditor 的空间。面板显示时窗口变宽以容纳面板，面板隐藏时窗口缩回原始宽度，JsonEditor 的宽度始终不受影响。

#### Scenario: 面板隐藏时的布局
- **WHEN** 当前选中行无对应 Lua 文件（面板隐藏）
- **THEN** 窗口保持原始宽度，JsonEditor 宽度不变，不显示第二个 GridSplitter

#### Scenario: 面板显示时的布局
- **WHEN** 当前选中行有对应 Lua 文件（面板显示）
- **THEN** 窗口宽度增加以容纳 Lua 面板，JsonEditor 宽度不变，面板出现在右侧扩展区域

### Requirement: 功能按钮栏预留
Lua 面板顶部 SHALL 有一排功能按钮栏，预留 4 个按钮位（编号 1、2、4、5），当前按钮为占位状态（禁用或空标签），为后续针对当前 Lua 文件的操作功能预留位置。

#### Scenario: 功能按钮栏展示
- **WHEN** Lua 面板可见时
- **THEN** 面板顶部显示功能按钮栏，包含 4 个预留按钮位（编号 1、2、4、5），均为占位/禁用状态

### Requirement: 文件名显示
Lua 面板 SHALL 显示当前加载的 Lua 文件名，以便用户确认正在查阅的文件。

#### Scenario: 显示文件名
- **WHEN** Lua 面板加载了 `wolf_01.lua`
- **THEN** 面板中显示文件名 "wolf_01.lua"
