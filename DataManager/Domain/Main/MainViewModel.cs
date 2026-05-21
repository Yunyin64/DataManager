using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataManager.Data;
using DataManager.Domain.FileTree;
using DataManager.Domain.JsonEditor;
using DataManager.Domain.LuaViewer;

namespace DataManager.Domain.Main
{
    /// <summary>
    /// 主窗口 ViewModel。管理工作区生命周期，协调子 ViewModel。
    /// </summary>
    public partial class MainViewModel : Core.Base.ViewModelBase
    {
        private DataWorkspace? _workspace;
        private readonly DispatcherTimer _autoReloadTimer;

        public MainViewModel()
        {
            FileTreeVM = new FileTreeViewModel();
            JsonEditorVM = new JsonEditorViewModel();
            LuaViewerVM = new LuaViewerViewModel();

            // 监听文件选中变化
            FileTreeVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FileTreeViewModel.SelectedFile))
                {
                    OnSelectedFileChanged();
                }
            };

            // 监听行选中变化，驱动 Lua 面板
            JsonEditorVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(JsonEditorViewModel.SelectedRowId))
                {
                    OnSelectedRowIdChanged();
                }
            };

            // 每秒自动重载数据源
            _autoReloadTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _autoReloadTimer.Tick += OnAutoReloadTick;
        }

        /// <summary>当前工作区（供 CliService 访问）</summary>
        public DataWorkspace? Workspace => _workspace;

        /// <summary>文件树子 ViewModel</summary>
        public FileTreeViewModel FileTreeVM { get; }

        /// <summary>JSON 编辑器子 ViewModel</summary>
        public JsonEditorViewModel JsonEditorVM { get; }

        /// <summary>Lua 查阅/编辑面板子 ViewModel</summary>
        public LuaViewerViewModel LuaViewerVM { get; }

        /// <summary>状态栏文本</summary>
        [ObservableProperty]
        private string _statusText = "就绪";

        /// <summary>工作区变更事件（供 App 层监听，驱动 CliService 更新）</summary>
        public event Action<string?>? WorkspaceChanged;

        /// <summary>
        /// 打开文件夹命令。
        /// </summary>
        [RelayCommand]
        private void OpenFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "选择 JSON 数据文件夹"
            };

            if (dialog.ShowDialog() != true)
                return;

            var folderPath = dialog.FolderName;

            // 切换工作区前停止旧定时器
            _autoReloadTimer.Stop();

            _workspace = new DataWorkspace(folderPath);
            _workspace.Load();

            FileTreeVM.LoadWorkspace(_workspace);
            JsonEditorVM.Clear();

            UpdateStatusText();

            // 启动自动重载定时器
            _autoReloadTimer.Start();

            // 通知 CLI 服务工作区已变更
            WorkspaceChanged?.Invoke(folderPath);
        }

        /// <summary>
        /// 保存命令。保存所有脏文件。
        /// </summary>
        [RelayCommand]
        private void Save()
        {
            if (_workspace == null)
                return;

            _workspace.Save();

            // 刷新文件列表的脏标记
            foreach (var fileItem in FileTreeVM.Files)
            {
                fileItem.RefreshDisplay();
            }

            UpdateStatusText();
        }

        // ── 自动重载 ──────────────────────────────────────

        /// <summary>
        /// 定时器回调：每秒重载所有已加载文件。
        /// 当编辑器正在编辑时，跳过当前文件的重载以避免覆盖用户修改。
        /// </summary>
        private void OnAutoReloadTick(object? sender, EventArgs e)
        {
            if (_workspace == null)
                return;

            // 如果正在编辑，跳过整个重载周期
            if (JsonEditorVM.IsEditing)
                return;

            // 重载所有非脏文件（脏文件说明有内存中的修改，不应从磁盘覆盖）
            foreach (var file in _workspace.Files)
            {
                if (file.IsDirty)
                    continue;

                try
                {
                    file.Load();
                }
                catch (Exception)
                {
                    // 文件暂时不可读（被占用、已删除等），跳过本轮
                }
            }

            // 刷新文件列表的脏标记
            foreach (var fileItem in FileTreeVM.Files)
            {
                fileItem.RefreshDisplay();
            }

            // 增量刷新当前编辑器（保持列宽、滚动位置等 UI 状态）
            var selected = FileTreeVM.SelectedFile;
            if (selected != null)
            {
                JsonEditorVM.RefreshData(selected.File);
            }
        }

        // ── 私有方法 ──────────────────────────────────────

        /// <summary>
        /// CLI 修改数据后刷新 UI（脏标记、编辑器内容）。
        /// 需在 UI 线程调用。
        /// </summary>
        public void RefreshAfterCliModification()
        {
            // 刷新文件列表的脏标记
            foreach (var fileItem in FileTreeVM.Files)
            {
                fileItem.RefreshDisplay();
            }

            // 如果当前选中的文件正在编辑器中显示，重新加载
            var selected = FileTreeVM.SelectedFile;
            if (selected != null)
            {
                JsonEditorVM.LoadFile(selected.File);
            }

            UpdateStatusText();
        }

        private void OnSelectedFileChanged()
        {
            var selected = FileTreeVM.SelectedFile;
            if (selected == null)
            {
                JsonEditorVM.Clear();
            }
            else
            {
                JsonEditorVM.LoadFile(selected.File);
            }

            // 切换文件时重置 Lua 面板
            LuaViewerVM.SwitchToId(_workspace?.RootPath, JsonEditorVM.SelectedRowId);

            UpdateStatusText();
        }

        /// <summary>
        /// 选中行 ID 变化时，驱动 Lua 面板加载对应文件。
        /// </summary>
        private void OnSelectedRowIdChanged()
        {
            LuaViewerVM.SwitchToId(_workspace?.RootPath, JsonEditorVM.SelectedRowId);
        }

        private void UpdateStatusText()
        {
            if (_workspace == null)
            {
                StatusText = "就绪";
                return;
            }

            var fileCount = _workspace.Files.Count;
            var baseText = $"已加载 {fileCount} 个文件";

            var selected = FileTreeVM.SelectedFile;
            if (selected != null)
            {
                var recordInfo = JsonEditorVM.HasData
                    ? $"{selected.File.FileName} - {JsonEditorVM.RecordCount} 条记录"
                    : selected.File.FileName;
                StatusText = $"{baseText} | {recordInfo}";
            }
            else
            {
                StatusText = baseText;
            }
        }
    }
}
