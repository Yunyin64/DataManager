using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;

namespace CommonCli
{
    /// <summary>
    /// common-cli 响应结构。对应 JSON: {"code": int, "output": string, "error": string}
    /// </summary>
    public class CliResponse
    {
        public int Code;
        public string Output = "";
        public string Error = "";

        public static CliResponse Success(string output = "") =>
            new CliResponse { Code = 0, Output = output ?? "" };

        public static CliResponse Fail(string error, int code = 1) =>
            new CliResponse { Code = code, Error = error ?? "" };
    }

    /// <summary>
    /// common-cli 命名管道服务端。
    ///
    /// 监听 \\.\pipe\common-cli-{toolName}-{workspaceId}，
    /// 接收 {"args":[...]} 请求，返回 {"code","output","error"} 响应。
    ///
    /// handler 在管道 IO 线程执行。如需访问 UI 线程资源，请在 handler 内部
    /// 自行 Dispatcher.Invoke / SynchronizationContext.Post 切到主线程。
    ///
    /// 用法:
    ///   var server = new CommonCliServer("MyTool", "MyWorkspace", args =>
    ///   {
    ///       return args[0] switch
    ///       {
    ///           "ping" => CliResponse.Success("pong"),
    ///           _ => CliResponse.Fail("unknown")
    ///       };
    ///   });
    ///   server.Start();
    /// </summary>
    public class CommonCliServer
    {
        private readonly string _pipeName;
        private readonly Func<string[], CliResponse> _handler;
        private CancellationTokenSource _cts;
        private bool _started;

        public event Action<Exception> OnException;

        /// <param name="toolName">工具名（与 cli 端调用时第一个参数一致）</param>
        /// <param name="workspaceId">工作区唯一标识</param>
        /// <param name="handler">请求处理函数。接收 args 数组，返回 CliResponse。</param>
        public CommonCliServer(string toolName, string workspaceId, Func<string[], CliResponse> handler)
        {
            _pipeName = "common-cli-" + toolName + "-" + workspaceId;
            _handler = handler;
        }

        public void Start(int maxClients = 4)
        {
            if (_started) return;
            _started = true;
            _cts = new CancellationTokenSource();

            for (int i = 0; i < maxClients; i++)
            {
                new Thread(PipeWorker) { IsBackground = true }.Start();
            }
        }

        public void Stop()
        {
            if (!_started) return;
            _started = false;
            _cts.Cancel();
        }

        private NamedPipeServerStream CreatePipe()
        {
            var pipeSecurity = new PipeSecurity();

            // 允许所有用户读写（AI Agent 可能以不同用户身份运行）
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));

            // 当前用户完全控制
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                WindowsIdentity.GetCurrent().User,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

            return NamedPipeServerStreamAcl.Create(
                _pipeName, PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
                65536, 65536, pipeSecurity);
        }

        private void PipeWorker()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    using var pipe = CreatePipe();
                    pipe.WaitForConnectionAsync(_cts.Token).Wait();

                    using var sr = new StreamReader(pipe);
                    using var sw = new StreamWriter(pipe);

                    while (pipe.IsConnected)
                    {
                        var readTask = sr.ReadLineAsync();
                        readTask.Wait(_cts.Token);
                        string line = readTask.Result;
                        if (line == null) break;

                        string[] args = ParseRequest(line);
                        CliResponse response = CallHandler(args);
                        string respJson = SerializeResponse(response);

                        sw.WriteLineAsync(respJson).Wait(_cts.Token);
                        sw.FlushAsync().Wait(_cts.Token);
                    }
                }
                catch (OperationCanceledException) { }
                catch (AggregateException ex) when (ex.InnerException is OperationCanceledException) { }
                catch (Exception ex)
                {
                    OnException?.Invoke(ex);
                }
            }
        }

        private string[] ParseRequest(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var argsElement = doc.RootElement.GetProperty("args");
                var args = new string[argsElement.GetArrayLength()];
                for (int i = 0; i < args.Length; i++)
                    args[i] = argsElement[i].GetString() ?? "";
                return args;
            }
            catch
            {
                return new[] { json };
            }
        }

        private CliResponse CallHandler(string[] args)
        {
            try
            {
                return _handler(args);
            }
            catch (Exception ex)
            {
                return CliResponse.Fail(ex.Message);
            }
        }

        private string SerializeResponse(CliResponse response)
        {
            return JsonSerializer.Serialize(new
            {
                code = response.Code,
                output = response.Output ?? "",
                error = response.Error ?? ""
            });
        }
    }
}
