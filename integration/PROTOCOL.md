# 通信协议规范

本文档描述 `common-cli.exe` 和工具进程之间的通信协议，**语言无关**。
如果你想用 C# 以外的语言（Python / Go / Rust / Node 等）写服务端，按本文档实现即可。

## 资源命名约定

所有资源名基于两个变量拼接：`ToolName` (你的工具名) + `WorkspaceId` (工作区唯一标识)。

| 资源 | 命名格式 | 例子 |
|---|---|---|
| Named Pipe | `\\.\pipe\common-cli-{ToolName}-{WorkspaceId}` | `\\.\pipe\common-cli-MyTool-project1` |
| 注册表文件 | `%LOCALAPPDATA%\common-cli\{ToolName}\registry.json` | `C:\Users\xxx\AppData\Local\common-cli\MyTool\registry.json` |

**约束**:
- `ToolName` 只能用 ASCII 字母数字 + `-_`（避免文件路径和 pipe 名出问题）
- `WorkspaceId` 同上。SchemaMaster 实际用的是把工作区绝对路径做转义（`:` → `_`，`\` → `_`），保证唯一
- 大小写敏感

## 注册表文件 (registry.json)

工具进程启动时写入，关闭时清理。cli 端的 `list` / `auto` 通过它发现存活工作区。

### 文件格式

```json
{
  "workspaces": [
    {"id": "project1", "description": "我的第一个项目"},
    {"id": "project2", "description": "另一个项目 - D:/foo/bar"}
  ]
}
```

### 操作语义

| 操作 | 行为 |
|---|---|
| Register(id, desc) | 如已存在同 id，**覆盖**；否则追加 |
| Unregister(id) | 按 id 删除条目 |

### 并发控制

**必须用文件锁**，否则多个工具实例同时读写会损坏 JSON：

- Windows: `LockFileEx` (C++) / `FileStream.Lock()` (.NET)
- POSIX: `flock` 或 `fcntl`

锁住整个文件，读 → 修改 → 写回 → 解锁，原子完成。

### 死条目清理

**不需要工具进程主动清理**。cli 端 `list`/`auto` 时会探活每个 pipe，
连不上就当作死掉，从 registry 清掉。这样：
- 工具崩溃没注销不会留垃圾
- 你不需要写复杂的退出处理

## Named Pipe 通信

### Pipe 创建（服务端）

- 类型: `PIPE_TYPE_BYTE` (流式，不是消息式)
- 方向: `PIPE_ACCESS_DUPLEX` (双向)
- 模式: `Asynchronous` 推荐 (overlapped IO)
- 缓冲: 建议各 64KB
- 安全: 给 Everyone 读写权限（让跨用户的 Agent 也能连）

### 多客户端

cli 是短连接（一次请求一次响应就断开），但服务端要起 **多个并发实例** 的 pipe，
否则同时来两个 Agent 就堵了。

推荐做法：起 N 个 worker 线程，每个线程独立 `WaitForConnection` →
处理 → 关闭 → 循环。N 默认 4 够用。

### 报文格式

**单行 JSON，`\n` 结尾**。

#### 请求 (cli → 工具)

```json
{"args": ["命令", "参数1", "参数2"]}
```

| 字段 | 类型 | 说明 |
|---|---|---|
| `args` | string[] | 完整 argv，第一个元素通常是子命令名 |

`args` 是 cli 端从 `argv[3..]` 收集的（跳过 `<ToolName> <WorkspaceId>` 两个固定参数）。
如果 cli 端检测到 stdin 有数据，会把 stdin 内容追加为最后一个 arg —— 这样可以传超长 SQL。

#### 响应 (工具 → cli)

```json
{"code": 0, "output": "执行结果", "error": ""}
```

| 字段 | 类型 | 说明 |
|---|---|---|
| `code` | int | 退出码。0 = 成功，非 0 = 失败 |
| `output` | string | 成功时的输出，cli 会写到 stdout |
| `error` | string | 失败时的错误信息，cli 会写到 stderr |

cli 端拿到响应后：
- 把 `output` 原样写 stdout（不加换行）
- 把 `error` 原样写 stderr
- 用 `code` 作为进程退出码

### 通信流程（一次调用）

```
client                              server
  │                                   │
  ├─── connect to pipe ───────────────▶
  │                                   │
  ├─── write '{"args":[...]}\n' ──────▶
  ├─── flush                          │
  │                                   ├─ parse JSON
  │                                   ├─ call handler
  │                                   ├─ build response
  ◀─── read until '\n' ───────────────┤
  ├─ parse JSON                       │
  ├─ write output → stdout            │
  ├─ exit(code)                       │
  ├─── disconnect ────────────────────▶
                                      └─ close, loop back to WaitForConnection
```

## 长连接? (高级)

默认是短连接：cli 一次调用 = 一次连接。这样最简单，并发隔离也好。

如果你的 handler 要保持会话状态（比如 transaction、cursor），可以让客户端
**保持连接，循环 send/recv**。CommonCliServer 已经支持：worker 内层是 `while (pipe.IsConnected)` 循环。
但 `common-cli.exe` 本身是一次性的，要做长连接需要自己写客户端。

## 错误处理

### 客户端 (cli) 视角

| 情况 | cli 行为 |
|---|---|
| Pipe 不存在 (`CreateFileA` 失败) | stderr 输出 `Failed to connect to tool ...`, exit 1 |
| Pipe 存在但全忙 (`ERROR_PIPE_BUSY`) | 同上（cli 不重试） |
| 连上后写入失败 | stderr 输出 `Failed to send request ...`, exit 1 |
| 连上后读取失败 | stderr 输出 `Pipe connection lost ...`, exit 1 |
| 响应不是合法 JSON | stderr 输出 `Invalid response from tool: ...`, exit 1 |

### 服务端视角

| 情况 | 推荐处理 |
|---|---|
| handler 抛异常 | 捕获，回 `{"code":1,"error":"<message>"}` |
| 请求 JSON 解析失败 | 回 `{"code":1,"error":"invalid request"}` |
| 客户端突然断开 | 静默忽略，重新 `WaitForConnection` |

## 跨平台移植笔记

### Linux / macOS

把 Named Pipe 换成 **Unix Domain Socket**：
- 路径: `~/.local/share/common-cli/{ToolName}/{WorkspaceId}.sock` 或类似
- 文件锁: `flock(LOCK_EX)` 或 `fcntl(F_SETLKW)`
- registry.json 路径: `${XDG_DATA_HOME:-~/.local/share}/common-cli/{ToolName}/registry.json`

注意 cli 端也要相应改，不然两边对不上。

### 端口? (反正都是 IPC)

也可以用 TCP loopback (`127.0.0.1:<random_port>`)，registry 里存端口号而不是 workspace id。
优点：跨平台、跨语言都简单。缺点：要管理端口冲突、防火墙弹窗。

如果你不在乎跨平台，**坚持用 Named Pipe** 是最省事的方案。

## 最小服务端实现 (Python 伪代码)

仅做协议参考，不能直接跑：

```python
import json, win32pipe, win32file

PIPE_NAME = r"\\.\pipe\common-cli-MyTool-project1"

def handler(args):
    if args[0] == "ping":
        return {"code": 0, "output": "pong", "error": ""}
    return {"code": 1, "output": "", "error": f"unknown: {args[0]}"}

while True:
    pipe = win32pipe.CreateNamedPipe(
        PIPE_NAME,
        win32pipe.PIPE_ACCESS_DUPLEX,
        win32pipe.PIPE_TYPE_BYTE | win32pipe.PIPE_WAIT,
        4,                    # max instances
        65536, 65536, 0, None
    )
    win32pipe.ConnectNamedPipe(pipe, None)
    try:
        # 读一行 JSON
        line = b""
        while True:
            _, data = win32file.ReadFile(pipe, 1)
            if data == b"\n": break
            line += data
        req = json.loads(line)
        # 调 handler
        resp = handler(req["args"])
        # 写一行 JSON
        win32file.WriteFile(pipe, (json.dumps(resp) + "\n").encode())
    finally:
        win32file.CloseHandle(pipe)
```
