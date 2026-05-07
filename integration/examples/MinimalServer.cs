using System;
using System.Linq;
using CommonCli;

namespace MyTool
{
    /// <summary>
    /// 最小可运行例子：演示注册工作区 + 启动 CLI 服务 + 实现几个简单命令。
    ///
    /// 把 src/ 下三个文件加到你的项目，引用 Newtonsoft.Json + System.IO.Pipes.AccessControl，
    /// 然后跑这段代码就能在终端通过 common-cli.exe 调用。
    ///
    /// 测试:
    ///   common-cli.exe MyTool list
    ///   common-cli.exe MyTool auto ping
    ///   common-cli.exe MyTool auto echo hello world
    ///   common-cli.exe MyTool auto add 3 4
    /// </summary>
    public static class MinimalServer
    {
        private static WorkspaceRegistry _registry;
        private static CommonCliServer _server;

        public static void Start()
        {
            // 1. 把当前工作区注册进来
            _registry = new WorkspaceRegistry("MyTool");
            _registry.Register("default", "默认工作区");

            // 2. 起 CLI 服务
            _server = new CommonCliServer("MyTool", "default", Handler);
            _server.OnException += ex => Console.Error.WriteLine($"[CLI] {ex}");
            _server.Start();

            Console.WriteLine("CLI server running. Try: common-cli.exe MyTool auto ping");
        }

        public static void Stop()
        {
            _server?.Stop();
            _registry?.Unregister("default");
            _registry?.Dispose();
        }

        // handler 在 IO 线程执行。涉及 UI 的操作要自己 Dispatcher.Invoke。
        private static CliResponse Handler(string[] args)
        {
            if (args == null || args.Length == 0)
                return CliResponse.Fail("usage: <command> [args...]\navailable: ping, echo, add");

            try
            {
                return args[0] switch
                {
                    "ping" => CliResponse.Success("pong"),
                    "echo" => CliResponse.Success(string.Join(" ", args.Skip(1))),
                    "add"  => HandleAdd(args),
                    _      => CliResponse.Fail($"unknown command: {args[0]}")
                };
            }
            catch (Exception ex)
            {
                return CliResponse.Fail(ex.Message);
            }
        }

        private static CliResponse HandleAdd(string[] args)
        {
            if (args.Length < 3)
                return CliResponse.Fail("usage: add <a> <b>");
            if (!int.TryParse(args[1], out int a) || !int.TryParse(args[2], out int b))
                return CliResponse.Fail("both arguments must be integers");
            return CliResponse.Success((a + b).ToString());
        }
    }
}
