## Why

当前 CLI 只支持 `get`/`query`/`set`/`save`/`status`/`list-files`，缺少"增"和"删"操作，外部 Agent 无法通过 CLI 完成完整 CRUD 流程。同时所有命令 handler 挤在 `CliService.cs` 一个文件里，随着命令增多会变得难以维护。

## What Changes

- 将 `CliService` 中所有命令 handler 拆分为独立 Command 类，每个命令一个 CS 文件
- 引入 `ICliCommand` 接口，`CliService` 变为纯路由层
- 新增 `add` 命令 — 向根数组追加一条 JSON 条目
- 新增 `delete` 命令 — 按 id 从根数组删除一条条目
- 新增 `update` 命令 — 按 id 匹配并整条替换
- 新增 `batch-add` 命令（占位，仅文件+注释，不实现）
- 新增 `batch-update` 命令（占位，仅文件+注释，不实现）

## Capabilities

### New Capabilities
- `cli-command-architecture`: CLI 命令架构 — ICliCommand 接口、Command 基类、CliService 路由重构
- `cli-crud-operations`: CLI CRUD 操作 — add/delete/update 命令实现 + batch-add/batch-update 占位

### Modified Capabilities

（无已有 specs）

## Impact

- `DataManager/Cli/CliService.cs` — 大幅重构，handler 逻辑移出，变为路由分发
- `DataManager/Cli/Commands/` — 新建文件夹，包含所有命令类
- 不涉及新 NuGet 依赖
- 不涉及 UI 层变更
- CLI 对外命令协议不变（`status`/`get`/`query`/`set`/`save`/`list-files` 行为兼容），新增 `add`/`delete`/`update`/`batch-add`/`batch-update`
