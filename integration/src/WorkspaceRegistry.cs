using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace CommonCli
{
    /// <summary>
    /// 工作区注册表。基于 %LOCALAPPDATA%\common-cli\{toolName}\registry.json 实现。
    ///
    /// 工具进程启动时调用 Register 把自己加入注册表，关闭时调用 Unregister 清理。
    /// cli 端 list/auto 命令通过这个文件发现存活工作区。
    ///
    /// 并发控制使用 FileStream.Lock() / Unlock()，多个工具实例同时读写不会损坏。
    ///
    /// 用法:
    ///   var registry = new WorkspaceRegistry("MyTool");
    ///   registry.Register("project1", "我的项目");
    ///   ...
    ///   registry.Unregister("project1");
    ///   registry.Dispose();
    /// </summary>
    public class WorkspaceRegistry : IDisposable
    {
        private readonly string _registryPath;
        private bool _disposed;

        public WorkspaceRegistry(string toolName)
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "common-cli",
                toolName);
            _registryPath = Path.Combine(dir, "registry.json");
        }

        public void Register(string id, string description)
        {
            EnsureDirectory();
            WithFileLock(writable: true, (data) =>
            {
                var workspaces = GetWorkspacesArray(data);

                // 已存在同 id → 覆盖
                var existing = workspaces.FirstOrDefault(w => w["id"]?.ToString() == id);
                if (existing != null)
                    workspaces.Remove(existing);

                workspaces.Add(new JObject
                {
                    ["id"] = id,
                    ["description"] = description
                });

                return true; // 写回
            });
        }

        public void Unregister(string id)
        {
            if (!File.Exists(_registryPath))
                return;

            WithFileLock(writable: true, (data) =>
            {
                var workspaces = GetWorkspacesArray(data);

                var existing = workspaces.FirstOrDefault(w => w["id"]?.ToString() == id);
                if (existing != null)
                {
                    workspaces.Remove(existing);
                    return true; // 写回
                }

                return false; // 无变化
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // 没有持久句柄，每次操作都是 open/close 一次
        }

        private void EnsureDirectory()
        {
            string dir = Path.GetDirectoryName(_registryPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        /// <summary>
        /// 用独占锁打开 registry 文件，读取 JSON，调 action，
        /// action 返回 true 时写回。
        /// </summary>
        private void WithFileLock(bool writable, Func<JObject, bool> action)
        {
            var mode = writable ? FileMode.OpenOrCreate : FileMode.Open;
            var access = writable ? FileAccess.ReadWrite : FileAccess.Read;
            var share = FileShare.None; // 独占

            using var fs = new FileStream(_registryPath, mode, access, share);

            long lockLen = Math.Max(fs.Length, 1);
            fs.Lock(0, lockLen);
            try
            {
                JObject data;
                if (fs.Length > 0)
                {
                    var bytes = new byte[fs.Length];
                    fs.ReadExactly(bytes, 0, bytes.Length);
                    string json = Encoding.UTF8.GetString(bytes);
                    try
                    {
                        data = JObject.Parse(json);
                    }
                    catch
                    {
                        data = new JObject { ["workspaces"] = new JArray() };
                    }
                }
                else
                {
                    data = new JObject { ["workspaces"] = new JArray() };
                }

                bool shouldWrite = action(data);

                if (shouldWrite && writable)
                {
                    string output = data.ToString(Newtonsoft.Json.Formatting.None);
                    byte[] outputBytes = Encoding.UTF8.GetBytes(output);

                    fs.Seek(0, SeekOrigin.Begin);
                    fs.SetLength(outputBytes.Length);
                    fs.Write(outputBytes, 0, outputBytes.Length);
                    fs.Flush();
                }
            }
            finally
            {
                fs.Unlock(0, lockLen);
            }
        }

        private static JArray GetWorkspacesArray(JObject data)
        {
            if (data["workspaces"] is JArray arr)
                return arr;
            var newArr = new JArray();
            data["workspaces"] = newArr;
            return newArr;
        }
    }
}
