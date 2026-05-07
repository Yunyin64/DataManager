using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DataManager.Core.Base.Interface;
using DataManager.Data;

namespace DataManager.Domain.FileTree
{
    /// <summary>
    /// 文件树 ViewModel。管理左侧文件导航列表。
    /// </summary>
    public partial class FileTreeViewModel : Core.Base.ViewModelBase
    {
        /// <summary>文件列表项集合</summary>
        public ObservableCollection<FileItemViewModel> Files { get; } = new();

        /// <summary>当前选中的文件项</summary>
        [ObservableProperty]
        private FileItemViewModel? _selectedFile;

        /// <summary>
        /// 加载工作区中的所有文件到列表。
        /// </summary>
        public void LoadWorkspace(DataWorkspace workspace)
        {
            Files.Clear();
            SelectedFile = null;

            foreach (var file in workspace.Files)
            {
                Files.Add(new FileItemViewModel(file));
            }
        }
    }

    /// <summary>
    /// 文件列表项 ViewModel。包装 IJsonDataFile，提供显示名称。
    /// </summary>
    public partial class FileItemViewModel : Core.Base.ViewModelBase
    {
        public FileItemViewModel(IJsonDataFile file)
        {
            File = file;
        }

        /// <summary>底层数据文件引用</summary>
        public IJsonDataFile File { get; }

        /// <summary>
        /// 显示名称：文件名 + 脏标记。
        /// </summary>
        public string DisplayName => File.IsDirty ? $"{File.FileName}*" : File.FileName;

        /// <summary>
        /// 刷新显示名称（当 IsDirty 状态变化时调用）。
        /// </summary>
        public void RefreshDisplay()
        {
            OnPropertyChanged(nameof(DisplayName));
        }
    }
}
