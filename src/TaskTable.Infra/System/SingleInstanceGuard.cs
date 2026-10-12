using System.IO.Pipes;
using System.Text;

namespace TaskTable.Infra.System;

/// <summary>
/// 单实例守卫：Mutex 防重复启动；二次启动时通过命名管道通知首个实例唤起主窗口。
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    public const string ActivateSignal = "activate";
    private const string MutexName = @"Local\TaskTable.SingleInstance";
    private const string PipeName = "TaskTable.SingleInstance.Pipe";

    private Mutex? _mutex;
    private CancellationTokenSource? _cts;

    public bool IsFirstInstance { get; private set; }

    public event Action? ActivateRequested;

    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        IsFirstInstance = createdNew;
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
        }
        return IsFirstInstance;
    }

    /// <summary>首实例：后台监听唤起信号。</summary>
    public void StartListening()
    {
        if (!IsFirstInstance) return;
        _cts = new CancellationTokenSource();
        Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    /// <summary>非首实例：给首实例发唤起信号。</summary>
    public static bool NotifyFirstInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1500);
            var bytes = Encoding.UTF8.GetBytes(ActivateSignal);
            client.Write(bytes, 0, bytes.Length);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(ct);
                using var reader = new StreamReader(server, Encoding.UTF8);
                var signal = await reader.ReadToEndAsync(ct);
                if (signal == ActivateSignal) ActivateRequested?.Invoke();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { AppLog.Warn($"单实例管道监听异常: {ex.Message}"); }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }
}
