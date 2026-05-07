using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DataManager.Core.Services
{
    /// <summary>
    /// JSON 文件读写、校验服务。
    /// </summary>
    public class JsonFileService
    {
        /// <summary>
        /// 从磁盘读取并解析 JSON 文件，返回 JToken 根节点。
        /// </summary>
        public JToken ReadJson(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"JSON 文件不存在: {filePath}", filePath);

            var text = File.ReadAllText(filePath);

            try
            {
                return JToken.Parse(text);
            }
            catch (JsonReaderException ex)
            {
                throw new JsonReaderException($"JSON 格式错误: {filePath} — {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 将 JToken 序列化并写入磁盘。
        /// </summary>
        public void WriteJson(string filePath, JToken token)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var text = token.ToString(Formatting.Indented);
            File.WriteAllText(filePath, text);
        }

        /// <summary>
        /// 扫描指定文件夹下所有 .json 文件路径。
        /// </summary>
        public IReadOnlyList<string> ScanJsonFiles(string directoryPath, bool recursive = true)
        {
            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException($"目录不存在: {directoryPath}");

            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.GetFiles(directoryPath, "*.json", option);
        }
    }
}
