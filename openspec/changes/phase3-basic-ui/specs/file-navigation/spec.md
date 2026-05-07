## ADDED Requirements

### Requirement: 文件列表展示
FileTreeView SHALL 以列表形式展示当前工作区中所有 JSON 文件的文件名。

#### Scenario: 打开文件夹后显示文件列表
- **WHEN** 用户通过"打开文件夹"按钮选择了一个包含 JSON 文件的文件夹
- **THEN** 左侧文件列表 SHALL 显示该文件夹下所有 `.json` 文件的文件名

#### Scenario: 空文件夹
- **WHEN** 用户选择的文件夹下没有 JSON 文件
- **THEN** 左侧文件列表 SHALL 显示为空

### Requirement: 文件选中交互
用户 SHALL 能够点击文件列表中的文件名来选中该文件，选中的文件 SHALL 有视觉高亮。

#### Scenario: 选中文件
- **WHEN** 用户点击文件列表中的某个文件名
- **THEN** 该文件 SHALL 显示为选中状态（高亮）
- **THEN** 右侧 JSON 内容视图 SHALL 切换为该文件的数据

### Requirement: 脏文件标记
当文件被修改但未保存时，文件名后 SHALL 显示 `*` 标记。

#### Scenario: 文件被修改后显示脏标记
- **WHEN** 某个文件的 IsDirty 为 true
- **THEN** 该文件在文件列表中的显示名 SHALL 为 `文件名*`（如 `Traits.json*`）

#### Scenario: 文件保存后移除脏标记
- **WHEN** 脏文件被保存（IsDirty 变为 false）
- **THEN** 该文件的显示名 SHALL 恢复为原始文件名（无 `*` 后缀）

### Requirement: 打开文件夹命令
用户点击"打开文件夹"按钮后 SHALL 弹出文件夹选择对话框，选择文件夹后 SHALL 加载该文件夹为工作区。

#### Scenario: 成功打开文件夹
- **WHEN** 用户点击"打开文件夹"按钮
- **THEN** 系统 SHALL 弹出文件夹选择对话框
- **WHEN** 用户选择一个文件夹并确认
- **THEN** 系统 SHALL 创建 DataWorkspace 并加载该文件夹下的所有 JSON 文件
- **THEN** 左侧文件列表 SHALL 更新为新工作区的文件列表

#### Scenario: 取消选择文件夹
- **WHEN** 用户点击"打开文件夹"按钮后在对话框中点击取消
- **THEN** 系统 SHALL 不做任何操作，保持当前状态
