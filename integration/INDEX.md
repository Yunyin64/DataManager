# 集成包文件清单

便于快速找到要的东西。

## 必读文档

| 文件 | 作用 |
|---|---|
| `README.md` | 总入口、5 分钟集成、设计要点速查 |
| `ARCHITECTURE.md` | 全景架构图、5 层叠加机制、与 MCP 对比 |
| `PROTOCOL.md` | Named Pipe + JSON 协议规范，跨语言重写参考 |
| `DEPLOYMENT.md` | 把 cli + Skill 部署到 AI 工作目录的全流程 |
| `INDEX.md` | 本文档 |

## 服务端源文件 (`src/`)

直接拷进你的项目即可。命名空间统一为 `CommonCli`。

| 文件 | 必须 | 依赖 |
|---|---|---|
| `CommonCliServer.cs` | 是 | `System.IO.Pipes.AccessControl` (NuGet) |
| `WorkspaceRegistry.cs` | 是 | `Newtonsoft.Json` |
| `CliArgParser.cs` | 可选 | 无 |

## 模板 (`templates/`)

复制后按你的工具改。

| 文件 | 用途 |
|---|---|
| `SKILL.md.template` | Claude Code Skill 文档骨架，告诉 AI 怎么用你的工具 |
| `settings.local.json.fragment` | 权限放行规则片段，merge 到 `.claude/settings.local.json` |

## 例子 (`examples/`)

| 文件 | 复杂度 | 用途 |
|---|---|---|
| `MinimalServer.cs` | 简单 | 最小可运行例子，3 个命令 (ping/echo/add)，可直接编译 |
| `DeployHelper.cs` | 中等 | 一键部署 helper：cli 进 PATH + Skill 部署 + 权限放行 |
| `CliManager.SchemaMaster.cs` | 真实 | SchemaMaster 生产环境的接入代码（不能直接编译，纯参考） |

## 推荐阅读顺序

**第一次集成**:
1. `README.md` 看 5 分钟集成那段
2. 把 `src/` 三个文件拷进项目
3. 抄 `examples/MinimalServer.cs` 跑通 ping
4. 看 `DEPLOYMENT.md` 把 SDK 部署给 AI Agent
5. 抄 `templates/SKILL.md.template` 写自己的 Skill 文档

**深入理解**:
1. `ARCHITECTURE.md` —— 搞懂为什么这套能 work
2. `PROTOCOL.md` —— 想用其他语言重写时看
3. `examples/CliManager.SchemaMaster.cs` —— 看真实项目怎么用

**做"安装"按钮**:
1. 抄 `examples/DeployHelper.cs`，改命名空间和 Skill 加载逻辑
2. 在你的 GUI 加一个 "安装 AI 助手" 按钮，调 `DeployHelper.InstallAll(workDir, "MyTool")`

## 不在这个包里的东西

| 不包含 | 在哪 |
|---|---|
| `common-cli.exe` 二进制 | 用户已经有了；如需重新构建，源码在 `K:/TechicalDesigner/Code/common-cli/` |
| `common-cli.exe` C++ 源码 | 同上 |
| SchemaMaster 业务代码 (MCPDatabaseToolImpl 等) | 跟 common-cli 无关，是 SchemaMaster 自己的业务 |
| Linux/macOS 适配 | 协议规范在 `PROTOCOL.md` 末尾给了思路，要自己实现 |
