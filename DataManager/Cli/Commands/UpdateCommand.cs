using System;
using System.Linq;
using System.Windows.Threading;
using DataManager.Data;
using Newtonsoft.Json.Linq;

namespace DataManager.Cli.Commands
{
    /// <summary>
    /// update 命令。按 id 定位条目，使用相对路径 upsert 属性。
    /// path 相对于匹配条目根，支持点分嵌套（如 "stats.atk"）。
    /// 属性不存在则创建，已存在则替换。
    /// </summary>
    public class UpdateCommand : CliCommandBase
    {
        public UpdateCommand(
            Dispatcher dispatcher,
            Func<DataWorkspace?> workspaceAccessor,
            Action? onDataModified = null)
            : base(dispatcher, workspaceAccessor, onDataModified)
        {
        }

        public override string Name => "update";

        public override CliResponse Execute(CliParseResult parsed)
        {
            if (parsed.Args.Count == 0)
                return CliResponse.Fail("usage: update <file> --ID <ID> --path <prop> --value <json>\n   or: update <file> --ID <ID> --value <json-object>  (merge multiple fields)");

            var fileName = parsed.Args[0];
            var id = parsed.GetOption("ID");
            var relPath = parsed.GetOption("path");
            var valueStr = parsed.GetOption("value");

            if (id == null)
                return CliResponse.Fail("--ID is required");
            if (valueStr == null)
                return CliResponse.Fail("--value is required");

            JToken newValue;
            try
            {
                newValue = JToken.Parse(valueStr);
            }
            catch
            {
                // 解析失败，当作字符串
                newValue = new JValue(valueStr);
            }

            // 无 --path 时，--value 必须是 JSON 对象，整体 merge 到条目
            if (relPath == null && newValue is not JObject)
                return CliResponse.Fail("without --path, --value must be a JSON object to merge");

            return InvokeOnUI(() =>
            {
                var ws = GetWorkspaceOrThrow();
                var file = FindFile(ws, fileName);
                if (file == null)
                    return CliResponse.Fail($"file not found: {fileName}");

                if (file.RootToken is not JArray array)
                    return CliResponse.Fail("root is not an array");

                var entry = array
                    .OfType<JObject>()
                    .FirstOrDefault(obj =>
                        string.Equals(obj["ID"]?.ToString(), id, StringComparison.OrdinalIgnoreCase));

                if (entry == null)
                    return CliResponse.Fail($"ID not found: {id}");

                if (relPath != null)
                {
                    // 单属性 upsert
                    UpsertByRelativePath(entry, relPath, newValue);
                }
                else
                {
                    // 整体 merge：将 value 对象的所有属性写入条目
                    foreach (var prop in ((JObject)newValue).Properties())
                    {
                        entry[prop.Name] = prop.Value;
                    }
                }

                // 标记 dirty
                if (file is JsonDataFile jdf)
                    jdf.IsDirty = true;

                _onDataModified?.Invoke();

                if (relPath != null)
                    return CliResponse.Success($"updated {id}.{relPath} in {fileName}");
                else
                    return CliResponse.Success($"merged {((JObject)newValue).Count} fields into {id} in {fileName}");
            });
        }

        /// <summary>
        /// 按点分相对路径 upsert 值。路径中间节点不存在时自动创建 JObject。
        /// 例如 "stats.atk" 在 entry 上操作 entry["stats"]["atk"]。
        /// </summary>
        private static void UpsertByRelativePath(JObject root, string relativePath, JToken value)
        {
            var segments = relativePath.Split('.');
            JObject current = root;

            // 遍历到倒数第二层，确保中间节点存在
            for (int i = 0; i < segments.Length - 1; i++)
            {
                var seg = segments[i];
                var child = current[seg];

                if (child is JObject childObj)
                {
                    current = childObj;
                }
                else
                {
                    // 中间节点不存在或不是 JObject → 创建
                    var newObj = new JObject();
                    current[seg] = newObj;
                    current = newObj;
                }
            }

            // 设置最终属性
            var lastSeg = segments[segments.Length - 1];
            current[lastSeg] = value;
        }
    }
}
