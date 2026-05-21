using System.IO;
using Newtonsoft.Json.Linq;
using DataManager.Core.Base.Interface;
using DataManager.Core.Services;

namespace DataManager.Data
{
    /// <summary>
    /// IJsonDataFile 的默认实现。单个 JSON 文件的抽象。
    /// 基于 Newtonsoft.Json 的 JToken 做底层数据存储。
    /// </summary>
    public class JsonDataFile : IJsonDataFile
    {
        private readonly JsonFileService _fileService;

        public JsonDataFile(string filePath, JsonFileService fileService)
        {
            FilePath = Path.GetFullPath(filePath);
            FileName = Path.GetFileName(filePath);
            _fileService = fileService;
        }

        /// <inheritdoc/>
        public string FilePath { get; }

        /// <inheritdoc/>
        public string FileName { get; }

        /// <inheritdoc/>
        public JToken? RootToken { get; private set; }

        /// <inheritdoc/>
        public bool IsDirty { get; internal set; }

        /// <inheritdoc/>
        public void Load()
        {
            RootToken = _fileService.ReadJson(FilePath);
            IsDirty = false;
        }

        /// <inheritdoc/>
        public void Save()
        {
            if (RootToken == null)
                throw new InvalidOperationException($"文件尚未加载，无法保存: {FilePath}");

            _fileService.WriteJson(FilePath, RootToken);
            IsDirty = false;
        }

        /// <inheritdoc/>
        public IEnumerable<JToken> Query(string jsonPath)
        {
            if (RootToken == null)
                return Enumerable.Empty<JToken>();

            return RootToken.SelectTokens(jsonPath);
        }

        /// <inheritdoc/>
        public void Modify(string jsonPath, JToken value)
        {
            if (RootToken == null)
                throw new InvalidOperationException($"文件尚未加载，无法修改: {FilePath}");

            var token = RootToken.SelectToken(jsonPath);
            if (token == null)
                throw new ArgumentException($"JSONPath 未匹配到节点: {jsonPath}");

            token.Replace(value);
            IsDirty = true;
        }
    }
}
