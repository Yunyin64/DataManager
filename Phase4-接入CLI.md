# 阶段 4：接入 CLI

把 `integration/src/` 的 CLI SDK 真正跑起来，让外部进程（AI Agent / 脚本）能通过 `common-cli.exe` 查询和修改 DataManager 管理的数据。

## 架构

```
外部进程 (AI Agent / 脚本 / 终端)
    │
    │  common-cli.exe DataManager auto query Traits --id brave
    ▼
┌──────────────────────────────────────────────┐
│  common-cli.exe                              │
│  → 读 registry.json 找存活工作区               │
│  → 探活 Named Pipe                            │
│  → 发送 {"args":["query","Traits",            │
│          "--id","brave"]}                      │
└───────────────────┬──────────────────────────┘
                    │ Named Pipe
                    │ \\.\pipe\common-cli-DataManager-{workspace}
                    ▼
┌──────────────────────────────────────────────┐
│  DataManager WPF App                         │
│                                              │
│  CliService                                  │
│    ├─ CommonCliServer (4 worker threads)     │
│    ├─ WorkspaceRegistry                      │
│    └─ 命令路由 → handler                      │
│         │                                    │
│         │ Dispatcher.Invoke                  │
│         ▼                                    │
│    DataWorkspace (内存中的 JSON 数据)          │
│         │                                    │
│         ▼                                    │
│    CliResponse → JSON 结果 → stdout          │
└──────────────────────────────────────────────┘
```

## CLI 命令体系设计

| 命令 | 用途 | 示例 |
|------|------|------|
| `list-files` | 列出当前工作区所有 JSON 文件 | `common-cli.exe DataManager auto list-files` |
| `get <file>` | 获取指定文件的全部内容 | `common-cli.exe DataManager auto get Traits` |
| `query <file> [--id <id>] [--path <jsonpath>]` | 按条件查询数据 | `common-cli.exe DataManager auto query Traits --id brave` |
| `set <file> --path <jsonpath> --value <json>` | 修改指定节点 | `common-cli.exe DataManager auto set Traits --path "[0].displayName" --value "\"英勇\""` |
| `save [<file>]` | 保存指定文件或全部脏文件 | `common-cli.exe DataManager auto save Traits` |
| `status` | 查看工作区状态（已加载文件、脏文件列表） | `common-cli.exe DataManager auto status` |

## 任务清单

- [ ] 4.1 实现 `CliService` 封装
  - 封装 `CommonCliServer` + `WorkspaceRegistry` 的完整生命周期
  - 提供 `Start(toolName, workspaceId, handler)` 和 `Stop()` 方法
  - workspaceId 策略：基于工作区文件夹路径生成（路径转义）

- [ ] 4.2 App 启动/退出时的生命周期集成
  - `App.OnStartup` → 启动 CliService，注册工作区
  - `App.OnExit` → 停止 CliService，注销工作区
  - 打开新文件夹时：注销旧工作区 → 注册新工作区

- [ ] 4.3 实现命令路由
  - `args[0]` 分发到各 handler 方法
  - 未知命令返回 `CliResponse.Fail("unknown command: xxx")`
  - 使用 `CliArgParser` 解析 `--id`, `--path`, `--value` 等选项

- [ ] 4.4 实现 `list-files` 命令
  - 返回当前工作区所有 JSON 文件列表
  - 输出 JSON 格式：`{"files": [{"name": "Traits", "path": "...", "dirty": false}]}`

- [ ] 4.5 实现 `get` 命令
  - 返回指定文件的完整 JSON 内容
  - 文件名匹配：忽略 `.json` 后缀（`get Traits` = `get Traits.json`）

- [ ] 4.6 实现 `query` 命令
  - `--id <id>` — 在根数组中查找 `id` 字段匹配的条目
  - `--path <jsonpath>` — JSONPath 查询
  - 返回匹配到的 JSON 片段

- [ ] 4.7 实现 `set` 命令
  - `--path <jsonpath>` + `--value <json>` — 修改指定节点
  - 修改后标记文件为 dirty
  - **通过 Dispatcher.Invoke 切到 UI 线程**，保证 GUI 同步刷新

- [ ] 4.8 实现 `save` 命令
  - 保存指定文件 / 保存所有脏文件
  - 返回保存结果

- [ ] 4.9 实现 `status` 命令
  - 返回工作区路径、已加载文件数、脏文件列表

- [ ] 4.10 线程安全保障
  - 所有读写 DataWorkspace 数据的 handler 都通过 `Dispatcher.Invoke` 切到 UI 线程
  - 避免竞态条件
  - 异常统一捕获，返回 `CliResponse.Fail(ex.Message)`

## 完成标准

- DataManager 启动后，终端执行 `common-cli.exe DataManager auto status` 能成功返回工作区信息
- `list-files` / `get` / `query` 能正确返回数据
- `set` 修改数据后，GUI 界面能实时刷新看到变更
- `save` 能将修改写回磁盘
- GUI 关闭后，`common-cli.exe DataManager auto status` 报告"没有活动的工作区"
