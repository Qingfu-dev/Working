using Microsoft.Data.Sqlite;
using TaskTable.Infra.System;

namespace TaskTable.Infra.Data;

/// <summary>SQLite 连接工厂 + 建表 + 每日首次启动自动备份。</summary>
public sealed class Database
{
    public string ConnectionString { get; }

    public Database()
    {
        AppPaths.EnsureAll();
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = AppPaths.DatabaseFile,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
        Initialize();
        BackupOncePerDay();
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    private void Initialize()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS tasks (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                note TEXT NULL,
                plan_date TEXT NOT NULL,
                status INTEGER NOT NULL DEFAULT 0,
                priority INTEGER NOT NULL DEFAULT 2,
                tags TEXT NULL,
                source TEXT NOT NULL DEFAULT 'manual',
                estimate_minutes INTEGER NULL,
                created_at TEXT NOT NULL,
                completed_at TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_tasks_plan_date ON tasks(plan_date);

            CREATE TABLE IF NOT EXISTS time_entries (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                task_id TEXT NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
                start_time TEXT NOT NULL,
                end_time TEXT NULL,
                duration_seconds INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_time_entries_task ON time_entries(task_id);

            CREATE TABLE IF NOT EXISTS expenses (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                amount REAL NOT NULL,
                category TEXT NOT NULL,
                expense_date TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_expenses_date ON expenses(expense_date);
            """;
        cmd.ExecuteNonQuery();
    }

    private void BackupOncePerDay()
    {
        try
        {
            var file = AppPaths.DatabaseFile;
            if (!File.Exists(file)) return;
            var stamp = Path.Combine(AppPaths.BackupDir, $"tasktable-{DateTime.Today:yyyy-MM-dd}.db");
            if (!File.Exists(stamp)) File.Copy(file, stamp);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"数据库备份失败: {ex.Message}");
        }
    }
}
