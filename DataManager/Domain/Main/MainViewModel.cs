using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataManager.Data;
using DataManager.Domain.FileTree;
using DataManager.Domain.JsonEditor;

namespace DataManager.Domain.Main
{
    /// <summary>
    /// 主窗口 ViewModel。管理工作区生命周期，协调子 ViewModel。
    /// </summary>
    public partial class MainViewModel : Core.Base.ViewModelBase
    {
        private DataWorkspace? _workspace;

        public MainViewModel()
        {
            FileTreeVM = new FileTreeViewModel();
            JsonEditorVM = new JsonEditorViewModel();

            // 监听文件选中变化
            FileTreeVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FileTreeViewModel.SelectedFile))
                {
                    OnSelectedFileChanged();
                }
            };
        }

        /// <summary>当前工作区（供 CliService 访问）</summary>
        public DataWorkspace? Workspace => _workspace;

        /// <summary>文件树子 ViewModel</summary>
        public FileTreeViewModel FileTreeVM { get; }

        /// <summary>JSON 编辑器子 ViewModel</summary>
        public JsonEditorViewModel JsonEditorVM { get; }

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

            _workspace = new DataWorkspace(folderPath);
            _workspace.Load();

            FileTreeVM.LoadWorkspace(_workspace);
            JsonEditorVM.Clear();

            UpdateStatusText();

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

            UpdateStatusText();
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
