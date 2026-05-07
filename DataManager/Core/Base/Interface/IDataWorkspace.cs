namespace DataManager.Core.Base.Interface
{
    /// <summary>
    /// 工作区契约。一个工作目录 = 一个工作区。
    /// </summary>
    public interface IDataWorkspace
    {
        /// <summary>工作区根目录路径</summary>
        string RootPath { get; }

        /// <summary>工作区中的所有 JSON 文件</summary>
        IReadOnlyList<IJsonDataFile> Files { get; }

        /// <summary>扫描文件夹下所有 .json 并加载</summary>
        void Load();

        /// <summary>保存所有脏文件</summary>
        void Save();

        /// <summary>按文件名获取文件（不含路径，含扩展名）</summary>
        IJsonDataFile? GetFile(string fileName);
    }
}
