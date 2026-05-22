## ADDED Requirements

### Requirement: add 命令 — 创建极简条目
`add` 命令 SHALL 接收 `<file>` 位置参数和 `--id <id>` 选项，在文件根 JArray 末尾追加 `{"id":"<id>"}`（仅含 id 字段的空条目），并标记文件为 dirty。

#### Scenario: 成功创建
- **WHEN** 调用 `add monsters --id dragon_01`
- **THEN** 文件根数组末尾新增 `{"id":"dragon_01"}`，返回 `CliResponse.Success`

#### Scenario: 根节点非数组
- **WHEN** 目标文件根节点不是 JArray
- **THEN** 返回 `CliResponse.Fail("root is not an array")`

#### Scenario: 缺少 id 参数
- **WHEN** 调用 `add monsters`（无 `--id`）
- **THEN** 返回 `CliResponse.Fail` 包含 usage 提示

#### Scenario: id 已存在
- **WHEN** 根数组中已有 id 为 `dragon_01` 的条目
- **THEN** 返回 `CliResponse.Fail("id already exists: dragon_01")`

### Requirement: delete 命令 — 按 id 删除条目
`delete` 命令 SHALL 接收 `<file>` 位置参数和 `--id <id>` 选项，从根 JArray 中找到 `id` 字段匹配的 JObject 并移除，标记文件为 dirty。

#### Scenario: 成功删除
- **WHEN** 调用 `delete monsters --id slime_01`，且根数组中存在 id 为 `slime_01` 的条目
- **THEN** 该条目从数组移除，返回 `CliResponse.Success`

#### Scenario: id 不存在
- **WHEN** 指定的 id 在根数组中无匹配
- **THEN** 返回 `CliResponse.Fail("id not found: {id}")`

#### Scenario: 根节点非数组
- **WHEN** 目标文件根节点不是 JArray
- **THEN** 返回 `CliResponse.Fail("root is not an array")`

#### Scenario: 缺少 id 参数
- **WHEN** 调用 `delete monsters`（无 `--id`）
- **THEN** 返回 `CliResponse.Fail` 包含 usage 提示

### Requirement: update 命令 — 按 id 定位 + 相对路径 upsert 属性
`update` 命令 SHALL 接收 `<file>` 位置参数、`--id <id>`、`--path <relative-path>` 和 `--value <json>` 选项。先按 id 在根 JArray 中定位 JObject，然后以 path 作为**相对于该条目的属性路径**，执行 upsert（属性存在则替换，不存在则创建），标记文件为 dirty。

#### Scenario: 修改已有属性
- **WHEN** 条目 `{"id":"slime_01","hp":100}` 存在，调用 `update monsters --id slime_01 --path "hp" --value '500'`
- **THEN** 条目变为 `{"id":"slime_01","hp":500}`

#### Scenario: 新增不存在的属性（upsert）
- **WHEN** 条目 `{"id":"dragon_01"}` 存在，调用 `update monsters --id dragon_01 --path "name" --value '"龙"'`
- **THEN** 条目变为 `{"id":"dragon_01","name":"龙"}`

#### Scenario: 设置嵌套属性
- **WHEN** 条目 `{"id":"dragon_01","stats":{"atk":50}}` 存在，调用 `update monsters --id dragon_01 --path "stats.atk" --value '100'`
- **THEN** 条目变为 `{"id":"dragon_01","stats":{"atk":100}}`

#### Scenario: id 不存在
- **WHEN** 指定的 id 在根数组中无匹配
- **THEN** 返回 `CliResponse.Fail("id not found: {id}")`

#### Scenario: 根节点非数组
- **WHEN** 目标文件根节点不是 JArray
- **THEN** 返回 `CliResponse.Fail("root is not an array")`

#### Scenario: 缺少必要参数
- **WHEN** 调用 `update monsters` 缺少 `--id`、`--path` 或 `--value` 中任一项
- **THEN** 返回 `CliResponse.Fail` 包含 usage 提示

### Requirement: batch-add 命令 — 批量追加极简条目
`batch-add` 命令 SHALL 接收 `<file>` 位置参数和 `--value <json-array>` 选项，将 JSON 数组中每个对象追加到根 JArray，标记文件为 dirty。每个对象 MUST 包含 `id` 字段。

#### Scenario: 成功批量追加
- **WHEN** 调用 `batch-add monsters --value '[{"id":"a"},{"id":"b"}]'`
- **THEN** 根数组末尾追加两条条目，返回 `CliResponse.Success`

#### Scenario: 存在重复 id
- **WHEN** 根数组已有 id 为 `a` 的条目，value 中也包含 `{"id":"a"}`
- **THEN** 返回 `CliResponse.Fail` 指出重复 id

#### Scenario: 缺少 value 参数
- **WHEN** 调用 `batch-add monsters`（无 `--value`）
- **THEN** 返回 `CliResponse.Fail` 包含 usage 提示

### Requirement: batch-update 命令 — 占位
`batch-update` 命令 SHALL 作为占位存在，调用时返回 `CliResponse.Fail("not implemented")`。

#### Scenario: 调用占位命令
- **WHEN** 调用 `batch-update` 命令
- **THEN** 返回 `CliResponse.Fail("not implemented")`
