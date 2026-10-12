namespace TaskTable.Core.Services;

/// <summary>时长格式化：任务行用精确 M:SS / H:MM:SS，日历格与总览用紧凑 45m / 1h30。</summary>
public static class DurationFormatter
{
    public static string Precise(long seconds)
    {
        if (seconds <= 0) return "";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes}:{ts.Seconds:D2}";
    }

    public static string Compact(long seconds)
    {
        if (seconds < 60) return "";
        long minutes = seconds / 60;
        if (minutes < 60) return $"{minutes}m";
        long hours = minutes / 60, rest = minutes % 60;
        return rest == 0 ? $"{hours}h" : $"{hours}h{rest:D2}";
    }
}