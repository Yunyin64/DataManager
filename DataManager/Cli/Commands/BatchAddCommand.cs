using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using DataManager.Data;
using Newtonsoft.Json.Linq;

namespace DataManager.Cli.Commands
{
    /// <summary>
    /// batch-add 命令。批量追加多条条目到根数组。
    /// 每条条目必须包含 id 字段，校验 id 不与已有条目重复。
    /// </summary>
    public class BatchAddCommand : CliCommandBase
    {
        public BatchAddCommand(
            Dispatcher dispatcher,
            Func<DataWorkspace?> workspaceAccessor,
            Action? onDataModified = null)
            : base(dispatcher, workspaceAccessor, onDataModified)
        {
        }

        public override string Name => "batch-add";

        public override CliResponse Execute(CliParseResult parsed)
        {
            if (parsed.Args.Count == 0)
                return CliResponse.Fail("usage: batch-add <file> --value <json-array>");

            var fileName = parsed.Args[0];
            var valueStr = parsed.GetOption("value");

            if (valueStr == null)
                return CliResponse.Fail("--value is required (must be a JSON array)");

            JArray newEntries;
            try
            {
                var token = JToken.Parse(valueStr);
                if (token is not JArray arr)
                    return CliResponse.Fail("--value must be a JSON array");
                newEntries = arr;
            }
            catch
            {
                return CliResponse.Fail("--value is not valid JSON");
            }

            if (newEntries.Count == 0)
                return CliResponse.Fail("--value array is empty");

            return InvokeOnUI(() =>
            {
                var ws = GetWorkspaceOrThrow();
                var file = FindFile(ws, fileName);
                if (file == null)
                    return CliResponse.Fail($"file not found: {fileName}");

                if (file.RootToken is not JArray array)
                    return CliResponse.Fail("root is not an array");

                // 收集已有 id
                var existingIds = new HashSet<string>(
                    array.OfType<JObject>()
                        .Select(obj => obj["ID"]?.ToString())
                        .Where(id => id != null)!,
                    StringComparer.OrdinalIgnoreCase);

                // 校验新条目 ID
                var duplicates = new List<string>();
                foreach (var entry in newEntries.OfType<JObject>())
                {
                    var id = entry["ID"]?.ToString();
                    if (id == null)
                    {
                        return CliResponse.Fail("each entry must have an 'ID' field");
                    }
                    if (existingIds.Contains(id))
                    {
                        duplicates.Add(id);
                    }
                    existingIds.Add(id);
                }

                if (duplicates.Count > 0)
                    return CliResponse.Fail($"duplicate id(s): {string.Join(", ", duplicates)}");

                // 追加所有条目
                foreach (var entry in newEntries)
                {
                    array.Add(entry);
                }

                // 标记 dirty
                if (file is JsonDataFile jdf)
                    jdf.IsDirty = true;

                _onDataModified?.Invoke();

                return CliResponse.Success($"added {newEntries.Count} entries to {fileName}");
            });
        }
    }
}
