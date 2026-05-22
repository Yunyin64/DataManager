using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using DataManager.Cli.Commands;
using DataManager.Data;

namespace DataManager.Cli
{
    /// <summary>
    /// CLI 服务封装。管理 CommonCliServer + WorkspaceRegistry 的完整生命周期。
    /// 命令路由分发到独立的 ICliCommand 实现。
    /// </summary>
    public class CliService : IDisposable
    {
        private const string ToolName = "DataManager";

        private readonly Dispatcher _dispatcher;
        private CommonCliServer? _server;
        private WorkspaceRegistry? _registry;
        private string? _currentWorkspaceId;
        private bool _disposed;

        // 由外部（MainViewModel）设置的 workspace 引用访问器
        private Func<DataWorkspace?>? _workspaceAccessor;

        // 数据修改后的 UI 刷新回调
        private Action? _onDataModified;

        // 命令注册表
        private Dictionary<string, ICliCommand>? _commands;

        /// <summary>CLI 参数解析用的选项别名</summary>
        private static readonly Dictionary<string, string> Aliases = new()
        {
            { "p", "path" },
            { "v", "value" },
            { "f", "file" },
        };

        /// <summary>需要带值的选项</summary>
        private static readonly HashSet<string> ValueOptions = new()
        {
            "ID", "DisplayName", "path", "value", "file"
        };

        public CliService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        /// <summary>
        /// 设置工作区访问器和数据修改回调。
        /// </summary>
        /// <param name="workspaceAccessor">返回当前 DataWorkspace（可能为 null）</param>
        /// <param name="onDataModified">数据被修改后的 UI 刷新回调</param>
        public void Configure(Func<DataWorkspace?> workspaceAccessor, Action? onDataModified = null)
        {
            _workspaceAccessor = workspaceAccessor;
            _onDataModified = onDataModified;
            RegisterCommands();
        }

        /// <summary>
        /// 注册并启动指定工作区的 CLI 服务。
        /// </summary>
        public void StartWorkspace(string workspacePath)
        {
            // 先停掉旧的
            StopWorkspace();

            _currentWorkspaceId = GenerateWorkspaceId(workspacePath);

            // 注册到 registry
            _registry = new WorkspaceRegistry(ToolName);
            _registry.Register(_currentWorkspaceId, workspacePath);

            // 启动管道服务
            _server = new CommonCliServer(ToolName, _currentWorkspaceId, HandleCommand);
            _server.OnException += ex =>
            {
                Debug.WriteLine($"[CliService] Pipe error: {ex.Message}");
            };
            _server.Start();

            Debug.WriteLine($"[CliService] Started. Workspace={_currentWorkspaceId}, Path={workspacePath}");
        }

        /// <summary>
        /// 停止当前工作区的 CLI 服务并注销。
        /// </summary>
        public void StopWorkspace()
        {
            if (_server != null)
            {
                _server.Stop();
                _server = null;
            }

            if (_registry != null && _currentWorkspaceId != null)
            {
                try
                {
                    _registry.Unregister(_currentWorkspaceId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CliService] Unregister error: {ex.Message}");
                }
                _registry.Dispose();
                _registry = null;
            }

            _currentWorkspaceId = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopWorkspace();
        }

        // ── 命令注册 ──────────────────────────────────────

        /// <summary>
        /// 注册所有 CLI 命令到路由表。
        /// </summary>
        private void RegisterCommands()
        {
            var wsAccessor = _workspaceAccessor!;
            var modified = _onDataModified;

            var commandList = new ICliCommand[]
            {
                new StatusCommand(_dispatcher, wsAccessor, () => _currentWorkspaceId),
                new ListFilesCommand(_dispatcher, wsAccessor),
                new GetCommand(_dispatcher, wsAccessor),
                new QueryCommand(_dispatcher, wsAccessor),
                new SetCommand(_dispatcher, wsAccessor, modified),
                new SaveCommand(_dispatcher, wsAccessor, modified),
                new AddCommand(_dispatcher, wsAccessor, modified),
                new DeleteCommand(_dispatcher, wsAccessor, modified),
                new UpdateCommand(_dispatcher, wsAccessor, modified),
                new BatchAddCommand(_dispatcher, wsAccessor, modified),
                new BatchUpdateCommand(_dispatcher, wsAccessor, modified),
            };

            _commands = new Dictionary<string, ICliCommand>(StringComparer.OrdinalIgnoreCase);
            foreach (var cmd in commandList)
            {
                _commands[cmd.Name] = cmd;
            }
        }

        // ── 命令路由 ──────────────────────────────────────

        private CliResponse HandleCommand(string[] args)
        {
            if (args == null || args.Length == 0)
                return CliResponse.Fail("no command provided");

            if (_commands == null)
                return CliResponse.Fail("CLI service not configured");

            var parsed = CliArgParser.Parse(args, Aliases, ValueOptions);
            var command = parsed.Command?.ToLowerInvariant();

            try
            {
                if (command != null && _commands.TryGetValue(command, out var handler))
                    return handler.Execute(parsed);

                return CliResponse.Fail($"unknown command: {command}");
            }
            catch (Exception ex)
            {
                return CliResponse.Fail(ex.Message);
            }
        }

        // ── 辅助方法 ──────────────────────────────────────

        /// <summary>
        /// 基于工作区路径生成 workspaceId。
        /// 将路径转换为安全的标识符（小写，替换特殊字符）。
        /// </summary>
        private static string GenerateWorkspaceId(string path)
        {
            var normalized = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToLowerInvariant();

            // 替换不安全字符为下划线
            var safe = Regex.Replace(normalized, @"[^a-z0-9]", "_");

            // 去重复下划线
            safe = Regex.Replace(safe, @"_+", "_").Trim('_');

            // 截断以避免管道名过长（管道名有 256 字符限制）
            if (safe.Length > 120)
                safe = safe.Substring(safe.Length - 120);

            return safe;
        }
    }
}
