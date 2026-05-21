using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DataManager.Domain.LuaViewer
{
    /// <summary>
    /// Lua 查阅/编辑面板 ViewModel。
    /// 根据选中行的 ID 自动加载对应的 Lua 文件，支持编辑和自动保存。
    /// </summary>
    public partial class LuaViewerViewModel : Core.Base.ViewModelBase
    {
        /// <summary>编辑器文本内容</summary>
        [ObservableProperty]
        private string _content = "";

        /// <summary>面板是否可见</summary>
        [ObservableProperty]
        private bool _isVisible;

        /// <summary>当前文件是否有未保存修改</summary>
        [ObservableProperty]
        private bool _isDirty;

        /// <summary>当前加载的文件名</summary>
        [ObservableProperty]
        private string _fileName = "";

        /// <summary>当前文件完整路径</summary>
        private string? _currentFilePath;

        /// <summary>加载内容时临时禁用脏标记</summary>
        private bool _isLoading;

        /// <summary>
        /// 切换到指定 ID 对应的 Lua 文件。
        /// 如果当前文件有修改，先自动保存再切换。
        /// </summary>
        /// <param name="workspaceRoot">工作区根路径</param>
        /// <param name="id">当前选中行的 ID 值（可能为 null）</param>
        public void SwitchToId(string? workspaceRoot, string? id)
        {
            System.Diagnostics.Debug.WriteLine($"[LuaPanel] SwitchToId: workspaceRoot={workspaceRoot ?? "null"}, id={id ?? "null"}");

            // 先保存当前修改
            if (IsDirty)
            {
                Save();
            }

            // 无工作区或无 ID → 隐藏
            if (string.IsNullOrEmpty(workspaceRoot) || string.IsNullOrEmpty(id))
            {
                System.Diagnostics.Debug.WriteLine($"[LuaPanel] SwitchToId: Hide (no root or no id)");
                Hide();
                return;
            }

            // 计算 Lua 文件路径
            var luaPath = Path.Combine(workspaceRoot, "Lua", $"{id}.lua");
            System.Diagnostics.Debug.WriteLine($"[LuaPanel] SwitchToId: luaPath={luaPath}, exists={File.Exists(luaPath)}");

            if (!File.Exists(luaPath))
            {
                Hide();
                return;
            }

            // 加载文件
            _currentFilePath = luaPath;
            _isLoading = true;
            Content = File.ReadAllText(luaPath);
            _isLoading = false;

            FileName = $"{id}.lua";
            IsDirty = false;
            IsVisible = true;
            System.Diagnostics.Debug.WriteLine($"[LuaPanel] SwitchToId: Loaded OK, IsVisible=true");
        }

        /// <summary>
        /// 保存当前内容到磁盘。
        /// </summary>
        public void Save()
        {
            if (_currentFilePath == null || !IsDirty)
                return;

            File.WriteAllText(_currentFilePath, Content);
            IsDirty = false;
        }

        /// <summary>
        /// 内容变更时标记脏状态。由 View 层的 TextChanged 事件调用。
        /// </summary>
        public void MarkContentChanged(string newContent)
        {
            if (_isLoading)
                return;

            _content = newContent;
            IsDirty = true;
        }

        /// <summary>
        /// 隐藏面板并清空状态。
        /// </summary>
        private void Hide()
        {
            IsVisible = false;
            _currentFilePath = null;
            FileName = "";
            _isLoading = true;
            Content = "";
            _isLoading = false;
            IsDirty = false;
        }
    }
}
