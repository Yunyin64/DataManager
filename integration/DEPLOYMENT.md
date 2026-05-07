# 部署到 AI 工作目录

集成 SDK 完成后，还需要让 AI Agent 知道：**有这个工具、怎么用它、能直接用不用确认**。
这一步叫"部署"。本文档基于 SchemaMaster 的真实部署逻辑（`AIAgentConfig.cs`）整理。

## 部署清单

要做 4 件事：

| # | 事项 | 文件位置 | 必须 |
|---|---|---|---|
| 1 | 把 `common-cli.exe` 放进用户 PATH | `%LOCALAPPDATA%\Microsoft\WindowsApps\common-cli.exe` | 是 |
| 2 | 部署 Skill 文档（教 AI 怎么用） | `<workDir>/.claude/skills/<name>/SKILL.md` | 是 |
| 3 | 加权限 allowlist（免确认） | `<workDir>/.claude/settings.local.json` | 推荐 |
| 4 | 同样的事再为别的 Agent 做一遍 | `<workDir>/.opencode/command/<name>.md` 等 | 可选 |

`<workDir>` = 用户运行 Claude Code 的目录（通常是项目根）。

## 1. 把 cli 放进 PATH

### 推荐方案: WindowsApps 目录

`%LOCALAPPDATA%\Microsoft\WindowsApps` 是 Windows 给 UWP/Store 应用预留的"伪目录"，
**默认就在每个用户的 PATH 里**。把 exe 拷过去就直接全局可用，不用改 PATH。

```csharp
string windowsAppsDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Microsoft", "WindowsApps");

Directory.CreateDirectory(windowsAppsDir);
File.Copy(srcCommonCliPath, Path.Combine(windowsAppsDir, "common-cli.exe"), overwrite: true);
```

### 兜底: 改用户 PATH 注册表

如果某些环境 WindowsApps 不在 PATH（罕见），手动添加：

```csharp
const string keyPath = @"HKEY_CURRENT_USER\Environment";
string userPath = Microsoft.Win32.Registry.GetValue(keyPath, "Path", "") as string ?? "";

if (!userPath.Split(';').Any(p => string.Equals(p.TrimEnd('\\'), targetDir, StringComparison.OrdinalIgnoreCase)))
{
    string newPath = string.IsNullOrEmpty(userPath) ? targetDir : userPath + ";" + targetDir;
    Environment.SetEnvironmentVariable("Path", newPath, EnvironmentVariableTarget.User);
    // 同时更新当前进程 PATH，省得用户重启 shell
    var current = Environment.GetEnvironmentVariable("PATH") ?? "";
    Environment.SetEnvironmentVariable("PATH", current + ";" + targetDir);
}
```

完整代码见 [`examples/DeployHelper.cs`](./examples/DeployHelper.cs) 的 `EnsureInUserPath` 方法。

## 2. 部署 Skill 文档

### Claude Code 路径

```
<workDir>/.claude/skills/<skill-name>/SKILL.md
```

`<skill-name>` 用 kebab-case，比如 `mytool-helper`。

Claude Code 启动时会扫描 `.claude/skills/`，读取每个目录下的 `SKILL.md`，
把 frontmatter 的 `description` 加入 system prompt。当用户的话匹配上 description，
Agent 会**主动加载** SKILL.md 全文，按里面的指引调用 cli。

### SKILL.md 模板

见 [`templates/SKILL.md.template`](./templates/SKILL.md.template)。最小骨架：

```markdown
---
name: mytool-helper
description: <一句话说什么时候应该用这个工具>
---

# <工具名> 助手

<介绍>

## CLI 工具

`common-cli.exe` 已加入系统 PATH，可直接调用。

### 命令格式

```bash
common-cli.exe MyTool <WorkspaceId> <command> [args...]
```

`<WorkspaceId>` 用 `auto` 自动连接（多工作区时返回列表，让你问用户）。

### 命令清单

| 命令 | 用途 | 示例 |
|---|---|---|
| `ping` | 健康检查 | `ping` |
| ... | ... | ... |
```

### 部署代码

```csharp
string skillDir = Path.Combine(workDir, ".claude", "skills", skillName);
Directory.CreateDirectory(skillDir);

// 从内嵌资源读取（推荐）或直接从模板字符串生成
using var stream = Assembly.GetExecutingAssembly()
    .GetManifestResourceStream("MyTool.Resources.SKILL.md");
using var reader = new StreamReader(stream);
File.WriteAllText(
    Path.Combine(skillDir, "SKILL.md"),
    reader.ReadToEnd(),
    Encoding.UTF8);
```

### 卸载

如果用户取消勾选这个 Skill，要删掉目录：

```csharp
if (Directory.Exists(skillDir))
    Directory.Delete(skillDir, recursive: true);
```

## 3. 权限 allowlist (免确认)

Claude Code 默认运行 Bash 命令前会弹确认框。AI 每次查询都让用户点"允许"显然不行。
往 `<workDir>/.claude/settings.local.json` 加一条 allow 规则：

```json
{
  "permissions": {
    "allow": [
      "Bash(*common-cli.exe *)"
    ]
  }
}
```

`*common-cli.exe *` 是通配符匹配：任何带 `common-cli.exe` 的 Bash 命令都自动放行。

### 部署代码（merge 到现有配置）

```csharp
string settingsPath = Path.Combine(workDir, ".claude", "settings.local.json");
JObject settings = File.Exists(settingsPath)
    ? JObject.Parse(File.ReadAllText(settingsPath))
    : new JObject();

if (settings["permissions"] is not JObject perms)
    settings["permissions"] = perms = new JObject();
if (perms["allow"] is not JArray allow)
    perms["allow"] = allow = new JArray();

const string rule = "Bash(*common-cli.exe *)";
if (!allow.Any(t => (string)t == rule))
    allow.Add(rule);

File.WriteAllText(settingsPath, settings.ToString(Formatting.Indented));
```

完整 helper 见 [`examples/DeployHelper.cs`](./examples/DeployHelper.cs)。

## 4. 适配其他 Agent (可选)

不同 Agent 的 Skill 路径不一样：

| Agent | Skill 路径 |
|---|---|
| Claude Code | `<workDir>/.claude/skills/<name>/SKILL.md` |
| OpenCode | `<workDir>/.opencode/command/<name>.md` |
| Cursor | (用 Rules 或 mcp-shim) |
| 其他 | 各家文档 |

SchemaMaster 在 `AIAgentConfig.cs` 同时部署了 Claude Code 和 OpenCode 两份。
其他 Agent 用类似套路。

## 用户操作流程

理想体验：用户在你 GUI 里点一个按钮 → 全自动部署完。

```
┌──────────────────────────────────┐
│  我的工具 - AI Agent 配置         │
├──────────────────────────────────┤
│  工作目录: [D:/Projects/MyGame] │
│                                  │
│  [安装 AI 助手]    ← 一键按钮     │
└──────────────────────────────────┘
         │
         ▼
按钮点击逻辑:
  1. DeployCommonCli()        # 拷 exe + 改 PATH
  2. DeploySkill(workDir)     # 写 SKILL.md
  3. WriteAllowlist(workDir)  # 改 settings.local.json
  4. 弹消息: "安装完成! 在 D:/Projects/MyGame 目录下打开 Claude Code 即可使用"
```

完整按钮逻辑见 [`examples/DeployHelper.cs`](./examples/DeployHelper.cs) 的 `InstallAll`。

## 验证安装

让用户在 `<workDir>` 下打开终端，敲：

```bash
common-cli.exe MyTool list
```

预期输出：

```json
{"workspaces":[{"id":"project1","description":"..."}]}
```

如果报 `Failed to connect to tool`，说明工具进程没启动 —— 让用户先开 GUI。
如果报 `'common-cli.exe' is not recognized`，说明 PATH 没生效 —— 让用户重启终端，
或用绝对路径测一次确认 exe 本身没问题。

## 注意事项

1. **不要每次启动 GUI 都重新部署**。让用户主动点"安装"按钮，避免覆盖他们手改的 Skill。
2. **更新 cli 时小心**。`File.Copy(..., overwrite: true)` 在 cli 正在被某个 Agent 使用时会失败。
   做法：`Move` 先把旧的改名再 `Copy` 新的，或者捕获异常提示用户关闭终端。
3. **`settings.local.json` 是用户私有的**，不要 commit 进 git。提示用户加 `.gitignore`。
4. **跨工作区共享**: `common-cli.exe` 装一次全局可用，但 Skill 是按工作目录的。
   用户每开一个新项目，都要点一次"安装"。
