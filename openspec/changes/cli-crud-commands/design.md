## Context

`CliService.cs` 当前承担两个职责：管道生命周期管理 + 所有命令 handler 实现。6 个命令的 handler 全写在一个文件里（约 450 行），本次要新增 5 个命令，如果不拆分将膨胀到 700+ 行且耦合严重。

数据模型约定：JSON 文件根节点通常为 `JArray`，每个条目是 `JObject` 且有 `id` 字段作为主键。所有增删改查围绕此模型设计。

## Goals / Non-Goals

**Goals:**
- 每个 CLI 命令独立一个 CS 文件，职责单一
- `CliService` 精简为路由分发 + 生命周期管理
- 完成 CRUD 闭环：新增 `add`、`delete`、`update` 命令
- `batch-add`、`batch-update` 建占位文件留接口

**Non-Goals:**
- 不改变管道通信协议（CommonCliServer 不动）
- 不改变参数解析器（CliArgParser 不动）
- 不实现 batch 命令的业务逻辑
- 不引入依赖注入容器（命令注册用手动字典）
- 不做 JSON Schema 校验

## Decisions

### D1: 引入 ICliCommand 接口 + CliCommandBase 抽象基类

**选择**: 接口 + 抽象基类双层结构

```
ICliCommand
  string Name { get; }
  HashSet<string> Aliases → 命令别名（可选）
  CliResponse Execute(CliParseResult parsed)

CliCommandBase : ICliCommand
  持有 Dispatcher, Func<DataWorkspace?>, Action? onDataModified
  提供 InvokeOnUI(), GetWorkspaceOrThrow(), FindFile() 辅助方法
```

**理由**: 现有 6 个 handler 都重复 `InvokeOnUI` + `GetWorkspaceOrThrow` + `FindFile` 逻辑，抽到基类消除重复。接口保持可测试性。

**替代方案**: 纯接口 + 每个命令自己持有依赖。拒绝原因：大量重复代码。

### D2: 命令注册方式 — 手动字典

**选择**: `CliService` 构造时 `new` 所有 Command 并放入 `Dictionary<string, ICliCommand>`

```csharp
_commands = new Dictionary<string, ICliCommand>
{
    { "status", new StatusCommand(...) },
    { "add", new AddCommand(...) },
    ...
};
```

**理由**: 项目规模小（~11 个命令），反射扫描或 DI 容器过度工程。

### D3: CliService 中辅助方法的迁移策略

**选择**: `InvokeOnUI`、`GetWorkspaceOrThrow`、`FindFile` 迁移到 `CliCommandBase` 基类。`GenerateWorkspaceId` 保留在 `CliService`（仅路由层使用）。

### D4: 新命令参数设计

| 命令 | 位置参数 | 选项 | 说明 |
|------|---------|------|------|
| `add` | `<file>` | `--id <id>` | 追加 `{"id":"<id>"}` 到根数组 |
| `delete` | `<file>` | `--id <id>` | 按 id 删除条目 |
| `update` | `<file>` | `--id <id> --path <rel-path> --value <json>` | 按 id 定位条目，相对路径 upsert 属性 |
| `batch-add` | `<file>` | `--value <json-array>` | 批量追加（每条含 id） |
| `batch-update` | `<file>` | `--value <json-array>` | 占位 |

**`update` 的 path 是相对于匹配条目的路径**（不是全局 JSONPath）。例如 `--path "stats.atk"` 操作 `entry["stats"]["atk"]`。upsert 语义：属性不存在则创建。

新增选项别名无需额外添加（`--id`/`--value`/`--path` 已有 `-i`/`-v`/`-p` 别名）。

### D5: 文件组织

```
DataManager/Cli/
├── Commands/
│   ├── ICliCommand.cs          # 接口
│   ├── CliCommandBase.cs       # 抽象基类
│   ├── StatusCommand.cs
│   ├── ListFilesCommand.cs
│   ├── GetCommand.cs
│   ├── QueryCommand.cs
│   ├── SetCommand.cs
│   ├── SaveCommand.cs
│   ├── AddCommand.cs           # 新
│   ├── DeleteCommand.cs        # 新
│   ├── UpdateCommand.cs        # 新
│   ├── BatchAddCommand.cs      # 占位
│   └── BatchUpdateCommand.cs   # 占位
├── CliArgParser.cs             # 不动
├── CliService.cs               # 精简为路由
├── CommonCliServer.cs          # 不动
└── WorkspaceRegistry.cs        # 不动
```

## Risks / Trade-offs

- **[拆分后行为回归]** → 迁移时严格保持现有 handler 逻辑不变，仅搬移代码
- **[Aliases/ValueOptions 分散]** → 每个 Command 可声明自己需要的 aliases/valueOptions，CliService 聚合后传给 Parser。或保持现有全局定义不变。选后者（简单，命令少）
- **[batch 占位文件长期不实现]** → 占位类返回 `CliResponse.Fail("not implemented")`，Agent 调用时能得到明确提示
