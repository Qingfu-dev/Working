using TaskTable.Core.Models;

namespace TaskTable.Core.Services;

public enum DayVisualState
{
    /// <summary>白：无任务。</summary>
    Empty,
    /// <summary>灰：全部未开始，且日期在未来。</summary>
    Planned,
    /// <summary>红：全部未完成，且日期为今天或过去。</summary>
    Unfinished,
    /// <summary>绿：全部完成。</summary>
    AllDone,
    /// <summary>混色：部分完成，红绿对角分割。</summary>
    Mixed
}

public readonly record struct DayVisual(DayVisualState State, double DoneRatio);

/// <summary>
/// 日历配色引擎。判定顺序（命中即停）：
/// 1 无任务=白；2 全部完成=绿；3 部分完成=混色；4 全部未开始且未来=灰；5 其余=红。
/// </summary>
public static class DayStateCalculator
{
    public static DayVisual Calculate(DateOnly date, DateOnly today, int total, int done)
    {
        if (total <= 0) return new DayVisual(DayVisualState.Empty, 0);

        double ratio = (double)done / total;

        if (done == total) return new DayVisual(DayVisualState.AllDone, 1);
        if (done > 0) return new DayVisual(DayVisualState.Mixed, ratio);
        if (date > today) return new DayVisual(DayVisualState.Planned, 0);
        return new DayVisual(DayVisualState.Unfinished, 0);
    }

    public static DayVisual Calculate(DateOnly date, DateOnly today, IReadOnlyCollection<TaskItem> tasks)
    {
        int total = tasks.Count;
        int done = 0;
        foreach (var t in tasks)
            if (t.IsDone) done++;
        return Calculate(date, today, total, done);
    }
}
