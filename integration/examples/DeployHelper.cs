using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CommonCli;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyTool
{
    /// <summary>
    /// 一键部署 helper。把 common-cli.exe + SKILL.md + 权限规则 一次性写入用户工作目录。
    ///
    /// 改编自 SchemaMaster/UI/AIAgentConfig.cs。
    ///
    /// 典型用法:
    ///   DeployHelper.InstallAll(workDir: @"D:\Projects\MyGame", toolName: "MyTool");
    /// </summary>
    public static class DeployHelper
    {
        // ===========================================================================
        //  入口：一次跑完
        // ===========================================================================

        /// <summary>
        /// 一键安装：cli 进 PATH + Skill 部署 + 权限放行。
        /// </summary>
        /// <param name="workDir">用户的项目目录（会写入 .claude/ 子目录）</param>
        /// <param name="toolName">你的工具名（要和 CommonCliServer 用的一致）</param>
        /// <param name="skillName">Skill 目录名，默认 "{toolName}-helper"</param>
        public static void InstallAll(string workDir, string toolName, string skillName = null)
        {
            skillName ??= $"{toolName.ToLowerInvariant()}-helper";

            DeployCommonCli();
            DeploySkill(workDir, skillName);
            EnsurePermission(workDir);
        }

        // ===========================================================================
        //  1. cli 部署到 PATH
        // ===========================================================================

        /// <summary>
        /// 把当前 exe 同目录下的 common-cli.exe 拷到 %LOCALAPPDATA%\Microsoft\WindowsApps，
        /// 这个目录默认就在用户 PATH 里。如果不在 PATH，会自动追加用户级 PATH。
        /// </summary>
        public static void DeployCommonCli()
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string srcCli = Path.Combine(exeDir, "common-cli.exe");
            if (!File.Exists(srcCli))
                throw new FileNotFoundException("common-cli.exe not found next to your exe", srcCli);

            string windowsAppsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WindowsApps");
            Directory.CreateDirectory(windowsAppsDir);

            string destCli = Path.Combine(windowsAppsDir, "common-cli.exe");
            File.Copy(srcCli, destCli, overwrite: true);

            EnsureInUserPath(windowsAppsDir);
        }

        private static void EnsureInUserPath(string directory)
        {
            try
            {
                var userPath = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_CURRENT_USER\Environment", "Path", "") as string ?? "";

                var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in userPath.Split(Path.PathSeparator))
                {
                    if (!string.IsNullOrWhiteSpace(p))
                        paths.Add(p.TrimEnd('\\', '/'));
                }

                if (!paths.Contains(directory.TrimEnd('\\', '/')))
                {
                    string newPath = string.IsNullOrEmpty(userPath) ? directory : userPath + ";" + directory;
                    Environment.SetEnvironmentVariable("Path", newPath, EnvironmentVariableTarget.User);
                    // 同时更新当前进程 PATH，省得用户重启 shell
                    var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                    Environment.SetEnvironmentVariable("PATH", currentPath + ";" + directory);
                }
            }
            catch
            {
                // 没有写注册表权限就算了，WindowsApps 目录默认本来就在 PATH 里
            }
        }

        // ===========================================================================
        //  2. Skill 部署到 .claude/skills/
        // ===========================================================================

        /// <summary>
        /// 把 SKILL.md 写到 <workDir>/.claude/skills/<skillName>/SKILL.md。
        /// 内容来源：你项目的内嵌资源 "MyTool.Resources.SKILL.md"。
        /// 如果你不用内嵌资源，把 LoadSkillContent 改成读字符串模板即可。
        /// </summary>
        public static void DeploySkill(string workDir, string skillName)
        {
            string skillDir = Path.Combine(workDir, ".claude", "skills", skillName);

            // 先清理旧的（避免历史残留文件）
            if (Directory.Exists(skillDir))
            {
                try { Directory.Delete(skillDir, recursive: true); } catch { }
            }
            Directory.CreateDirectory(skillDir);

            string skillContent = LoadSkillContent();
            File.WriteAllText(
                Path.Combine(skillDir, "SKILL.md"),
                skillContent,
                Encoding.UTF8);
        }

        public static void UninstallSkill(string workDir, string skillName)
        {
            string skillDir = Path.Combine(workDir, ".claude", "skills", skillName);
            if (Directory.Exists(skillDir))
                Directory.Delete(skillDir, recursive: true);
        }

        // 改成你自己的资源加载逻辑。例子：从内嵌资源读取。
        private static string LoadSkillContent()
        {
            const string resourceName = "MyTool.Resources.SKILL.md";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidOperationException(
                    $"Embedded resource {resourceName} not found. " +
                    "Set Build Action = EmbeddedResource on Resources/SKILL.md, or replace this method with inline string.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        // ===========================================================================
        //  3. settings.local.json 加权限放行
        // ===========================================================================

        private const string CommonCliPermission = "Bash(*common-cli.exe *)";

        /// <summary>
        /// 往 <workDir>/.claude/settings.local.json 的 permissions.allow 里追加规则。
        /// 已存在则跳过；其他键完整保留。
        /// </summary>
        public static void EnsurePermission(string workDir)
        {
            string claudeDir = Path.Combine(workDir, ".claude");
            Directory.CreateDirectory(claudeDir);
            string settingsPath = Path.Combine(claudeDir, "settings.local.json");

            JObject settings;
            if (File.Exists(settingsPath))
            {
                try { settings = JObject.Parse(File.ReadAllText(settingsPath)); }
                catch { settings = new JObject(); }
            }
            else
            {
                settings = new JObject();
            }

            if (settings["permissions"] is not JObject perms)
                settings["permissions"] = perms = new JObject();
            if (perms["allow"] is not JArray allow)
                perms["allow"] = allow = new JArray();

            if (!allow.Any(t => (string)t == CommonCliPermission))
                allow.Add(CommonCliPermission);

            File.WriteAllText(settingsPath, settings.ToString(Formatting.Indented));
        }
    }
}
