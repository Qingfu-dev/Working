using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskTable.Core.Models;

namespace TaskTable.App.ViewModels;

/// <summary>任务列表项 VM。状态变更通过回调交给 MainViewModel 落库并刷新。</summary>
public sealed partial class TaskItemViewModel : ObservableObject
{
    public TaskItem Model { get; }

    /// <summary>完成状态切换回调（参数：本项、新状态）。</summary>
    public Action<TaskItemViewModel, bool>? OnToggleDone { get; set; }
    public Action<TaskItemViewModel>? OnStartStopTimer { get; set; }
    public Action<TaskItemViewModel>? OnDelete { get; set; }

    public TaskItemViewModel(TaskItem model) => Model = model;

    public string Id => Model.Id;
    public string Title => Model.Title;
    public bool IsAi => Model.IsFromAi;

    public bool IsDone
    {
        get => Model.IsDone;
        set
        {
            if (Model.IsDone == value) return;
            OnToggleDone?.Invoke(this, value);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimerButtonIcon))]
    [NotifyPropertyChangedFor(nameof(TimerButtonTip))]
    private bool _isTimerRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTime))]
    private string _timeText = "";

    public bool HasTime => !string.IsNullOrEmpty(TimeText);

    public string TimerButtonIcon => IsTimerRunning
        ? "pack://application:,,,/Assets/Icons/stop.png"
        : "pack://application:,,,/Assets/Icons/play.png";
    public string TimerButtonTip => IsTimerRunning ? "停止计时" : "开始计时";

    public string PriorityText => Model.Priority switch { 1 => "高", 3 => "低", _ => "中" };

    public Brush PriorityBrush => Model.Priority switch
    {
        1 => new SolidColorBrush(Color.FromRgb(0xE8, 0x46, 0x3A)),
        3 => new SolidColorBrush(Color.FromRgb(0x52, 0x52, 0x5B)),
        _ => new SolidColorBrush(Color.FromRgb(0xEF, 0xAA, 0x17))
    };

    public void RefreshDoneState()
    {
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(TitleOpacity));
    }

    public double TitleOpacity => Model.IsDone ? 0.45 : 1.0;
}
