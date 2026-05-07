using Newtonsoft.Json.Linq;

namespace DataManager.Core.Base.Interface
{
    /// <summary>
    /// JSON 文件契约。一个 .json 文件的抽象。
    /// </summary>
    public interface IJsonDataFile
    {
        /// <summary>文件完整路径</summary>
        string FilePath { get; }

        /// <summary>文件名（含扩展名）</summary>
        string FileName { get; }

        /// <summary>JSON 根节点</summary>
        JToken? RootToken { get; }

        /// <summary>是否有未保存的修改</summary>
        bool IsDirty { get; }

        /// <summary>从磁盘加载 JSON</summary>
        void Load();

        /// <summary>保存到磁盘</summary>
        void Save();

        /// <summary>JSONPath 查询</summary>
        IEnumerable<JToken> Query(string jsonPath);

        /// <summary>修改指定 JSONPath 处的值</summary>
        void Modify(string jsonPath, JToken value);
    }
}
