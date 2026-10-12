namespace TaskTable.Infra.System;

/// <summary>应用目录约定：%AppData%\TaskTable\。</summary>
public static class AppPaths
{
    public static string Root { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskTable");

    public static string DatabaseFile => Path.Combine(Root, "tasktable.db");
    public static string BackupDir => Path.Combine(Root, "backup");
    public static string LogDir => Path.Combine(Root, "logs");
    public static string InboxDir => Path.Combine(Root, "inbox");
    public static string InboxDoneDir => Path.Combine(InboxDir, "done");
    public static string InboxFailedDir => Path.Combine(InboxDir, "failed");

    public static void EnsureAll()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(BackupDir);
        Directory.CreateDirectory(LogDir);
        Directory.CreateDirectory(InboxDir);
        Directory.CreateDirectory(InboxDoneDir);
        Directory.CreateDirectory(InboxFailedDir);
    }
}
