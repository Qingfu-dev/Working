using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskTable.Core.Services;
using TaskTable.Infra.Data;

namespace TaskTable.App.ViewModels;

/// <summary>
/// 近一年热力图：7 行（周一~周日）× 53 列（周），列优先填充由生成顺序保证。
/// 两种视图：0 = 专注时长（绿色系），1 = 每日消费（琥珀色系）。
/// </summary>
public sealed partial class HeatmapViewModel : ObservableObject
{
    private readonly Dictionary<DateOnly, long> _focusData;
    private readonly Dictionary<DateOnly, decimal> _expenseData;
    private readonly DateOnly _start;
    private readonly DateOnly _today;

    public ObservableCollection<HeatmapCell> Cells { get; } = new();

    /// <summary>0 = 专注时长，1 = 每日消费。</summary>
    [ObservableProperty] private int _modeIndex;

    partial void OnModeIndexChanged(int value) => Rebuild();

    public string HeaderText => ModeIndex == 0 ? "专注热力图" : "消费热力图";

    public string SummaryText
    {
        get
        {
            if (ModeIndex == 1)
            {
                decimal total = _expenseData.Values.Sum();
                int days = _expenseData.Count(kv => kv.Value > 0);
                return total > 0
                    ? $"近一年累计消费 ¥{total:0.00} · 有记录 {days} 天"
                    : "近一年暂无消费记录";
            }

            long seconds = _focusData.Values.Sum();
            int activeDays = _focusData.Count(kv => kv.Value >= 60);
            return seconds >= 60
                ? $"近一年累计 {DurationFormatter.Compact(seconds)} · 有记录 {activeDays} 天"
                : "近一年暂无计时记录";
        }
    }

    /// <summary>图例色板：空 + 4 档（随视图切换）。</summary>
    public Brush[] LegendBrushes => ModeIndex == 0 ? FocusLegend : ExpenseLegend;

    // ── 专注（绿） ──
    private static readonly Brush[] FocusLegend =
    [
        Frozen(0xEF, 0xEF, 0xF2), Frozen(0xB4, 0xFD, 0xD9), Frozen(0x5C, 0xF5, 0xA8),
        Frozen(0x0F, 0xDC, 0x78), Frozen(0x02, 0x74, 0x3B)
    ];

    // ── 消费（琥珀） ──
    private static readonly Brush[] ExpenseLegend =
    [
        Frozen(0xEF, 0xEF, 0xF2), Frozen(0xFF, 0xE3, 0xB0), Frozen(0xFF, 0xC9, 0x6B),
        Frozen(0xF5, 0x9E, 0x0B), Frozen(0xB4, 0x53, 0x09)
    ];

    public HeatmapViewModel(TimeEntryRepository timeRepo, ExpenseRepository expenseRepo, DateOnly today)
    {
        // 本周周一 → 往前 52 周，共 53 列
        var mondayThisWeek = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        _start = mondayThisWeek.AddDays(-52 * 7);
        _today = today;
        _focusData = timeRepo.GetTimeByRange(_start, today.AddDays(1));
        _expenseData = expenseRepo.GetDailyTotals(_start, today.AddDays(1));
        Rebuild();
    }

    private void Rebuild()
    {
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(LegendBrushes));

        Cells.Clear();
        // UniformGrid 按行填充：索引 = 星期 * 53 + 周序，从而形成"每周一列"
        for (int day = 0; day < 7; day++)
        {
            for (int week = 0; week < 53; week++)
            {
                var date = _start.AddDays(week * 7 + day);
                if (date > _today)
                {
                    Cells.Add(new HeatmapCell(Brushes.Transparent, null));
                    continue;
                }
                Cells.Add(ModeIndex == 1 ? ExpenseCell(date) : FocusCell(date));
            }
        }
    }

    private HeatmapCell FocusCell(DateOnly date)
    {
        long sec = _focusData.TryGetValue(date, out long value) ? value : 0;
        string tip = sec >= 60
            ? $"{date:yyyy-MM-dd} · 专注 {DurationFormatter.Compact(sec)}"
            : $"{date:yyyy-MM-dd} · 无记录";
        return new HeatmapCell(FocusLevel(sec), tip);
    }

    private HeatmapCell ExpenseCell(DateOnly date)
    {
        decimal amount = _expenseData.TryGetValue(date, out decimal value) ? value : 0;
        string tip = amount > 0
            ? $"{date:yyyy-MM-dd} · 消费 ¥{amount:0.00}"
            : $"{date:yyyy-MM-dd} · 无记录";
        return new HeatmapCell(ExpenseLevel(amount), tip);
    }

    private static Brush FocusLevel(long seconds)
    {
        if (seconds < 60) return FocusLegend[0];
        if (seconds < 15 * 60) return FocusLegend[1];
        if (seconds < 30 * 60) return FocusLegend[2];
        if (seconds < 60 * 60) return FocusLegend[3];
        return FocusLegend[4];
    }

    /// <summary>消费档位（每日合计）：&lt;50 / &lt;100 / &lt;200 / ≥200 元。</summary>
    private static Brush ExpenseLevel(decimal amount)
    {
        if (amount <= 0) return ExpenseLegend[0];
        if (amount < 50) return ExpenseLegend[1];
        if (amount < 100) return ExpenseLegend[2];
        if (amount < 200) return ExpenseLegend[3];
        return ExpenseLegend[4];
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