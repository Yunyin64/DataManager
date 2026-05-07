using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using DataManager.Core.Base.Interface;
using DataManager.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DataManager.Cli
{
    /// <summary>
    /// CLI 服务封装。管理 CommonCliServer + WorkspaceRegistry 的完整生命周期。
    /// 接收 common-cli 的命令请求，路由到对应 handler，读写 DataWorkspace 数据。
    ///
    /// 所有 handler 通过 Dispatcher.Invoke 切到 UI 线程执行，保证线程安全。
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

        // set 命令修改后的 UI 刷新回调
        private Action? _onDataModified;

        /// <summary>CLI 参数解析用的选项别名</summary>
        private static readonly Dictionary<string, string> Aliases = new()
        {
            { "i", "id" },
            { "p", "path" },
            { "v", "value" },
            { "f", "file" },
        };

        /// <summary>需要带值的选项</summary>
        private static readonly HashSet<string> ValueOptions = new()
        {
            "id", "path", "value", "file"
        };

        public CliService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        /// <summary>
        /// 设置工作区访问器和数据修改回调。
        /// </summary>
        /// <param name="workspaceAccessor">返回当前 DataWorkspace（可能为 null）</param>
        /// <param name="onDataModified">数据被 set 命令修改后的 UI 刷新回调</param>
        public void Configure(Func<DataWorkspace?> workspaceAccessor, Action? onDataModified = null)
        {
            _workspaceAccessor = workspaceAccessor;
            _onDataModified = onDataModified;
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

        // ── 命令路由 ──────────────────────────────────────

        private CliResponse HandleCommand(string[] args)
        {
            if (args == null || args.Length == 0)
                return CliResponse.Fail("no command provided");

            var parsed = CliArgParser.Parse(args, Aliases, ValueOptions);
            var command = parsed.Command?.ToLowerInvariant();

            try
            {
                return command switch
                {
                    "status"     => HandleStatus(),
                    "list-files" => HandleListFiles(),
                    "get"        => HandleGet(parsed),
                    "query"      => HandleQuery(parsed),
                    "set"        => HandleSet(parsed),
                    "save"       => HandleSave(parsed),
                    _            => CliResponse.Fail($"unknown command: {command}")
                };
            }
            catch (Exception ex)
            {
                return CliResponse.Fail(ex.Message);
            }
        }

        // ── status ──────────────────────────────────────

        private CliResponse HandleStatus()
        {
            return InvokeOnUI(() =>
            {
                var ws = GetWorkspaceOrThrow();

                var dirtyFiles = ws.Files.Where(f => f.IsDirty).Select(f => f.FileName).ToList();

                var result = new JObject
                {
                    ["workspaceId"] = _currentWorkspaceId,
                    ["rootPath"] = ws.RootPath,
                    ["fileCount"] = ws.Files.Count,
                    ["dirtyFiles"] = new JArray(dirtyFiles.ToArray<object>())
                };

                return CliResponse.Success(result.ToString(Formatting.None));
            });
        }

        // ── list-files ──────────────────────────────────

        private CliResponse HandleListFiles()
        {
            return InvokeOnUI(() =>
            {
                var ws = GetWorkspaceOrThrow();

                var filesArray = new JArray();
                foreach (var file in ws.Files)
                {
                    filesArray.Add(new JObject
                    {
                        ["name"] = Path.GetFileNameWithoutExtension(file.FileName),
                        ["path"] = file.FilePath,
                        ["dirty"] = file.IsDirty
                    });
                }

                var result = new JObject { ["files"] = filesArray };
                return CliResponse.Success(result.ToString(Formatting.None));
            });
        }

        // ── get ─────────────────────────────────────────

        private CliResponse HandleGet(CliParseResult parsed)
        {
            if (parsed.Args.Count == 0)
                return CliResponse.Fail("usage: get <file>");

            var fileName = parsed.Args[0];

            return InvokeOnUI(() =>
            {
                var ws = GetWorkspaceOrThrow();
                var file = FindFile(ws, fileName);
                if (file == null)
                    return CliResponse.Fail($"file not found: {fileName}");

                var content = file.RootToken?.ToString(Formatting.Indented) ?? "null";
                return CliResponse.Success(content);
            });
        }

        // ── query ───────────────────────────────────────

        private CliResponse HandleQuery(CliParseResult parsed)
        {
            if (parsed.Args.Count == 0)
                return CliResponse.Fail("usage: query <file> [--id <id>] [--path <jsonpath>]");

            var fileName = parsed.Args[0];
            var id = parsed.GetOption("id");
            var jsonPath = parsed.GetOption("path");

            return InvokeOnUI(() =>
            {
                var ws = GetWorkspaceOrThrow();
                var file = FindFile(ws, fileName);
                if (file == null)
                    return CliResponse.Fail($"file not found: {fileName}");

                if (file.RootToken == null)
                    return CliResponse.Fail($"file not loaded: {fileName}");

                // --id 查询：在根数组中查找 id 字段匹配的条目
                if (id != null)
                {
                    if (file.RootToken is JArray array)
                    {
                        var match = array
                            .OfType<JObject>()
                            .FirstOrDefault(obj =>
                                string.Equals(obj["id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase));

                        if (match != null)
                            return CliResponse.Success(match.ToString(Formatting.Indented));
                        else
                            return CliResponse.Fail($"id not found: {id}");
                    }
                    else
                    {
                        return CliResponse.Fail("root is not an array, --id is not applicable");
                    }
                }

                // --path 查询：JSONPath
                if (jsonPath != null)
                {
                    var results = file.Query(jsonPath).ToList();
                    if (results.Count == 0)
                        return CliResponse.Fail($"no match for path: {jsonPath}");
                    if (results.Count == 1)
                        return CliResponse.Success(results[0].ToString(Formatting.Indented));

                    var arr = new JArray(results);
                    return CliResponse.Success(arr.ToString(Formatting.Indented));
                }

                // 无过滤条件 → 返回全部
                return CliResponse.Success(file.RootToken.ToString(Formatting.Indented));
            });
        }

        // ── set ─────────────────────────────────────────

        private CliResponse HandleSet(CliParseResult parsed)
        {
            if (parsed.Args.Count == 0)
                return CliResponse.Fail("usage: set <file> --path <jsonpath> --value <json>");

            var fileName = parsed.Args[0];
            var jsonPath = parsed.GetOption("path");
            var valueStr = parsed.GetOption("value");

            if (jsonPath == null)
                return CliResponse.Fail("--path is required");
            if (valueStr == null)
                return CliResponse.Fail("--value is required");

            JToken newValue;
            try
            {
                newValue = JToken.Parse(valueStr);
            }
            catch
            {
                // 如果解析失败，当作字符串
                newValue = new JValue(valueStr);
            }

            return InvokeOnUI(() =>
            {
                var ws = GetWorkspaceOrThrow();
                var file = FindFile(ws, fileName);
                if (file == null)
                    return CliResponse.Fail($"file not found: {fileName}");

                file.Modify(jsonPath, newValue);

                // 触发 UI 刷新
                _onDataModified?.Invoke();

                return CliResponse.Success($"modified {fileName} at {jsonPath}");
            });
        }

        // ── save ────────────────────────────────────────

        private CliResponse HandleSave(CliParseResult parsed)
        {
            var fileName = parsed.Args.Count > 0 ? parsed.Args[0] : null;

            return InvokeOnUI(() =>
            {
                var ws = GetWorkspaceOrThrow();

                if (fileName != null)
                {
                    // 保存指定文件
                    var file = FindFile(ws, fileName);
                    if (file == null)
                        return CliResponse.Fail($"file not found: {fileName}");

                    file.Save();

                    // 触发 UI 刷新
                    _onDataModified?.Invoke();

                    return CliResponse.Success($"saved {file.FileName}");
                }
                else
                {
                    // 保存所有脏文件
                    var dirtyFiles = ws.Files.Where(f => f.IsDirty).ToList();
                    if (dirtyFiles.Count == 0)
                        return CliResponse.Success("no dirty files to save");

                    foreach (var f in dirtyFiles)
                        f.Save();

                    // 触发 UI 刷新
                    _onDataModified?.Invoke();

                    var names = dirtyFiles.Select(f => f.FileName);
                    return CliResponse.Success($"saved {dirtyFiles.Count} file(s): {string.Join(", ", names)}");
                }
            });
        }

        // ── 辅助方法 ──────────────────────────────────────

        /// <summary>
        /// 在 UI 线程上执行操作并返回结果。
        /// 保证所有 DataWorkspace 的读写都在 UI 线程上完成。
        /// </summary>
        private CliResponse InvokeOnUI(Func<CliResponse> action)
        {
            try
            {
                return _dispatcher.Invoke(action);
            }
            catch (Exception ex)
            {
                return CliResponse.Fail(ex.Message);
            }
        }

        private DataWorkspace GetWorkspaceOrThrow()
        {
            var ws = _workspaceAccessor?.Invoke();
            if (ws == null)
                throw new InvalidOperationException("no workspace loaded");
            return ws;
        }

        /// <summary>
        /// 按文件名查找文件。支持带或不带 .json 后缀。
        /// </summary>
        private static IJsonDataFile? FindFile(DataWorkspace ws, string name)
        {
            // 先精确匹配
            var file = ws.GetFile(name);
            if (file != null) return file;

            // 不带后缀 → 加上 .json 再试
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                file = ws.GetFile(name + ".json");
                if (file != null) return file;
            }

            // 带后缀 → 去掉后缀试试（以防 GetFile 匹配逻辑不同）
            if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                file = ws.GetFile(Path.GetFileNameWithoutExtension(name));
            }

            return file;
        }

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
