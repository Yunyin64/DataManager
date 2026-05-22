using System.IO;
using Microsoft.Win32;

namespace DataManager.Core.Utils
{
    /// <summary>
    /// 将指定目录注册到用户级 PATH 环境变量。
    /// </summary>
    public static class PathRegistrar
    {
        private const string CliExeName = "common-cli.exe";

        /// <summary>
        /// 查找 common-cli.exe 并将其所在目录加入用户 PATH（如尚未存在）。
        /// </summary>
        /// <returns>注册成功或已存在返回 true，找不到 exe 返回 false。</returns>
        public static bool EnsureCliInPath()
        {
            var cliDir = FindCliDirectory();
            if (cliDir == null)
                return false;

            var currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";

            // 检查是否已包含该目录
            var dirs = currentPath.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var dir in dirs)
            {
                if (string.Equals(dir.TrimEnd('\\', '/'), cliDir.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                    return true; // 已注册
            }

            // 追加到 PATH
            var newPath = string.IsNullOrEmpty(currentPath)
                ? cliDir
                : currentPath + ";" + cliDir;

            Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);

            // 同时更新当前进程 PATH（方便调试）
            var processPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            Environment.SetEnvironmentVariable("PATH", processPath + ";" + cliDir);

            // 广播环境变量变更通知（让已打开的资源管理器/终端感知到变化）
            BroadcastEnvironmentChange();

            return true;
        }

        /// <summary>
        /// 查找 common-cli.exe 所在目录。
        /// 搜索策略：从 WPF 程序 exe 位置向上查找解决方案根目录，
        /// 然后在 common-cli 的标准输出目录中查找。
        /// </summary>
        private static string? FindCliDirectory()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;

            // 策略1：exe 同目录（发布时可能复制到一起）
            var sameDirPath = Path.Combine(appDir, CliExeName);
            if (File.Exists(sameDirPath))
                return appDir.TrimEnd('\\');

            // 策略2：解决方案结构 — 向上找到包含 common-cli 文件夹的目录
            var searchDir = appDir;
            for (int i = 0; i < 6; i++)
            {
                var parent = Directory.GetParent(searchDir);
                if (parent == null) break;
                searchDir = parent.FullName;

                var cliProjectDir = Path.Combine(searchDir, "common-cli");
                if (Directory.Exists(cliProjectDir))
                {
                    // 查找各种输出目录
                    string[] candidates =
                    [
                        Path.Combine(cliProjectDir, "x64", "Release"),
                        Path.Combine(cliProjectDir, "x64", "Debug"),
                        Path.Combine(cliProjectDir, "Release"),
                        Path.Combine(cliProjectDir, "Debug"),
                    ];

                    foreach (var candidate in candidates)
                    {
                        var exePath = Path.Combine(candidate, CliExeName);
                        if (File.Exists(exePath))
                            return candidate;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 广播 WM_SETTINGCHANGE 通知其他进程环境变量已变更。
        /// </summary>
        private static void BroadcastEnvironmentChange()
        {
            try
            {
                // P/Invoke SendMessageTimeout 广播环境变更
                nint HWND_BROADCAST = 0xFFFF;
                uint WM_SETTINGCHANGE = 0x001A;
                nint result;

                SendMessageTimeout(
                    HWND_BROADCAST,
                    WM_SETTINGCHANGE,
                    0,
                    "Environment",
                    0x0002, // SMTO_ABORTIFHUNG
                    5000,
                    out result);
            }
            catch
            {
                // 广播失败不影响主流程
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern nint SendMessageTimeout(
            nint hWnd,
            uint Msg,
            nint wParam,
            string lParam,
            uint fuFlags,
            uint uTimeout,
            out nint lpdwResult);
    }
}
