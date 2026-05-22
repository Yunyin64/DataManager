using System;
using System.Linq;
using System.Windows.Threading;
using DataManager.Data;

namespace DataManager.Cli.Commands
{
    /// <summary>
    /// save 命令。保存指定文件或所有脏文件到磁盘。
    /// </summary>
    public class SaveCommand : CliCommandBase
    {
        public SaveCommand(
            Dispatcher dispatcher,
            Func<DataWorkspace?> workspaceAccessor,
            Action? onDataModified = null)
            : base(dispatcher, workspaceAccessor, onDataModified)
        {
        }

        public override string Name => "save";

        public override CliResponse Execute(CliParseResult parsed)
        {
            var fileName = parsed.Args.Count > 0 ? parsed.Args[0] : null;

            return InvokeOnUI(() =>
            {
                var ws = GetWorkspaceOrThrow();

                if (fileName != null)
                {
                    // 保存指定文件
                    var file = FindFile(ws, fileName);
                    if (file == null)
                        return CliResponse.Fail($"file not found: {fileName}");

                    file.Save();
                    _onDataModified?.Invoke();

                    return CliResponse.Success($"saved {file.FileName}");
                }
                else
                {
                    // 保存所有脏文件
                    var dirtyFiles = ws.Files.Where(f => f.IsDirty).ToList();
                    if (dirtyFiles.Count == 0)
                        return CliResponse.Success("no dirty files to save");

                    foreach (var f in dirtyFiles)
                        f.Save();

                    _onDataModified?.Invoke();

                    var names = dirtyFiles.Select(f => f.FileName);
                    return CliResponse.Success($"saved {dirtyFiles.Count} file(s): {string.Join(", ", names)}");
                }
            });
        }
    }
}
