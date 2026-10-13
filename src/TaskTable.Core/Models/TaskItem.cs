namespace TaskTable.Core.Models;

public enum TaskStatus
{
    Todo = 0,
    InProgress = 1,
    Done = 2,
    Postponed = 3
}

public sealed class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;

    /// <summary>计划日期（本地日期，yyyy-MM-dd）。</summary>
    public DateOnly PlanDate { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.Todo;

    /// <summary>1 高 / 2 中 / 3 低。</summary>
    public int Priority { get; set; } = 2;

    /// <summary>来源标记：manual / ai / ai:xxx。</summary>
    public string Source { get; set; } = "manual";

    public int? EstimateMinutes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }

    public bool IsDone => Status == TaskStatus.Done;
    public bool IsFromAi => Source.StartsWith("ai", StringComparison.OrdinalIgnoreCase);
}
