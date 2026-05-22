using System;
using System.Linq;
using System.Windows.Threading;
using DataManager.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DataManager.Cli.Commands
{
    /// <summary>
    /// status 命令。返回工作区状态信息。
    /// </summary>
    public class StatusCommand : CliCommandBase
    {
        private readonly Func<string?> _workspaceIdAccessor;

        public StatusCommand(
            Dispatcher dispatcher,
            Func<DataWorkspace?> workspaceAccessor,
            Func<string?> workspaceIdAccessor)
            : base(dispatcher, workspaceAccessor)
        {
            _workspaceIdAccessor = workspaceIdAccessor;
        }

        public override string Name => "status";

        public override CliResponse Execute(CliParseResult parsed)
        {
            return InvokeOnUI(() =>
            {
                var ws = GetWorkspaceOrThrow();

                var dirtyFiles = ws.Files.Where(f => f.IsDirty).Select(f => f.FileName).ToList();

                var result = new JObject
                {
                    ["workspaceId"] = _workspaceIdAccessor(),
                    ["rootPath"] = ws.RootPath,
                    ["fileCount"] = ws.Files.Count,
                    ["dirtyFiles"] = new JArray(dirtyFiles.ToArray<object>())
                };

                return CliResponse.Success(result.ToString(Formatting.None));
            });
        }
    }
}
