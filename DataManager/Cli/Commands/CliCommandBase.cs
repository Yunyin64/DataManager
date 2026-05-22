using System;
using System.IO;
using System.Windows.Threading;
using DataManager.Core.Base.Interface;
using DataManager.Data;

namespace DataManager.Cli.Commands
{
    /// <summary>
    /// CLI 命令抽象基类。封装 UI 线程调度、工作区访问、文件查找等公共逻辑。
    /// </summary>
    public abstract class CliCommandBase : ICliCommand
    {
        protected readonly Dispatcher _dispatcher;
        protected readonly Func<DataWorkspace?> _workspaceAccessor;
        protected readonly Action? _onDataModified;

        protected CliCommandBase(
            Dispatcher dispatcher,
            Func<DataWorkspace?> workspaceAccessor,
            Action? onDataModified = null)
        {
            _dispatcher = dispatcher;
            _workspaceAccessor = workspaceAccessor;
            _onDataModified = onDataModified;
        }

        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public abstract CliResponse Execute(CliParseResult parsed);

        /// <summary>
        /// 在 UI 线程上执行操作并返回结果。
        /// </summary>
        protected CliResponse InvokeOnUI(Func<CliResponse> action)
        {
            try
            {
                return _dispatcher.Invoke(action);
            }
            catch (Exception ex)
            {
                return CliResponse.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 获取当前工作区，未加载则抛异常。
        /// </summary>
        protected DataWorkspace GetWorkspaceOrThrow()
        {
            var ws = _workspaceAccessor?.Invoke();
            if (ws == null)
                throw new InvalidOperationException("no workspace loaded");
            return ws;
        }

        /// <summary>
        /// 按文件名查找文件。支持带或不带 .json 后缀。
        /// </summary>
        protected static IJsonDataFile? FindFile(DataWorkspace ws, string name)
        {
            // 先精确匹配
            var file = ws.GetFile(name);
            if (file != null) return file;

            // 不带后缀 → 加上 .json 再试
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                file = ws.GetFile(name + ".json");
                if (file != null) return file;
            }

            // 带后缀 → 去掉后缀试试
            if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                file = ws.GetFile(Path.GetFileNameWithoutExtension(name));
            }

            return file;
        }
    }
}
