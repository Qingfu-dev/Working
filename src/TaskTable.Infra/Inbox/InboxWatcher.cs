using TaskTable.Core.Inbox;
using TaskTable.Core.Models;
using TaskTable.Infra.Data;
using TaskTable.Infra.System;

namespace TaskTable.Infra.Inbox;

/// <summary>
/// AI 收件箱监听：FileSystemWatcher + 2s 轮询兜底。
/// 新 .json → 解析 → 直接入列 → 移入 done\；失败 → 移入 failed\ + 日志。
/// </summary>
public sealed class InboxWatcher : IDisposable
{
    private readonly TaskRepository _tasks;
    private FileSystemWatcher? _watcher;
    private Timer? _pollTimer;
    private readonly object _gate = new();
    private readonly HashSet<string> _processing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>成功入列后触发（UI 线程自行刷新）。</summary>
    public event Action<int>? TasksImported;

    public InboxWatcher(TaskRepository tasks) => _tasks = tasks;

    public void Start()
    {
        AppPaths.EnsureAll();

        _watcher = new FileSystemWatcher(AppPaths.InboxDir, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };
        _watcher.Created += (_, e) => TryProcess(e.FullPath);
        _watcher.Renamed += (_, e) => TryProcess(e.FullPath);

        _pollTimer = new Timer(_ => ScanPending(), null, 2000, 2000);
        ScanPending(); // 启动时先消化存量
        AppLog.Info($"收件箱监听已启动: {AppPaths.InboxDir}");
    }

    private void ScanPending()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(AppPaths.InboxDir, "*.json"))
                TryProcess(file);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"收件箱扫描异常: {ex.Message}");
        }
    }

    private void TryProcess(string path)
    {
        lock (_gate)
        {
            if (!_processing.Add(path)) return;
        }
        try
        {
            Process(path);
        }
        finally
        {
            lock (_gate) _processing.Remove(path);
        }
    }

    private void Process(string path)
    {
        if (!File.Exists(path)) return;

        string json;
        try
        {
            // tmp→json 原子重命名后文件应已写完；短暂等待句柄释放
            json = ReadWithRetry(path);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"收件箱文件读取失败 {Path.GetFileName(path)}: {ex.Message}");
            return; // 下次轮询再试
        }

        List<TaskItem> tasks;
        try
        {
            tasks = InboxTaskParser.Parse(json, DateOnly.FromDateTime(DateTime.Today));
        }
        catch (InboxParseException ex)
        {
            AppLog.Error($"收件箱解析失败 {Path.GetFileName(path)}: {ex.Message}");
            MoveTo(path, AppPaths.InboxFailedDir);
            return;
        }

        _tasks.InsertMany(tasks);
        AppLog.Info($"AI 任务入列 {tasks.Count} 条 ← {Path.GetFileName(path)}");
        MoveTo(path, AppPaths.InboxDoneDir);
        TasksImported?.Invoke(tasks.Count);
    }

    private static string ReadWithRetry(string path)
    {
        for (int i = 0; ; i++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException) when (i < 5)
            {
                Thread.Sleep(200);
            }
        }
    }

    private static void MoveTo(string path, string dir)
    {
        try
        {
            var name = $"{DateTime.Now:yyyyMMdd-HHmmss}-{Path.GetFileName(path)}";
            File.Move(path, Path.Combine(dir, name), overwrite: true);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"收件箱文件移动失败 {Path.GetFileName(path)}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _pollTimer?.Dispose();
    }
}
