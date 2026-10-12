using Microsoft.Data.Sqlite;
using TaskTable.Core.Models;
using TaskStatus = TaskTable.Core.Models.TaskStatus;

namespace TaskTable.Infra.Data;

public sealed class TaskRepository
{
    private readonly Database _db;
    public TaskRepository(Database db) => _db = db;

    public void Insert(TaskItem task)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO tasks (id, title, note, plan_date, status, priority, tags, source, estimate_minutes, created_at, completed_at)
            VALUES ($id, $title, $note, $date, $status, $priority, $tags, $source, $est, $created, $completed);
            """;
        Bind(cmd, task);
        cmd.ExecuteNonQuery();
    }

    public void InsertMany(IEnumerable<TaskItem> tasks)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        foreach (var task in tasks)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO tasks (id, title, note, plan_date, status, priority, tags, source, estimate_minutes, created_at, completed_at)
                VALUES ($id, $title, $note, $date, $status, $priority, $tags, $source, $est, $created, $completed);
                """;
            Bind(cmd, task);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void UpdateStatus(string id, TaskStatus status)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = status == TaskStatus.Done
            ? "UPDATE tasks SET status = $status, completed_at = $completed WHERE id = $id;"
            : "UPDATE tasks SET status = $status, completed_at = NULL WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$status", (int)status);
        if (status == TaskStatus.Done)
            cmd.Parameters.AddWithValue("$completed", DateTime.Now.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void Delete(string id)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM tasks WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public List<TaskItem> GetByDate(DateOnly date) => Query("plan_date = $d", c =>
        c.Parameters.AddWithValue("$d", date.ToString("yyyy-MM-dd")));

    public List<TaskItem> GetByMonth(int year, int month)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1);
        return Query("plan_date >= $from AND plan_date < $to", c =>
        {
            c.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
            c.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));
        });
    }

    /// <summary>按月聚合：plan_date → (total, done)，供日历配色引擎批量使用。</summary>
    public Dictionary<DateOnly, (int Total, int Done)> GetMonthSummary(int year, int month)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1);
        var result = new Dictionary<DateOnly, (int, int)>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT plan_date, COUNT(*), SUM(CASE WHEN status = 2 THEN 1 ELSE 0 END)
            FROM tasks WHERE plan_date >= $from AND plan_date < $to GROUP BY plan_date;
            """;
        cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var date = DateOnly.ParseExact(reader.GetString(0), "yyyy-MM-dd");
            result[date] = (reader.GetInt32(1), reader.IsDBNull(2) ? 0 : (int)reader.GetInt64(2));
        }
        return result;
    }

    private List<TaskItem> Query(string where, Action<SqliteCommand> bindWhere)
    {
        var list = new List<TaskItem>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT id, title, note, plan_date, status, priority, tags, source, estimate_minutes, created_at, completed_at
            FROM tasks WHERE {where} ORDER BY priority ASC, created_at ASC;
            """;
        bindWhere(cmd);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new TaskItem
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Note = reader.IsDBNull(2) ? null : reader.GetString(2),
                PlanDate = DateOnly.ParseExact(reader.GetString(3), "yyyy-MM-dd"),
                Status = (TaskStatus)reader.GetInt32(4),
                Priority = reader.GetInt32(5),
                Tags = reader.IsDBNull(6) ? null : reader.GetString(6),
                Source = reader.GetString(7),
                EstimateMinutes = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                CreatedAt = DateTime.Parse(reader.GetString(9)),
                CompletedAt = reader.IsDBNull(10) ? null : DateTime.Parse(reader.GetString(10))
            });
        }
        return list;
    }

    private static void Bind(SqliteCommand cmd, TaskItem t)
    {
        cmd.Parameters.AddWithValue("$id", t.Id);
        cmd.Parameters.AddWithValue("$title", t.Title);
        cmd.Parameters.AddWithValue("$note", (object?)t.Note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$date", t.PlanDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$status", (int)t.Status);
        cmd.Parameters.AddWithValue("$priority", t.Priority);
        cmd.Parameters.AddWithValue("$tags", (object?)t.Tags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$source", t.Source);
        cmd.Parameters.AddWithValue("$est", (object?)t.EstimateMinutes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created", t.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$completed", (object?)t.CompletedAt?.ToString("o") ?? DBNull.Value);
    }
}
