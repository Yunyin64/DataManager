// 这个文件是 SchemaMaster 真实生产环境的 CliManager.cs（原文件路径：
// K:/TechicalDesigner/Code/MoonLight/SchemaMaster/MCP/CliManager.cs）。
//
// 保留原样作为"中等复杂度真实接入案例"，展示了：
//   - 多个子命令分发 (query / exec / search / diff / blame)
//   - 用 CliArgParser 解析 GNU 风格选项 (-t/-p/-c/-n/-k 等)
//   - 用别名映射把短选项映射到长选项
//   - 区分 flag (布尔) 和 option (带值)
//   - handler 在 IO 线程，用 Application.Current.Dispatcher.Invoke 切到 UI 线程
//   - 业务函数包裹 ProgressBarWindow.Call 展示进度
//   - 插件机制：除内置命令外，还能从插件系统动态注册 CLI 命令
//   - 异常兜底 + 远程日志
//
// 不能直接编译，因为依赖了 SchemaMaster 内部的:
//   - SchemaMasterManager
//   - MCPDatabaseToolImpl (业务实现)
//   - MoonLightCommon.Log
//   - CommonUI 的 ProgressBarWindow 和 ExcelWindow
//   - SchemaMaster.Core 命名空间下的 CliResponse / CommonCliServer / WorkspaceRegistry
//
// 适合作为接入模式参考；不要直接抄进新项目。

using CommonUI;
using MoonLightCommon.Log;
using MoonLightCommon.Utils;
using SchemaMaster.Core;
using SchemaMaster.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace SchemaMaster.MCP
{
    internal static class CliManager
    {
        internal static readonly HashSet<string> BuiltinCommands = new HashSet<string>
        {
            "query", "exec", "search", "diff", "blame"
        };

        internal static readonly HashSet<string> ReservedCommands = new HashSet<string>
        {

        };

        private static CommonCliServer Server;
        private static WorkspaceRegistry Registry;
        private static string WorkspaceId;

        public static void Start()
        {
            string workspacePath = SchemaMasterManager.Workspace.FilePath;
            WorkspaceId = EncodeWorkspacePath(workspacePath);

            Server = new CommonCliServer("SchemaMaster", WorkspaceId, Handler);
            Server.Start();

            Registry = new WorkspaceRegistry("SchemaMaster");
            Registry.Register(WorkspaceId, SchemaMasterManager.GetWorkspaceName());
        }

        public static void Stop()
        {
            Server.Stop();

            if (Registry != null)
            {
                Registry.Unregister(WorkspaceId);
                Registry.Dispose();
                Registry = null;
            }
        }

        // 把工作区绝对路径转义成合法的 pipe 名 / registry id
        private static string EncodeWorkspacePath(string path)
        {
            string fullPath = Path.GetFullPath(path);
            return fullPath.Replace(":", "_").Replace("\\", "_").Replace("/", "_");
        }

        // 把 handler 切到 UI 线程，用 ProgressBarWindow 套一层进度提示
        private static T Call<T>(Func<T> func)
        {
            T result = default;
            Application.Current.Dispatcher.Invoke(() =>
            {
                bool anyWindowActive = ExcelWindow.Instances.Values.Any(w => w.IsActive);
                if (anyWindowActive)
                {
                    result = ProgressBarWindow.Call("AI正在执行操作...", () =>
                    {
                        try
                        {
                            return func();
                        }
                        catch (Exception ex)
                        {
                            LocalLog.Error(ExceptionInfo.GetExceptionInfo(ex));
                            throw;
                        }
                    });
                }
                else
                {
                    try
                    {
                        result = func();
                    }
                    catch (Exception ex)
                    {
                        LocalLog.Error(ExceptionInfo.GetExceptionInfo(ex));
                        throw;
                    }
                }
            });
            return result;
        }

        private static string GetAvailableCommands()
        {
            var commands = new List<string>(BuiltinCommands);
            commands.AddRange(SchemaMasterManager.Plugin.GetRegisteredCliCommandNames());
            return string.Join(", ", commands);
        }

        // 主分发函数 —— CommonCliServer 把每个请求都转给它
        private static CliResponse Handler(string[] args)
        {
            RemoteLog.DoLog("CLI", args);
            LocalLog.Info($"[CliManager] 收到请求: {(args == null ? "null" : string.Join(" ", args))}");

            if (args == null || args.Length == 0)
                return CliResponse.Fail($"用法: <command> [options] [arguments]\n可用命令: {GetAvailableCommands()}");

            var command = args[0];
            try
            {
                CliResponse response;
                switch (command)
                {
                    case "query": response = HandleQuery(args); break;
                    case "exec": response = HandleExec(args); break;
                    case "search": response = HandleSearch(args); break;
                    case "diff": response = HandleDiff(args); break;
                    case "blame": response = HandleBlame(args); break;
                    default:
                        if (SchemaMasterManager.Plugin.TryGetCliCommand(command, out var handler))
                        {
                            var pluginArgs = args.Skip(1).ToArray();
                            response = Call(() => handler(pluginArgs));
                        }
                        else
                        {
                            response = CliResponse.Fail($"未知命令: {command}\n可用命令: {GetAvailableCommands()}");
                        }
                        break;
                }

                if (response.Code == 0)
                    LocalLog.Info($"[CliManager] 响应成功: {response.Output}");
                else
                    LocalLog.Warn($"[CliManager] 响应失败(code={response.Code}): {response.Error}");

                return response;
            }
            catch (Exception ex)
            {
                LocalLog.Error($"[CliManager] 异常: {ex.Message}");
                return CliResponse.Fail(ex.Message);
            }
        }

        // ===== 各子命令的处理 =====

        // query <sql>
        private static CliResponse HandleQuery(string[] args)
        {
            var parsed = CliArgParser.Parse(args);
            if (parsed.Args.Count == 0)
                return CliResponse.Fail("用法: query <sql>");

            var sql = parsed.Args[0];
            var result = Call(() => MCPDatabaseToolImpl.Query(sql));
            return CliResponse.Success(result);
        }

        // exec <sql>
        private static CliResponse HandleExec(string[] args)
        {
            var parsed = CliArgParser.Parse(args);
            if (parsed.Args.Count == 0)
                return CliResponse.Fail("用法: exec <sql>");

            var sql = parsed.Args[0];
            var result = Call(() => MCPDatabaseToolImpl.Execute(sql));
            return CliResponse.Success(result);
        }

        // search 用到了完整的选项解析能力 —— 别名 + 带值选项 + 布尔标志混用
        private static readonly Dictionary<string, string> SearchAliases = new Dictionary<string, string>
        {
            { "t", "table" },
            { "p", "partition" },
            { "c", "column" },
            { "s", "case-sensitive" },
            { "w", "word" },
            { "x", "exact" },
            { "E", "regex" },
            { "n", "limit" },
            { "k", "offset" },
        };

        private static readonly HashSet<string> SearchValueOptions = new HashSet<string>
        {
            "table", "partition", "column", "limit", "offset"
        };

        // search [options] <pattern>
        private static CliResponse HandleSearch(string[] args)
        {
            var parsed = CliArgParser.Parse(args, SearchAliases, SearchValueOptions);
            if (parsed.Args.Count == 0)
                return CliResponse.Fail("用法: search [options] <pattern>\n选项: -t/--table, -p/--partition, -c/--column, -s/--case-sensitive, -w/--word, -x/--exact, -E/--regex, -n/--limit, -k/--offset");

            var pattern = parsed.Args[0];
            var tableNames = parsed.GetCsvOption("table");
            var partitionNames = parsed.GetCsvOption("partition");
            var colNames = parsed.GetCsvOption("column");
            var caseSensitive = parsed.HasFlag("case-sensitive");
            var matchWholeWord = parsed.HasFlag("word");
            var matchWholeCell = parsed.HasFlag("exact");
            var isRegex = parsed.HasFlag("regex");
            var limit = parsed.GetIntOption("limit");
            var offset = parsed.GetIntOption("offset");

            var result = Call(() => MCPDatabaseToolImpl.Search(
                pattern, tableNames, partitionNames, colNames,
                caseSensitive, matchWholeWord, matchWholeCell, isRegex,
                limit, offset));
            return CliResponse.Success(result);
        }

        private static readonly Dictionary<string, string> DiffAliases = new Dictionary<string, string>
        {
            { "r", "revision" },
        };

        private static readonly HashSet<string> DiffValueOptions = new HashSet<string>
        {
            "revision"
        };

        // diff [-r <revision>]
        private static CliResponse HandleDiff(string[] args)
        {
            var parsed = CliArgParser.Parse(args, DiffAliases, DiffValueOptions);
            var revision = parsed.GetOption("revision");

            if (revision != null)
            {
                var result = Call(() => MCPDatabaseToolImpl.RevisionDiff(revision));
                return CliResponse.Success(result);
            }
            else
            {
                var result = Call(() => MCPDatabaseToolImpl.LocalDiff());
                return CliResponse.Success(result);
            }
        }

        private static readonly Dictionary<string, string> BlameAliases = new Dictionary<string, string>
        {
            { "p", "partition" },
            { "c", "column" },
        };

        private static readonly HashSet<string> BlameValueOptions = new HashSet<string>
        {
            "partition", "column"
        };

        // blame [options] <table> <condition>
        private static CliResponse HandleBlame(string[] args)
        {
            var parsed = CliArgParser.Parse(args, BlameAliases, BlameValueOptions);
            if (parsed.Args.Count < 2)
                return CliResponse.Fail("用法: blame [-p <partition>] [-c <column>] <table> <condition>");

            var tableName = parsed.Args[0];
            var condition = parsed.Args[1];
            var partitionName = parsed.GetOption("partition");
            var columnName = parsed.GetOption("column");

            var result = Call(() => MCPDatabaseToolImpl.Blame(tableName, condition, partitionName, columnName));
            return CliResponse.Success(result);
        }
    }
}
