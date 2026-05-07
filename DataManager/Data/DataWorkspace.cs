using System.IO;
using DataManager.Core.Base.Interface;
using DataManager.Core.Services;

namespace DataManager.Data
{
    /// <summary>
    /// IDataWorkspace 的默认实现。一个文件夹 = 一个工作区。
    /// </summary>
    public class DataWorkspace : IDataWorkspace
    {
        private readonly JsonFileService _fileService;
        private readonly List<IJsonDataFile> _files = new();

        public DataWorkspace(string rootPath, JsonFileService? fileService = null)
        {
            RootPath = Path.GetFullPath(rootPath);
            _fileService = fileService ?? new JsonFileService();
        }

        /// <inheritdoc/>
        public string RootPath { get; }

        /// <inheritdoc/>
        public IReadOnlyList<IJsonDataFile> Files => _files;

        /// <inheritdoc/>
        public void Load()
        {
            _files.Clear();

            var jsonPaths = _fileService.ScanJsonFiles(RootPath, recursive: true);

            foreach (var path in jsonPaths)
            {
                try
                {
                    var file = new JsonDataFile(path, _fileService);
                    file.Load();
                    _files.Add(file);
                }
                catch (Exception)
                {
                    // 跳过加载失败的文件（空文件、格式错误等）
                }
            }
        }

        /// <inheritdoc/>
        public void Save()
        {
            foreach (var file in _files)
            {
                if (file.IsDirty)
                    file.Save();
            }
        }

        /// <inheritdoc/>
        public IJsonDataFile? GetFile(string fileName)
        {
            return _files.FirstOrDefault(f =>
                string.Equals(f.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
