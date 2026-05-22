using System;
using System.Windows.Threading;
using DataManager.Data;
using Newtonsoft.Json.Linq;

namespace DataManager.Cli.Commands
{
    /// <summary>
    /// set 命令。按全局 JSONPath 修改文件中已有节点的值。
    /// </summary>
    public class SetCommand : CliCommandBase
    {
        public SetCommand(
            Dispatcher dispatcher,
            Func<DataWorkspace?> workspaceAccessor,
            Action? onDataModified = null)
            : base(dispatcher, workspaceAccessor, onDataModified)
        {
        }

        public override string Name => "set";

        public override CliResponse Execute(CliParseResult parsed)
        {
            if (parsed.Args.Count == 0)
                return CliResponse.Fail("usage: set <file> --path <jsonpath> --value <json>");

            var fileName = parsed.Args[0];
            var jsonPath = parsed.GetOption("path");
            var valueStr = parsed.GetOption("value");

            if (jsonPath == null)
                return CliResponse.Fail("--path is required");
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

            return InvokeOnUI(() =>
            {
                var ws = GetWorkspaceOrThrow();
                var file = FindFile(ws, fileName);
                if (file == null)
                    return CliResponse.Fail($"file not found: {fileName}");

                file.Modify(jsonPath, newValue);

                _onDataModified?.Invoke();

                return CliResponse.Success($"modified {fileName} at {jsonPath}");
            });
        }
    }
}
