using System;
using System.Windows.Threading;
using DataManager.Data;

namespace DataManager.Cli.Commands
{
    /// <summary>
    /// batch-update 命令。占位，暂未实现。
    /// 预期功能：批量按 id 修改多条条目的属性。
    /// </summary>
    public class BatchUpdateCommand : CliCommandBase
    {
        public BatchUpdateCommand(
            Dispatcher dispatcher,
            Func<DataWorkspace?> workspaceAccessor,
            Action? onDataModified = null)
            : base(dispatcher, workspaceAccessor, onDataModified)
        {
        }

        public override string Name => "batch-update";

        public override CliResponse Execute(CliParseResult parsed)
        {
            return CliResponse.Fail("not implemented");
        }
    }
}
