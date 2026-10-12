using TaskTable.Core.Models;

namespace TaskTable.Infra.Data;

public sealed class TimeEntryRepository
{
    private readonly Database _db;
    public TimeEntryRepository(Database db) => _db = db;

    /// <summary>开始计时：若已有进行中的记录则先结算掉（同一时刻只允许一条 running）。</summary>
    public TimeEntry Start(string taskId)
    {
        StopRunning();
        var entry = new TimeEntry { TaskId = taskId, StartTime = DateTime.Now };
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO time_entries (task_id, start_time, end_time, duration_seconds)
            VALUES ($task, $start, NULL, 0);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$task", taskId);
        cmd.Parameters.AddWithValue("$start", entry.StartTime.ToString("o"));
        entry.Id = (long)(cmd.ExecuteScalar() ?? 0L);
        return entry;
    }

    /// <summary>暂停/结束计时：在 C# 侧按时间戳差值结算（避免 SQLite 时区/格式歧义）。</summary>
    public void Stop(long entryId)
    {
        using var conn = _db.Open();

        DateTime start;
        long accrued;
        using (var read = conn.CreateCommand())
        {
            read.CommandText = "SELECT start_time, duration_seconds FROM time_entries WHERE id = $id AND end_time IS NULL;";
            read.Parameters.AddWithValue("$id", entryId);
            using var reader = read.ExecuteReader();
            if (!reader.Read()) return;
            start = DateTime.Parse(reader.GetString(0));
            accrued = reader.GetInt64(1);
        }

        long elapsed = accrued + Math.Max(0L, (long)(DateTime.Now - start).TotalSeconds);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE time_entries SET end_time = $end, duration_seconds = $dur WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", entryId);
        cmd.Parameters.AddWithValue("$end", DateTime.Now.ToString("o"));
        cmd.Parameters.AddWithValue("$dur", elapsed);
        cmd.ExecuteNonQuery();
    }

    public TimeEntry? GetRunning()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, task_id, start_time, duration_seconds FROM time_entries WHERE end_time IS NULL LIMIT 1;";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new TimeEntry
        {
            Id = reader.GetInt64(0),
            TaskId = reader.GetString(1),
            StartTime = DateTime.Parse(reader.GetString(2)),
            DurationSeconds = reader.GetInt64(3)
        };
    }

    /// <summary>启动兜底：上次异常退出遗留的 running 记录直接按开始时间结算。</summary>
    public void StopRunning()
    {
        var running = GetRunning();
        if (running is not null) Stop(running.Id);
    }

    /// <summary>某任务某天的累计秒数（进行中的记录在 C# 侧按时间戳差值实时累计）。</summary>
    public long GetSecondsForTaskOnDate(string taskId, DateOnly date)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT start_time, end_time, duration_seconds
            FROM time_entries
            WHERE task_id = $task AND substr(start_time, 1, 10) = $day;
            """;
        cmd.Parameters.AddWithValue("$task", taskId);
        cmd.Parameters.AddWithValue("$day", date.ToString("yyyy-MM-dd"));

        long total = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            long accrued = reader.GetInt64(2);
            if (reader.IsDBNull(1))
            {
                var start = DateTime.Parse(reader.GetString(0));
                accrued += Math.Max(0L, (long)(DateTime.Now - start).TotalSeconds);
            }
            total += accrued;
        }
        return total;
    }

    /// <summary>某天全部任务的累计秒数（含进行中记录的实时差值）。</summary>
    public long GetSecondsForDate(DateOnly date)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT start_time, end_time, duration_seconds FROM time_entries WHERE substr(start_time, 1, 10) = $day;";
        cmd.Parameters.AddWithValue("$day", date.ToString("yyyy-MM-dd"));
        long total = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            total += AccruedSeconds(reader);
        return total;
    }

    /// <summary>按月聚合每日累计秒数，供日历格显示时长标签。</summary>
    public Dictionary<DateOnly, long> GetMonthTimeSummary(int year, int month)
    {
        var from = new DateOnly(year, month, 1);
        return GetTimeByRange(from, from.AddMonths(1));
    }

    /// <summary>按日期区间（左闭右开）聚合每日累计秒数，供热力图使用。</summary>
    public Dictionary<DateOnly, long> GetTimeByRange(DateOnly from, DateOnly toExclusive)
    {
        var result = new Dictionary<DateOnly, long>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT start_time, end_time, duration_seconds
            FROM time_entries
            WHERE substr(start_time, 1, 10) >= $from AND substr(start_time, 1, 10) < $to;
            """;
        cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$to", toExclusive.ToString("yyyy-MM-dd"));
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (!DateOnly.TryParseExact(reader.GetString(0)[..10], "yyyy-MM-dd", out var day)) continue;
            long accrued = AccruedSeconds(reader);
            result[day] = result.TryGetValue(day, out long current) ? current + accrued : accrued;
        }
        return result;
    }

    /// <summary>单行结算：已完成记录取存储值；进行中记录叠加实时时间戳差值。</summary>
    private static long AccruedSeconds(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        long accrued = reader.GetInt64(2);
        if (!reader.IsDBNull(1)) return accrued;
        var start = DateTime.Parse(reader.GetString(0));
        return accrued + Math.Max(0L, (long)(DateTime.Now - start).TotalSeconds);
    }
}
