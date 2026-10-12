using System.Collections.Concurrent;

namespace TaskTable.Infra.System;

/// <summary>轻量文件日志：%AppData%\TaskTable\logs\yyyy-MM-dd.log，按天滚动。</summary>
public static class AppLog
{
    private static readonly ConcurrentQueue<string> _pending = new();
    private static readonly object _gate = new();
    private static bool _started;

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        _pending.Enqueue($"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}");
        if (_started) return;
        lock (_gate)
        {
            if (_started) return;
            _started = true;
            Task.Run(FlushLoopAsync);
        }
    }

    private static async Task FlushLoopAsync()
    {
        while (true)
        {
            try
            {
                while (_pending.TryDequeue(out var line))
                    File.AppendAllText(CurrentFile, line + Environment.NewLine);
            }
            catch { /* 日志失败不影响主流程 */ }
            await Task.Delay(1000);
        }
    }

    private static string CurrentFile =>
        Path.Combine(AppPaths.LogDir, $"{DateTime.Now:yyyy-MM-dd}.log");
}
