## 1. 命令架构基础

- [x] 1.1 创建 `DataManager/Cli/Commands/` 文件夹
- [x] 1.2 创建 `ICliCommand.cs` — 定义接口（`Name`, `Execute(CliParseResult)`）
- [x] 1.3 创建 `CliCommandBase.cs` — 抽象基类，封装 `InvokeOnUI()`、`GetWorkspaceOrThrow()`、`FindFile()` 公共方法

## 2. 现有命令拆分

- [x] 2.1 创建 `StatusCommand.cs` — 从 CliService 迁移 HandleStatus 逻辑
- [x] 2.2 创建 `ListFilesCommand.cs` — 从 CliService 迁移 HandleListFiles 逻辑
- [x] 2.3 创建 `GetCommand.cs` — 从 CliService 迁移 HandleGet 逻辑
- [x] 2.4 创建 `QueryCommand.cs` — 从 CliService 迁移 HandleQuery 逻辑
- [x] 2.5 创建 `SetCommand.cs` — 从 CliService 迁移 HandleSet 逻辑
- [x] 2.6 创建 `SaveCommand.cs` — 从 CliService 迁移 HandleSave 逻辑

## 3. 新增 CRUD 命令

- [x] 3.1 创建 `AddCommand.cs` — 创建极简条目（`add <file> --id <id>` → 追加 `{"id":"<id>"}`，校验 id 不重复）
- [x] 3.2 创建 `DeleteCommand.cs` — 按 id 删除条目（`delete <file> --id <id>`）
- [x] 3.3 创建 `UpdateCommand.cs` — 按 id 定位 + 相对路径 upsert 属性（`update <file> --id <id> --path <rel-path> --value <json>`，支持嵌套路径如 `stats.atk`）
- [x] 3.4 创建 `BatchAddCommand.cs` — 批量追加条目（`batch-add <file> --value <json-array>`，校验 id 不重复）

## 4. 占位命令

- [x] 4.1 创建 `BatchUpdateCommand.cs` — 占位，返回 not implemented

## 5. CliService 重构

- [x] 5.1 重构 `CliService.cs` — 移除所有 handler 方法，改为 `Dictionary<string, ICliCommand>` 路由分发
- [x] 5.2 更新 Aliases / ValueOptions 确保新命令参数被正确解析

## 6. 文档

- [x] 6.1 生成 `integration/cli-guide.md` — 面向 Agent 的 CLI 指令使用指南（精简，列出所有命令用法、参数、示例）
