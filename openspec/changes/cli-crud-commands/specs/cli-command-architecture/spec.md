## ADDED Requirements

### Requirement: ICliCommand 接口定义
系统 SHALL 定义 `ICliCommand` 接口，包含 `Name` 属性（string）和 `Execute(CliParseResult)` 方法（返回 `CliResponse`）。

#### Scenario: 命令实现接口
- **WHEN** 创建一个新的 CLI 命令类
- **THEN** 该类 MUST 实现 `ICliCommand` 接口

### Requirement: CliCommandBase 抽象基类
系统 SHALL 提供 `CliCommandBase` 抽象基类实现 `ICliCommand`，封装 `InvokeOnUI()`、`GetWorkspaceOrThrow()`、`FindFile()` 公共方法。子类通过构造函数接收 `Dispatcher`、`Func<DataWorkspace?>` 和可选的 `Action? onDataModified`。

#### Scenario: 基类提供 UI 线程调度
- **WHEN** 命令需要访问 DataWorkspace
- **THEN** 通过基类 `InvokeOnUI()` 在 UI 线程执行操作

#### Scenario: 基类提供工作区校验
- **WHEN** 调用 `GetWorkspaceOrThrow()` 且无工作区加载
- **THEN** 抛出 `InvalidOperationException`

### Requirement: CliService 路由分发
`CliService` SHALL 维护 `Dictionary<string, ICliCommand>` 命令注册表。`HandleCommand` 方法 SHALL 仅做参数解析和命令路由，不包含业务逻辑。

#### Scenario: 已知命令路由
- **WHEN** 收到已注册的命令名
- **THEN** 调用对应 `ICliCommand.Execute()` 并返回结果

#### Scenario: 未知命令
- **WHEN** 收到未注册的命令名
- **THEN** 返回 `CliResponse.Fail("unknown command: {name}")`

### Requirement: 现有命令拆分
系统 SHALL 将 `status`、`list-files`、`get`、`query`、`set`、`save` 六个命令从 `CliService` 拆分为独立的 Command 类文件，每个文件对应一个命令。拆分后行为 MUST 与原实现完全一致。

#### Scenario: 拆分后行为兼容
- **WHEN** 通过 CLI 调用任一已有命令
- **THEN** 返回结果与拆分前完全相同
