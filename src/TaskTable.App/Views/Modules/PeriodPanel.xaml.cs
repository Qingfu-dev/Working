using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TaskTable.Core.Services;

namespace TaskTable.App.Views.Modules;

/// <summary>
/// 峰谷时段面板：显示当前处于梁文锋时段（红，高峰价格）还是梁文谷时段（绿，半价）、
/// 今日 24 小时时段分布与规则说明。
/// </summary>
public partial class PeriodPanel : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<Border> _cells = new();
    private DateTime _timelineDay = DateTime.MinValue;

    public PeriodPanel()
    {
        InitializeComponent();
        UpdateAll();
        _timer.Tick += (_, _) => UpdateAll();
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    private void UpdateAll()
    {
        var now = DateTime.Now;
        bool peak = PeakHours.IsPeak(now);
        var red = (Brush)FindResource("StateRedBrush");
        var green = (Brush)FindResource("StateGreenBrush");

        StatusDot.Fill = peak ? red : green;
        StatusTitle.Foreground = peak ? red : green;
        StatusTitle.Text = peak ? "梁文锋时段" : "梁文谷时段";
        StatusPrice.Text = peak ? "当前为高峰价格" : "当前价格为高峰时段价格的一半";

        var next = PeakHours.NextSwitch(now);
        StatusCountdown.Text = next is { } t
            ? $"现在 {now:HH:mm}，距切换到{(peak ? "梁文谷" : "梁文锋")}时段还有 {FormatSpan(t - now)}"
            : $"现在 {now:HH:mm}";

        if (_timelineDay != now.Date)
        {
            _timelineDay = now.Date;
            BuildTimeline(now.Date);
        }
        UpdateCurrentHour(now.Hour);
    }

    private void BuildTimeline(DateTime day)
    {
        TimelineGrid.Children.Clear();
        _cells.Clear();

        bool holiday = PeakHours.IsHoliday(day);
        bool workday = !holiday && !PeakHours.IsWeekend(day.DayOfWeek);
        string tag = holiday ? "法定节假日" : PeakHours.IsWeekend(day.DayOfWeek) ? "周末" : "";
        TimelineTitle.Text = tag.Length > 0 ? $"今日时段分布（{tag} · 全天梁文谷时段）" : "今日时段分布";

        for (int h = 0; h < 24; h++)
        {
            bool peakSlot = workday && ((h >= 9 && h < 12) || (h >= 14 && h < 18));
            var cell = new Border
            {
                Background = (Brush)FindResource(peakSlot ? "StateRedBrush" : "StateGreenBrush"),
                CornerRadius = h == 0 ? new CornerRadius(4, 0, 0, 4)
                    : h == 23 ? new CornerRadius(0, 4, 4, 0) : new CornerRadius(0),
                Margin = new Thickness(h == 0 ? 0 : 0.5, 0, h == 23 ? 0 : 0.5, 0),
                Tag = peakSlot,
                ToolTip = $"{h:00}:00-{h + 1:00}:00 · {(peakSlot ? "梁文锋时段" : "梁文谷时段")}",
            };
            TimelineGrid.Children.Add(cell);
            _cells.Add(cell);
        }
    }

    private void UpdateCurrentHour(int hour)
    {
        for (int i = 0; i < _cells.Count; i++)
        {
            bool current = i == hour;
            _cells[i].BorderThickness = current ? new Thickness(1.5) : new Thickness(0);
            _cells[i].BorderBrush = Brushes.White;
            _cells[i].Opacity = current ? 1.0 : (bool)_cells[i].Tag ? 0.86 : 0.8;
        }
    }

    private static string FormatSpan(TimeSpan span)
    {
        if (span.TotalMinutes < 1) return "即将切换";
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays} 天 {span.Hours} 小时";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours} 小时 {span.Minutes} 分";
        return $"{(int)Math.Ceiling(span.TotalMinutes)} 分钟";
    }
}