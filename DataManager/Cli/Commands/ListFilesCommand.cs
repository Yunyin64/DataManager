using System;
using System.IO;
using System.Windows.Threading;
using DataManager.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DataManager.Cli.Commands
{
    /// <summary>
    /// list-files 命令。列出工作区所有 JSON 文件（精简格式）。
    /// </summary>
    public class ListFilesCommand : CliCommandBase
    {
        public ListFilesCommand(
            Dispatcher dispatcher,
            Func<DataWorkspace?> workspaceAccessor)
            : base(dispatcher, workspaceAccessor)
        {
        }

        public override string Name => "list-files";

        public override CliResponse Execute(CliParseResult parsed)
        {
            return InvokeOnUI(() =>
            {
                var ws = GetWorkspaceOrThrow();

                var names = new JArray();
                var dirtyNames = new JArray();
                foreach (var file in ws.Files)
                {
                    var name = Path.GetFileNameWithoutExtension(file.FileName);
                    names.Add(name);
                    if (file.IsDirty)
                        dirtyNames.Add(name);
                }

                var result = new JObject
                {
                    ["workspace"] = ws.RootPath,
                    ["count"] = ws.Files.Count,
                    ["files"] = names
                };

                // 只有存在脏文件时才输出 dirty 字段
                if (dirtyNames.Count > 0)
                    result["dirty"] = dirtyNames;

                return CliResponse.Success(result.ToString(Formatting.None));
            });
        }
    }
}
