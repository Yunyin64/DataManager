# common-cli 集成 SDK

把 `common-cli.exe` 接入你自己的 GUI 工具，让 AI Agent (Claude Code / Cursor / Codex 等) 能通过命令行操控它。

## 这是什么

`common-cli.exe` 是一个 **800 行 C++ 写成的通用桥** —— 它本身不绑任何工具，纯粹做"shell 命令 ↔ Named Pipe"翻译。

要让你的工具能被 AI 用起来，你需要在 **工具进程内** 集成两段代码：

| 组件 | 作用 | 必须 |
|---|---|---|
| `WorkspaceRegistry` | 把当前工作区写到共享注册表，让 cli `auto`/`list` 能发现 | 是 |
| `CommonCliServer` | 监听 Named Pipe，收到 JSON 请求 → 调你的 handler → 返回 JSON | 是 |
| `CliArgParser` | 解析子命令的 `-x`/`--xxx` 选项（业务侧用） | 可选 |

外加把 SDK 和 Skill 文档部署到 AI 工作目录的辅助代码（见 `examples/DeployHelper.cs`）。

## 5 分钟集成

前提：你已经把 `common-cli.exe` 准备好（编译产物或直接拷贝过来）。

### 1. 把三个源文件丢进你的项目

```
src/CommonCliServer.cs
src/WorkspaceRegistry.cs
src/CliArgParser.cs   ← 可选
```

命名空间是中性的 `CommonCli`，按需改。依赖：
- `Newtonsoft.Json` (WorkspaceRegistry 用)
- `System.IO.Pipes` + `System.IO.Pipes.AccessControl` (NuGet 包：`System.IO.Pipes.AccessControl`)

### 2. 启动时注册工作区 + 起服务

```csharp
using CommonCli;

// 工具启动时
var registry = new WorkspaceRegistry("MyTool");           // 工具名（任取，但要全局唯一）
registry.Register("project1", "我的第一个项目");           // 工作区 id + 描述

var server = new CommonCliServer("MyTool", "project1", args =>
{
    // args 是 cli 端传来的参数数组
    if (args.Length == 0)
        return CliResponse.Fail("缺少命令");

    return args[0] switch
    {
        "ping"  => CliResponse.Success("pong"),
        "hello" => CliResponse.Success($"hello, {args.ElementAtOrDefault(1) ?? "world"}"),
        _       => CliResponse.Fail($"未知命令: {args[0]}")
    };
});
server.Start();

// 工具退出时
server.Stop();
registry.Unregister("project1");
registry.Dispose();
```

### 3. 在终端测试

```bash
common-cli.exe MyTool list
# {"workspaces":[{"id":"project1","description":"我的第一个项目"}]}

common-cli.exe MyTool auto ping
# pong

common-cli.exe MyTool project1 hello Alice
# hello, Alice
```

通了之后，再去看 `DEPLOYMENT.md` 把 SDK 部署到 AI 工作目录，让 Claude Code / Cursor 知道怎么调你的工具。

## 文档导航

| 文档 | 内容 |
|---|---|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | 整体架构、5 层叠加机制、数据流 |
| [PROTOCOL.md](./PROTOCOL.md) | Named Pipe + JSON 协议规范（语言无关，给非 C# 重写参考） |
| [DEPLOYMENT.md](./DEPLOYMENT.md) | 把 cli + Skill 部署到 AI 工作目录的全流程 |
| [src/](./src/) | 三个核心源文件，可直接拷走 |
| [templates/](./templates/) | SKILL.md 模板、settings.local.json 权限片段 |
| [examples/](./examples/) | 最小可运行例子 + SchemaMaster 真实接入案例 |

## 设计要点速查

| 资源 | 命名约定 |
|---|---|
| Named Pipe | `\\.\pipe\common-cli-{ToolName}-{WorkspaceId}` |
| 注册表文件 | `%LOCALAPPDATA%\common-cli\{ToolName}\registry.json` |

| 行为 | 说明 |
|---|---|
| 多工作区 | 同 ToolName 不同 WorkspaceId，cli 端用 `list`/`auto` 路由 |
| 多工具共存 | 不同 ToolName 互不干扰，Pipe/registry 都隔离 |
| 死实例自愈 | cli 端 `list`/`auto` 时探活每个 pipe，连不上就从 registry 清理 |
| handler 线程 | 在 IO 线程跑，访问 UI 资源需自己 Invoke 主线程 |
| 并发 | server 默认起 4 个 worker 线程，可通过 `Start(maxClients: N)` 调整 |

## 为什么不用 MCP

- MCP 默认是 Agent fork stdio 子进程当 server，但 GUI 工具是用户手动开的常驻进程
- MCP 一对一配对，不天然支持"一个工具开多个工作区"
- 这套用 shell 命令 + Named Pipe，**任何**支持 Bash 工具的 AI Agent 都能用，不绑协议

详见 [ARCHITECTURE.md](./ARCHITECTURE.md) 的对比章节。
