namespace DataManager.Cli.Commands
{
    /// <summary>
    /// CLI 命令契约。每个命令实现此接口。
    /// </summary>
    public interface ICliCommand
    {
        /// <summary>命令名称（小写，用于路由匹配）</summary>
        string Name { get; }

        /// <summary>
        /// 执行命令。
        /// </summary>
        /// <param name="parsed">解析后的参数</param>
        /// <returns>CLI 响应</returns>
        CliResponse Execute(CliParseResult parsed);
    }
}
