namespace TaskTable.Core.Models;

public sealed class TimeEntry
{
    public long Id { get; set; }
    public string TaskId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    /// <summary>已结算的秒数；EndTime 为空时表示进行中，按时间戳差值实时计算。</summary>
    public long DurationSeconds { get; set; }

    public bool IsRunning => EndTime is null;

    public TimeSpan EffectiveDuration => EndTime is null
        ? TimeSpan.FromSeconds(DurationSeconds) + (DateTime.Now - StartTime)
        : TimeSpan.FromSeconds(DurationSeconds);
}
