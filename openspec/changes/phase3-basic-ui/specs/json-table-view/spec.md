## ADDED Requirements

### Requirement: Array of Objects 表格展示
当 JSON 文件的根节点是 Array of Objects 时，JsonEditorView SHALL 以 DataGrid 表格形式展示数据。第一个 Object 的所有属性名 SHALL 作为列名，每个 Object SHALL 作为一行。

#### Scenario: 展示 Traits.json
- **WHEN** 用户选中一个根节点为 Array of Objects 的 JSON 文件（如 Traits.json）
- **THEN** DataGrid SHALL 显示列：`id`、`displayName`、`description`、`StatModifiers`
- **THEN** DataGrid SHALL 显示 8 行数据（每个 Object 一行）

### Requirement: 简单值直接显示
单元格值为简单类型（string、number、boolean、null）时 SHALL 直接显示原始值的文本表示。

#### Scenario: 字符串值显示
- **WHEN** 单元格值为字符串 `"brave"`
- **THEN** 单元格 SHALL 显示 `brave`

#### Scenario: 数字值显示
- **WHEN** 单元格值为数字 `0.3`
- **THEN** 单元格 SHALL 显示 `0.3`

#### Scenario: 布尔值显示
- **WHEN** 单元格值为布尔值 `true`
- **THEN** 单元格 SHALL 显示 `True`

#### Scenario: Null 值显示
- **WHEN** 单元格值为 `null`
- **THEN** 单元格 SHALL 显示 `null`

### Requirement: 复杂值浓缩显示
单元格值为嵌套 Array 或 Object 时 SHALL 显示浓缩文本摘要。

#### Scenario: 非空数组浓缩显示
- **WHEN** 单元格值为 `[{"statId":"move_speed","type":"flat","value":0.3}]`
- **THEN** 单元格 SHALL 显示类似 `[1] {move_speed, flat, 0.3}` 的浓缩文本

#### Scenario: 空数组显示
- **WHEN** 单元格值为空数组 `[]`
- **THEN** 单元格 SHALL 显示 `[]`

#### Scenario: 嵌套对象浓缩显示
- **WHEN** 单元格值为 Object `{"a":1,"b":2}`
- **THEN** 单元格 SHALL 显示类似 `{2} {a, b}` 的浓缩文本

### Requirement: 动态列生成
DataGrid 的列 SHALL 在文件加载时根据 JSON 数据动态生成，不依赖硬编码列定义。

#### Scenario: 不同结构的文件显示不同列
- **WHEN** 用户先选中 Traits.json（列：id, displayName, description, StatModifiers）
- **THEN** DataGrid SHALL 显示 4 列
- **WHEN** 用户切换到另一个有不同属性名的 JSON 文件
- **THEN** DataGrid SHALL 清除旧列并生成新列

### Requirement: 行号显示
DataGrid SHALL 在最左侧显示行号列（从 0 开始）。

#### Scenario: 行号显示
- **WHEN** DataGrid 显示 8 条数据
- **THEN** 最左侧列 SHALL 显示 0 到 7 的行号

### Requirement: 只读模式
DataGrid SHALL 为只读模式，不允许用户编辑单元格内容。

#### Scenario: 尝试编辑单元格
- **WHEN** 用户双击某个单元格
- **THEN** 单元格 SHALL 不进入编辑模式

### Requirement: 不支持的 JSON 结构提示
当 JSON 文件的根节点不是 Array of Objects 时，SHALL 显示提示文本而非表格。

#### Scenario: 根节点为单个 Object
- **WHEN** JSON 文件根节点为单个 Object（非 Array）
- **THEN** 视图 SHALL 显示提示文本 "暂不支持该 JSON 结构"

### Requirement: 值类型颜色区分
不同类型的值 SHALL 使用不同前景色显示，便于视觉区分。

#### Scenario: 颜色区分
- **WHEN** DataGrid 显示数据
- **THEN** 字符串值 SHALL 显示为橙色
- **THEN** 数字值 SHALL 显示为紫色
- **THEN** 布尔值 true SHALL 显示为绿色，false SHALL 显示为红色
- **THEN** null 值 SHALL 显示为灰色
- **THEN** 复杂值（Object/Array）SHALL 显示为蓝色
