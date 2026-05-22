using System.Windows;
using DataManager.Cli;
using DataManager.Core.Utils;
using DataManager.Domain.Main;

namespace DataManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private CliService? _cliService;
        private MainViewModel? _mainVM;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 确保 common-cli.exe 所在目录在用户 PATH 中
            PathRegistrar.EnsureCliInPath();

            // 创建 CLI 服务（使用当前 Dispatcher 确保 UI 线程安全）
            _cliService = new CliService(Dispatcher);

            // MainWindow 由 StartupUri 自动创建，在 Activated 中获取 ViewModel
            // 但此时 MainWindow 已经由 XAML 创建完毕，可以直接获取
            // 通过 Startup 事件后 MainWindow 已被赋值
        }

        /// <summary>
        /// 窗口加载完成后初始化 CLI 服务。
        /// 在 MainWindow 创建后调用，确保 DataContext 已设置。
        /// </summary>
        internal void InitializeCli(MainViewModel mainVM)
        {
            _mainVM = mainVM;

            // 配置 CLI 服务：提供 workspace 访问器和修改后的刷新回调
            _cliService?.Configure(
                workspaceAccessor: () => _mainVM.Workspace,
                onDataModified: () => _mainVM.RefreshAfterCliModification()
            );

            // 监听工作区变更，驱动 CLI 服务更新
            _mainVM.WorkspaceChanged += OnWorkspaceChanged;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 停止 CLI 服务，注销工作区
            _cliService?.Dispose();
            _cliService = null;

            base.OnExit(e);
        }

        private void OnWorkspaceChanged(string? workspacePath)
        {
            if (_cliService == null) return;

            if (workspacePath != null)
            {
                _cliService.StartWorkspace(workspacePath);
            }
            else
            {
                _cliService.StopWorkspace();
            }
        }
    }
}
