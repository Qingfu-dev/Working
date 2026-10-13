namespace TaskTable.Core.Services;

/// <summary>
/// 峰谷时段规则（北京时间）：
/// 高峰（梁文锋时段）＝周一至周五 9:00-12:00、14:00-18:00（不含法定节假日）；
/// 低谷（梁文谷时段）＝其余全部时间（含周末与法定节假日全天），价格为高峰时段的一半。
/// 法定节假日内置 2025 / 2026 年国务院办公厅放假安排。
/// </summary>
public static class PeakHours
{
    private static readonly HashSet<DateTime> HolidaySet = BuildHolidays();

    /// <summary>该时刻是否处于高峰时段。</summary>
    public static bool IsPeak(DateTime local)
    {
        if (IsWeekend(local.DayOfWeek) || HolidaySet.Contains(local.Date)) return false;
        int minutes = local.Hour * 60 + local.Minute;
        return (minutes >= 9 * 60 && minutes < 12 * 60) || (minutes >= 14 * 60 && minutes < 18 * 60);
    }

    /// <summary>是否为法定节假日（内置年份范围内，含调休连休日）。</summary>
    public static bool IsHoliday(DateTime date) => HolidaySet.Contains(date.Date);

    public static bool IsWeekend(DayOfWeek day) => day is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>下一个峰谷切换时刻；扫描 8 天仍无切换时返回 null（正常规则下不会发生）。</summary>
    public static DateTime? NextSwitch(DateTime local)
    {
        bool current = IsPeak(local);
        int[] hours = { 0, 9, 12, 14, 18 };
        for (int d = 0; d < 8; d++)
        {
            var day = local.Date.AddDays(d);
            foreach (int h in hours)
            {
                var t = day.AddHours(h);
                if (t <= local) continue;
                if (IsPeak(t) != current) return t;
            }
        }
        return null;
    }

    private static HashSet<DateTime> BuildHolidays()
    {
        var set = new HashSet<DateTime>();
        void Range(int y1, int m1, int d1, int y2, int m2, int d2)
        {
            for (var d = new DateTime(y1, m1, d1); d <= new DateTime(y2, m2, d2); d = d.AddDays(1))
                set.Add(d);
        }

        // 2025 年（国办发明电〔2024〕7号）
        Range(2025, 1, 1, 2025, 1, 1);      // 元旦
        Range(2025, 1, 28, 2025, 2, 4);     // 春节
        Range(2025, 4, 4, 2025, 4, 6);      // 清明节
        Range(2025, 5, 1, 2025, 5, 5);      // 劳动节
        Range(2025, 5, 31, 2025, 6, 2);     // 端午节
        Range(2025, 10, 1, 2025, 10, 8);    // 国庆节、中秋节

        // 2026 年（国办发明电〔2025〕7号）
        Range(2026, 1, 1, 2026, 1, 3);      // 元旦
        Range(2026, 2, 15, 2026, 2, 23);    // 春节
        Range(2026, 4, 4, 2026, 4, 6);      // 清明节
        Range(2026, 5, 1, 2026, 5, 5);      // 劳动节
        Range(2026, 6, 19, 2026, 6, 21);    // 端午节
        Range(2026, 9, 25, 2026, 9, 27);    // 中秋节
        Range(2026, 10, 1, 2026, 10, 7);    // 国庆节

        return set;
    }
}