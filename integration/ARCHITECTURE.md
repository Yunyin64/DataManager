# 架构总览

## 全景图

```
┌──────────────────────────────────────────────────────────────────────┐
│                        终端 (AI Agent 进程)                            │
│                                                                      │
│   Claude Code / Cursor / Codex / 你写的脚本                           │
│              │                                                       │
│              │ Bash 工具 = subprocess.run                             │
│              ▼                                                       │
│   ┌──────────────────────────────────────────────┐                   │
│   │  common-cli.exe  (800 行 C++)                 │                   │
│   │  - 读 registry.json 找存活工作区               │                   │
│   │  - 探活 Named Pipe                            │                   │
│   │  - argv → JSON 请求                          │                   │
│   │  - JSON 响应 → stdout/stderr/exit code       │                   │
│   └──────────────────────────────────────────────┘                   │
│              │                                                       │
└──────────────┼───────────────────────────────────────────────────────┘
               │ Named Pipe: \\.\pipe\common-cli-{Tool}-{Workspace}
               │ JSON over '\n'-delimited stream
               ▼
┌──────────────────────────────────────────────────────────────────────┐
│                    GUI 工具进程 (你的 App)                             │
│                                                                      │
│   ┌──────────────────────────────────────────────┐                   │
│   │  CommonCliServer (4 worker threads)          │                   │
│   │  - 收 JSON → 调 handler → 回 JSON            │                   │
│   └────────┬─────────────────────────────────────┘                   │
│            │ handler 在 IO 线程                                       │
│            ▼                                                         │
│   ┌──────────────────────────────────────────────┐                   │
│   │  你的 handler (业务逻辑)                       │                   │
│   │  通常: Dispatcher.Invoke 切到 UI 线程         │                   │
│   └──────────────────────────────────────────────┘                   │
│                                                                      │
│   WorkspaceRegistry → %LOCALAPPDATA%\common-cli\{Tool}\registry.json  │
│                       (启动时注册自己, 关闭时注销)                      │
└──────────────────────────────────────────────────────────────────────┘
```

## 5 层叠加机制

"AI 直接 `Bash common-cli.exe MyTool auto query "..."` 就能控制 GUI" 这件事，
是 5 层独立机制叠出来的。每一层单独看都不神奇，叠起来才有效：

| 层 | 机制 | 谁负责 |
|---|---|---|
| 1 | `common-cli.exe` 在 PATH 里能找到 | 部署阶段把 exe 放到 `%LOCALAPPDATA%\Microsoft\WindowsApps` |
| 2 | AI Agent 知道有这个工具、怎么用 | `.claude/skills/<name>/SKILL.md` 喂"使用说明书" |
| 3 | AI 调 Bash 不用每次确认 | `.claude/settings.local.json` 加 `allow: ["Bash(*common-cli.exe *)"]` |
| 4 | Bash 工具能拿到 stdout/stderr/exit code | Agent 内置能力（Bash 工具 = `subprocess.run`） |
| 5 | cli 进程能找到 GUI 进程并通信 | `common-cli.exe` 内部用 Named Pipe + registry.json |

**每一层都是借用已有机制**，没有任何"AI 专用发明"。SDK 的工作就是把这 5 层串好。

## 三个核心组件的职责

### CommonCliServer

- 创建命名管道 `\\.\pipe\common-cli-{Tool}-{Workspace}`
- 起 N 个 worker 线程并发监听（默认 4）
- 每条连接：循环读 `\n` 结尾的 JSON 行 → 调 handler → 写回 JSON
- PipeSecurity 给 Everyone 读写权限（防止跨用户运行的 Agent 连不上）

### WorkspaceRegistry

- 维护 `%LOCALAPPDATA%\common-cli\{Tool}\registry.json`
- `Register(id, description)` → 追加/覆盖一条
- `Unregister(id)` → 删掉一条
- 用 `FileStream.Lock()` 做独占锁，防并发损坏
- 让 cli 端的 `list`/`auto` 能发现你的工作区

### CliArgParser (可选)

- 业务侧用：解析 `-t`/`--table` 这类 GNU 风格选项
- 区分 flag (布尔) 和 option (带值)
- 支持短选项别名映射（`-t` → `--table`）
- 不强制使用，你愿意自己 split argv 也行

## 数据流（一次完整调用）

```
[1] 用户在 Claude Code 里说: "查一下 Monster 表"
            │
            ▼
[2] Agent 匹配 Skill description, 加载 SKILL.md
            │
            ▼
[3] Agent 决定调用: Bash("common-cli.exe MyTool auto query 'SELECT * FROM Monster'")
            │
            ▼
[4] Bash 工具 (settings.local.json 已 allow) 起子进程
            │
            ▼
[5] common-cli.exe 启动:
    - 第一个 argv = "MyTool" → 决定 ToolName
    - 第二个 argv = "auto" → 触发自动选择模式
    - 读 %LOCALAPPDATA%\common-cli\MyTool\registry.json
    - 对每个 workspace 探活 \\.\pipe\common-cli-MyTool-{id}
    - 只有一个存活: 选定 id, 继续
            │
            ▼
[6] common-cli.exe 连 Pipe, 发送:
    {"args":["query","SELECT * FROM Monster"]}\n
            │
            ▼
[7] CommonCliServer worker 收到, 解析 args, 调你的 handler
            │
            ▼
[8] handler (在 IO 线程) Dispatcher.Invoke 切到 UI 线程,
    执行真实查询, 拿到结果
            │
            ▼
[9] handler 返回 CliResponse.Success(json), Server 序列化:
    {"code":0,"output":"...","error":""}\n
            │
            ▼
[10] common-cli.exe 收到, output → stdout, exit code = 0
            │
            ▼
[11] Bash 工具捕获 stdout, 作为 tool result 给 Agent
            │
            ▼
[12] Agent 看到查询结果, 整理成自然语言回复用户
```

## 与 MCP 的对比

| 维度 | MCP | common-cli |
|---|---|---|
| 通信协议 | JSON-RPC (stdio/SSE/HTTP) | 自定义 JSON over Named Pipe |
| 工具发现 | `tools/list` RPC | SKILL.md (人读+AI 读) |
| 参数 | JSON Schema 结构化 | shell argv 字符串数组 |
| Agent 调用 | 专用 MCP tool | 普通 Bash 工具 |
| 进程模型 | Agent fork stdio 子进程 | GUI 用户手动开, 常驻 |
| 多实例 | 不天然支持 | 注册表 + WorkspaceId 天生支持 |
| Agent 绑定 | 必须支持 MCP 协议 | 任何能跑 shell 的 Agent |

**适合用 common-cli 的场景**:
- GUI 应用是**用户手动启动**的（不能让 Agent 自己 fork）
- 同一个工具可能开**多个独立工作区**（要 routing）
- 想支持**多种 Agent**（Claude Code、Cursor、命令行脚本都行）
- 调试方便（终端直接敲命令测试）

**用 MCP 更合适的场景**:
- 工具是无状态服务（可被 Agent 随起随关）
- 一对一配对就够，不需要多实例
- 想要严格的参数 schema 校验
- 已经有成熟的 MCP server 框架可用

## 失败模式 / 边界情况

| 场景 | 表现 | 处理 |
|---|---|---|
| GUI 没启动 | cli 报 `Failed to connect to tool` | SKILL.md 教 Agent 提醒用户开 GUI |
| GUI 进程崩了 | Pipe 自动消失，cli 探活时清掉 registry 残留 | 自愈，无需人工干预 |
| 多个工作区 + `auto` | cli 退出码 1，输出工作区列表 | SKILL.md 教 Agent 询问用户选哪个 |
| handler 抛异常 | Server 兜底捕获，回 `{"code":1,"error":"..."}` | Agent 看到 stderr，可以诊断 |
| handler 死循环 | 整条 cli 卡住直到超时 | 业务侧自己加超时控制 |
| 跨用户运行 Agent | PipeSecurity 已给 Everyone 读写权限 | 默认就 OK |
