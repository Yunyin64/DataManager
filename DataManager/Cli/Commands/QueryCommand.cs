using System;
using System.Linq;
using System.Windows.Threading;
using DataManager.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DataManager.Cli.Commands
{
    /// <summary>
    /// query 命令。按 --id 或 --path (JSONPath) 查询文件内容。
    /// </summary>
    public class QueryCommand : CliCommandBase
    {
        public QueryCommand(
            Dispatcher dispatcher,
            Func<DataWorkspace?> workspaceAccessor)
            : base(dispatcher, workspaceAccessor)
        {
        }

        public override string Name => "query";

        public override CliResponse Execute(CliParseResult parsed)
        {
            if (parsed.Args.Count == 0)
                return CliResponse.Fail("usage: query <file> [--ID <ID>] [--path <jsonpath>]");

            var fileName = parsed.Args[0];
            var id = parsed.GetOption("ID");
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
                                string.Equals(obj["ID"]?.ToString(), id, StringComparison.OrdinalIgnoreCase));

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
    }
}
