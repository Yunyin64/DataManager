using System;
using System.Linq;
using System.Windows.Threading;
using DataManager.Data;
using Newtonsoft.Json.Linq;

namespace DataManager.Cli.Commands
{
    /// <summary>
    /// add 命令。创建新条目（含 ID 和 DisplayName），追加到根数组末尾。
    /// </summary>
    public class AddCommand : CliCommandBase
    {
        public AddCommand(
            Dispatcher dispatcher,
            Func<DataWorkspace?> workspaceAccessor,
            Action? onDataModified = null)
            : base(dispatcher, workspaceAccessor, onDataModified)
        {
        }

        public override string Name => "add";

        public override CliResponse Execute(CliParseResult parsed)
        {
            if (parsed.Args.Count == 0)
                return CliResponse.Fail("usage: add <file> --ID <ID> --DisplayName <DisplayName>");

            var fileName = parsed.Args[0];
            var id = parsed.GetOption("ID");
            var displayName = parsed.GetOption("DisplayName");

            if (id == null)
                return CliResponse.Fail("--ID is required");
            if (displayName == null)
                return CliResponse.Fail("--DisplayName is required");

            return InvokeOnUI(() =>
            {
                var ws = GetWorkspaceOrThrow();
                var file = FindFile(ws, fileName);
                if (file == null)
                    return CliResponse.Fail($"file not found: {fileName}");

                if (file.RootToken is not JArray array)
                    return CliResponse.Fail("root is not an array");

                // 校验 ID 不重复
                var existing = array
                    .OfType<JObject>()
                    .FirstOrDefault(obj =>
                        string.Equals(obj["ID"]?.ToString(), id, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                    return CliResponse.Fail($"ID already exists: {id}");

                // 追加新条目
                var newEntry = new JObject
                {
                    ["ID"] = id,
                    ["DisplayName"] = displayName
                };
                array.Add(newEntry);

                // 标记 dirty
                if (file is JsonDataFile jdf)
                    jdf.IsDirty = true;

                _onDataModified?.Invoke();

                return CliResponse.Success($"added {id} ({displayName}) to {fileName}");
            });
        }
    }
}
