using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskTable.Core.Services;

namespace TaskTable.App.ViewModels;

/// <summary>日历格子：日期 + 状态 + 预生成的填充画刷（含对角混色）。</summary>
public sealed partial class DayCellViewModel : ObservableObject
{
    private static readonly SolidColorBrush White = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)));
    private static readonly SolidColorBrush Gray = Freeze(new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB)));
    private static readonly SolidColorBrush Red = Freeze(new SolidColorBrush(Color.FromRgb(0xE8, 0x46, 0x3A)));
    private static readonly SolidColorBrush Green = Freeze(new SolidColorBrush(Color.FromRgb(0x1D, 0xC9, 0x81)));

    public required DateOnly Date { get; init; }
    public required bool IsCurrentMonth { get; init; }
    public bool IsToday => Date == DateOnly.FromDateTime(DateTime.Today);
    public DayVisual Visual { get; private set; }
    public Brush FillBrush { get; private set; } = White;

    public int DayNumber => Date.Day;
    public bool HasTasks => Visual.State != DayVisualState.Empty;

    /// <summary>当日累计计时（紧凑格式，如 45m / 1h30），无计时为空。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTime))]
    private string _timeText = "";

    public bool HasTime => !string.IsNullOrEmpty(TimeText);

    /// <summary>混色/红/绿底上用白字，白/灰底上用深字。</summary>
    public bool UseLightText => Visual.State is DayVisualState.Unfinished or DayVisualState.AllDone or DayVisualState.Mixed;

    public void SetVisual(DayVisual visual)
    {
        Visual = visual;
        FillBrush = visual.State switch
        {
            DayVisualState.Empty => White,
            DayVisualState.Planned => Gray,
            DayVisualState.Unfinished => Red,
            DayVisualState.AllDone => Green,
            DayVisualState.Mixed => CreateSplitBrush(visual.DoneRatio),
            _ => White
        };
    }

    /// <summary>红绿对角分割：左上红（未完成占比）→ 右下绿（完成占比），硬切换无渐变。</summary>
    private static Brush CreateSplitBrush(double doneRatio)
    {
        double cut = Math.Clamp(1.0 - doneRatio, 0.02, 0.98);
        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop(Red.Color, 0));
        brush.GradientStops.Add(new GradientStop(Red.Color, cut));
        brush.GradientStops.Add(new GradientStop(Green.Color, cut));
        brush.GradientStops.Add(new GradientStop(Green.Color, 1));
        return Freeze(brush);
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
