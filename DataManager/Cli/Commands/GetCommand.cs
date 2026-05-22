using System;
using System.Collections.Generic;
using System.Windows.Threading;
using DataManager.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DataManager.Cli.Commands
{
    /// <summary>
    /// get 命令。获取文件的 JSON 内容。支持多文件：get FaBao,FormBase,GongFaBase
    /// </summary>
    public class GetCommand : CliCommandBase
    {
        public GetCommand(
            Dispatcher dispatcher,
            Func<DataWorkspace?> workspaceAccessor)
            : base(dispatcher, workspaceAccessor)
        {
        }

        public override string Name => "get";

        public override CliResponse Execute(CliParseResult parsed)
        {
            if (parsed.Args.Count == 0)
                return CliResponse.Fail("usage: get <file> or get <file1>,<file2>,...");

            // 支持逗号分隔多文件，也兼容多个 args
            var allNames = new List<string>();
            foreach (var arg in parsed.Args)
            {
                foreach (var part in arg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    allNames.Add(part);
            }
            var names = allNames.ToArray();

            // 单文件：保持原有行为（直接返回 JSON 内容）
            if (names.Length == 1)
            {
                return InvokeOnUI(() =>
                {
                    var ws = GetWorkspaceOrThrow();
                    var file = FindFile(ws, names[0]);
                    if (file == null)
                        return CliResponse.Fail($"file not found: {names[0]}");

                    var content = file.RootToken?.ToString(Formatting.Indented) ?? "null";
                    return CliResponse.Success(content);
                });
            }

            // 多文件：返回 { "fileName": content, ... } 结构
            return InvokeOnUI(() =>
            {
                var ws = GetWorkspaceOrThrow();
                var result = new JObject();
                var notFound = new List<string>();

                foreach (var name in names)
                {
                    var file = FindFile(ws, name);
                    if (file == null)
                    {
                        notFound.Add(name);
                        continue;
                    }
                    result[name] = file.RootToken ?? JValue.CreateNull();
                }

                if (notFound.Count > 0 && result.Count == 0)
                    return CliResponse.Fail($"files not found: {string.Join(", ", notFound)}");

                var output = result.ToString(Formatting.Indented);
                if (notFound.Count > 0)
                    output = $"// warning: not found: {string.Join(", ", notFound)}\n{output}";

                return CliResponse.Success(output);
            });
        }
    }
}
