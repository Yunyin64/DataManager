using System.Collections.Generic;
using System.Linq;

namespace CommonCli
{
    /// <summary>
    /// CLI 子命令的解析结果。
    /// </summary>
    public class CliParseResult
    {
        public string Command { get; }
        public Dictionary<string, string> Options { get; } = new();
        public HashSet<string> Flags { get; } = new();
        public List<string> Args { get; } = new();

        public CliParseResult(string command) => Command = command;

        public bool HasFlag(string name) => Flags.Contains(name);

        public string GetOption(string name) =>
            Options.TryGetValue(name, out var v) ? v : null;

        public int? GetIntOption(string name)
        {
            var v = GetOption(name);
            return v != null && int.TryParse(v, out var n) ? n : (int?)null;
        }

        public HashSet<string> GetCsvOption(string name)
        {
            var v = GetOption(name);
            return v != null
                ? new HashSet<string>(v.Split(',').Where(s => s.Length > 0))
                : null;
        }
    }

    /// <summary>
    /// 简易 GNU 风格 CLI 参数解析器。
    ///
    /// 支持:
    ///   --long-name value      → Options["long-name"] = "value"
    ///   --flag                 → Flags 含 "flag"
    ///   -s value               → 通过 aliases 映射为长名
    ///   -f                     → 同上，作为 flag
    ///   其他                    → 加入 Args 位置参数列表
    ///
    /// 通过 valueOptions 区分"带值选项"和"布尔标志"。
    ///
    /// 用法:
    ///   var aliases = new Dictionary<string, string> { {"t", "table"}, {"n", "limit"} };
    ///   var valueOpts = new HashSet<string> { "table", "limit" };
    ///   var parsed = CliArgParser.Parse(args, aliases, valueOpts);
    ///   if (parsed.GetOption("table") != null) ...
    ///   if (parsed.HasFlag("verbose")) ...
    /// </summary>
    public static class CliArgParser
    {
        /// <param name="args">原始参数数组（第一个元素为子命令）</param>
        /// <param name="aliases">短选项 → 长选项 别名映射</param>
        /// <param name="valueOptions">需要带值的选项名集合（长名）</param>
        public static CliParseResult Parse(
            string[] args,
            Dictionary<string, string> aliases = null,
            HashSet<string> valueOptions = null)
        {
            if (args == null || args.Length == 0)
                return new CliParseResult(null);

            var result = new CliParseResult(args[0]);
            aliases = aliases ?? new Dictionary<string, string>();
            valueOptions = valueOptions ?? new HashSet<string>();

            for (int i = 1; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg.StartsWith("--") && arg.Length > 2)
                {
                    var name = arg.Substring(2);
                    if (valueOptions.Contains(name) && i + 1 < args.Length)
                    {
                        result.Options[name] = args[++i];
                    }
                    else
                    {
                        result.Flags.Add(name);
                    }
                }
                else if (arg.StartsWith("-") && arg.Length == 2 && arg[1] != '-')
                {
                    var shortName = arg.Substring(1);
                    var longName = aliases.TryGetValue(shortName, out var ln) ? ln : shortName;

                    if (valueOptions.Contains(longName) && i + 1 < args.Length)
                    {
                        result.Options[longName] = args[++i];
                    }
                    else
                    {
                        result.Flags.Add(longName);
                    }
                }
                else
                {
                    result.Args.Add(arg);
                }
            }

            return result;
        }
    }
}
