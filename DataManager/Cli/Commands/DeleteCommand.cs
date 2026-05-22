using System;
using System.Linq;
using System.Windows.Threading;
using DataManager.Data;
using Newtonsoft.Json.Linq;

namespace DataManager.Cli.Commands
{
    /// <summary>
    /// delete 命令。按 id 从根数组中删除一条条目。
    /// </summary>
    public class DeleteCommand : CliCommandBase
    {
        public DeleteCommand(
            Dispatcher dispatcher,
            Func<DataWorkspace?> workspaceAccessor,
            Action? onDataModified = null)
            : base(dispatcher, workspaceAccessor, onDataModified)
        {
        }

        public override string Name => "delete";

        public override CliResponse Execute(CliParseResult parsed)
        {
            if (parsed.Args.Count == 0)
                return CliResponse.Fail("usage: delete <file> --ID <ID>");

            var fileName = parsed.Args[0];
            var id = parsed.GetOption("ID");

            if (id == null)
                return CliResponse.Fail("--ID is required");

            return InvokeOnUI(() =>
            {
                var ws = GetWorkspaceOrThrow();
                var file = FindFile(ws, fileName);
                if (file == null)
                    return CliResponse.Fail($"file not found: {fileName}");

                if (file.RootToken is not JArray array)
                    return CliResponse.Fail("root is not an array");

                var match = array
                    .OfType<JObject>()
                    .FirstOrDefault(obj =>
                        string.Equals(obj["ID"]?.ToString(), id, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                    return CliResponse.Fail($"id not found: {id}");

                match.Remove();

                // 标记 dirty
                if (file is JsonDataFile jdf)
                    jdf.IsDirty = true;

                _onDataModified?.Invoke();

                return CliResponse.Success($"deleted {id} from {fileName}");
            });
        }
    }
}
