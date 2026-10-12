using System.Collections.ObjectModel;
using System.Windows.Media;
using TaskTable.Core.Services;
using TaskTable.Infra.Data;

namespace TaskTable.App.ViewModels;

/// <summary>近一年专注热力图：7 行（周一~周日）× 53 列（周），列优先填充由生成顺序保证。</summary>
public sealed class HeatmapViewModel
{
    public ObservableCollection<HeatmapCell> Cells { get; } = new();
    public string SummaryText { get; }

    private static readonly Brush EmptyBrush = Frozen(0xEF, 0xEF, 0xF2);
    private static readonly Brush Level1Brush = Frozen(0xB4, 0xFD, 0xD9);
    private static readonly Brush Level2Brush = Frozen(0x5C, 0xF5, 0xA8);
    private static readonly Brush Level3Brush = Frozen(0x0F, 0xDC, 0x78);
    private static readonly Brush Level4Brush = Frozen(0x02, 0x74, 0x3B);

    public HeatmapViewModel(TimeEntryRepository repo, DateOnly today)
    {
        // 本周周一 → 往前 52 周，共 53 列
        var mondayThisWeek = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var start = mondayThisWeek.AddDays(-52 * 7);
        var data = repo.GetTimeByRange(start, today.AddDays(1));

        long total = data.Values.Sum();
        int activeDays = data.Count(kv => kv.Value >= 60);
        SummaryText = total >= 60
            ? $"近一年累计 {DurationFormatter.Compact(total)} · 有记录 {activeDays} 天"
            : "近一年暂无计时记录";

        // UniformGrid 按行填充：索引 = 星期 * 53 + 周序，从而形成"每周一列"
        for (int day = 0; day < 7; day++)
        {
            for (int week = 0; week < 53; week++)
            {
                var date = start.AddDays(week * 7 + day);
                if (date > today)
                {
                    Cells.Add(new HeatmapCell(Brushes.Transparent, null));
                    continue;
                }
                long sec = data.TryGetValue(date, out long value) ? value : 0;
                string tip = sec >= 60
                    ? $"{date:yyyy-MM-dd} · 专注 {DurationFormatter.Compact(sec)}"
                    : $"{date:yyyy-MM-dd} · 无记录";
                Cells.Add(new HeatmapCell(Level(sec), tip));
            }
        }
    }

    private static Brush Level(long seconds)
    {
        if (seconds < 60) return EmptyBrush;
        if (seconds < 15 * 60) return Level1Brush;
        if (seconds < 30 * 60) return Level2Brush;
        if (seconds < 60 * 60) return Level3Brush;
        return Level4Brush;
    }

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

public sealed class HeatmapCell
{
    public Brush Fill { get; }
    public string? Tip { get; }

    public HeatmapCell(Brush fill, string? tip)
    {
        Fill = fill;
        Tip = tip;
    }
}