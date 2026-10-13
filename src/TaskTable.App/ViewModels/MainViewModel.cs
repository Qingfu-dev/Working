using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskTable.Core.Models;
using TaskTable.Core.Services;
using TaskTable.Infra.Data;
using TaskTable.Infra.System;
using TaskStatus = TaskTable.Core.Models.TaskStatus;

namespace TaskTable.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly TaskRepository _tasks;
    private readonly TimeEntryRepository _times;
    private readonly ExpenseRepository _expenses;
    private readonly AppSettings _settings;
    private readonly Action<string, string> _notify;
    private readonly DispatcherTimer _ticker;
    private TimeEntry? _running;

    /// <summary>会话计时起点：本次软件启动时刻（开机自启动时即近似为开机时刻）。</summary>
    private readonly DateTime _sessionStart = DateTime.Now;

    public ObservableCollection<DayCellViewModel> Cells { get; } = new();
    public ObservableCollection<TaskItemViewModel> DayTasks { get; } = new();
    public ObservableCollection<ExpenseItemViewModel> DayExpenses { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthTitle))]
    private DateOnly _currentMonth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedDateTitle))]
    private DateOnly _selectedDate;

    [ObservableProperty]
    private DayCellViewModel? _selectedCell;

    partial void OnSelectedCellChanged(DayCellViewModel? value)
    {
        if (value is not null && value.Date != SelectedDate)
            SelectDate(value.Date);
    }

    [ObservableProperty]
    private string _todaySummary = "";

    /// <summary>本次运行时长（会话计时），每秒刷新。</summary>
    [ObservableProperty]
    private string _sessionText = "";

    /// <summary>实际生效的全局快捷键名（由 App 在注册后写入）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FooterHint))]
    [NotifyPropertyChangedFor(nameof(AddButtonTip))]
    private string _hotkeyName = "Alt+Space";

    public string FooterHint => $"双击日期格快速添加 · {HotkeyName} 随时发布";
    public string AddButtonTip => $"快速添加（{HotkeyName}）";

    /// <summary>选中日期的累计计时（如"累计 1h30"），不足 1 分钟不显示。</summary>
    [ObservableProperty]
    private string _dayTimeText = "";

    /// <summary>设置窗口请求，由 App 层打开。</summary>
    public event Action? SettingsRequested;

    /// <summary>专注热力图窗口请求，由 App 层打开。</summary>
    public event Action? HeatmapRequested;

    /// <summary>当日消费合计（无记录为空）。</summary>
    [ObservableProperty]
    private string _expenseTotalText = "";

    /// <summary>记账金额输入框内容。</summary>
    [ObservableProperty]
    private string _newExpenseAmount = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRunningTimer))]
    [NotifyPropertyChangedFor(nameof(RunningTimerText))]
    private string _runningTaskTitle = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RunningTimerText))]
    private string _runningElapsed = "";

    public bool HasRunningTimer => _running is not null;
    public string RunningTimerText => $"{RunningTaskTitle} · {RunningElapsed}";

    /// <summary>快速添加请求（参数：默认日期），由 App 层开窗。</summary>
    public event Action<DateOnly>? QuickAddRequested;

    public string MonthTitle => $"{CurrentMonth.Year}年{CurrentMonth.Month}月";
    public string SelectedDateTitle => $"{SelectedDate.Month}月{SelectedDate.Day}日 任务";

    public MainViewModel(TaskRepository tasks, TimeEntryRepository times, ExpenseRepository expenses,
                         AppSettings settings, Action<string, string> notify)
    {
        _tasks = tasks;
        _times = times;
        _expenses = expenses;
        _settings = settings;
        _notify = notify;
        var today = DateOnly.FromDateTime(DateTime.Today);
        _currentMonth = new DateOnly(today.Year, today.Month, 1);
        _selectedDate = today;

        _ticker = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _ticker.Tick += (_, _) => TickTimer();
        _ticker.Start();
        UpdateSessionText();

        // 崩溃恢复：若上次退出时计时仍在跑，按时间戳恢复显示
        _running = _times.GetRunning();
        if (_running is not null)
        {
            var task = _tasks.GetByDate(today).FirstOrDefault(t => t.Id == _running.TaskId);
            RunningTaskTitle = task?.Title ?? "（任务）";
        }

        RefreshAll();
    }

    // ═══ 日历 ═══

    [RelayCommand]
    private void PrevMonth() { CurrentMonth = CurrentMonth.AddMonths(-1); RefreshCalendar(); }

    [RelayCommand]
    private void NextMonth() { CurrentMonth = CurrentMonth.AddMonths(1); RefreshCalendar(); }

    [RelayCommand]
    private void GoToday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        CurrentMonth = new DateOnly(today.Year, today.Month, 1);
        RefreshCalendar();
        SelectDate(today);
    }

    [RelayCommand]
    private void SelectCell(DayCellViewModel? cell)
    {
        if (cell is null) return;
        SelectedCell = cell;
        SelectDate(cell.Date);
    }

    private void SelectDate(DateOnly date)
    {
        SelectedDate = date;
        RefreshDayTasks();
    }

    public void RefreshAll()
    {
        RefreshCalendar();
        RefreshDayTasks();
        RefreshTodaySummary();
    }

    private void RefreshCalendar()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var summary = _tasks.GetMonthSummary(CurrentMonth.Year, CurrentMonth.Month);
        var timeSummary = _times.GetMonthTimeSummary(CurrentMonth.Year, CurrentMonth.Month);

        // 周一开头；补齐 6 行 × 7 列
        var first = CurrentMonth;
        int offset = ((int)first.DayOfWeek + 6) % 7;
        var gridStart = first.AddDays(-offset);

        DayCellViewModel? reselect = null;
        Cells.Clear();
        for (int i = 0; i < 42; i++)
        {
            var date = gridStart.AddDays(i);
            var cell = new DayCellViewModel { Date = date, IsCurrentMonth = date.Month == CurrentMonth.Month };
            if (summary.TryGetValue(date, out var s))
                cell.SetVisual(DayStateCalculator.Calculate(date, today, s.Total, s.Done));
            else
                cell.SetVisual(DayStateCalculator.Calculate(date, today, 0, 0));
            if (timeSummary.TryGetValue(date, out long sec))
                cell.TimeText = DurationFormatter.Compact(sec);
            Cells.Add(cell);
            if (date == SelectedDate) reselect = cell;
        }
        SelectedCell = reselect;
    }

    // ═══ 当日任务 ═══

    private void RefreshDayTasks()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tasks = _tasks.GetByDate(SelectedDate);
        DayTasks.Clear();
        foreach (var t in tasks)
        {
            var vm = new TaskItemViewModel(t)
            {
                IsTimerRunning = _running?.TaskId == t.Id,
                OnToggleDone = OnToggleDone,
                OnStartStopTimer = OnStartStopTimer,
                OnDelete = OnDeleteTask
            };
            long sec = _times.GetSecondsForTaskOnDate(t.Id, today);
            vm.TimeText = FormatSeconds(sec);
            DayTasks.Add(vm);
        }
        RefreshTodaySummary();
        RefreshDayTimeText();
        RefreshDayExpenses();
    }

    private void RefreshTodaySummary()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tasks = _tasks.GetByDate(today);
        int done = tasks.Count(t => t.IsDone);
        TodaySummary = tasks.Count == 0 ? "今日暂无任务" : $"今日完成 {done} / {tasks.Count}";
    }

    // ═══ 任务操作 ═══

    [RelayCommand]
    private void AddTask() => QuickAddRequested?.Invoke(SelectedDate);

    public void AddTaskDirect(TaskItem task)
    {
        _tasks.Insert(task);
        RefreshAll();
    }

    private void OnToggleDone(TaskItemViewModel vm, bool isDone)
    {
        _tasks.UpdateStatus(vm.Id, isDone ? TaskStatus.Done : TaskStatus.Todo);
        vm.Model.Status = isDone ? TaskStatus.Done : TaskStatus.Todo;
        vm.Model.CompletedAt = isDone ? DateTime.Now : null;
        vm.RefreshDoneState();
        RefreshCalendar();
        RefreshTodaySummary();
    }

    private void OnDeleteTask(TaskItemViewModel vm)
    {
        _tasks.Delete(vm.Id);
        if (_running?.TaskId == vm.Id) StopCurrentTimer();
        RefreshAll();
    }

    // ═══ 计时 ═══

    private void OnStartStopTimer(TaskItemViewModel vm)
    {
        if (_running?.TaskId == vm.Id)
        {
            StopCurrentTimer();
            SyncPomodoroOnTaskStop();
        }
        else
        {
            StopCurrentTimer();
            _running = _times.Start(vm.Id);
            RunningTaskTitle = vm.Title;
            SetRunningFlags(vm.Id);
            SyncPomodoroOnTaskStart();
        }
        OnPropertyChanged(nameof(HasRunningTimer));
        OnPropertyChanged(nameof(RunningTimerText));
    }

    private void StopCurrentTimer()
    {
        if (_running is null) return;
        _times.Stop(_running.Id);
        var stoppedTaskId = _running.TaskId;
        _running = null;
        RunningTaskTitle = "";
        RunningElapsed = "";
        SetRunningFlags(null);
        // 结算后刷新该任务当日累计
        var vm = DayTasks.FirstOrDefault(t => t.Id == stoppedTaskId);
        if (vm is not null)
            vm.TimeText = FormatSeconds(_times.GetSecondsForTaskOnDate(stoppedTaskId, DateOnly.FromDateTime(DateTime.Today)));
        RefreshDayTimeText();
        var cell = Cells.FirstOrDefault(c => c.Date == SelectedDate);
        if (cell is not null)
            cell.TimeText = DurationFormatter.Compact(_times.GetSecondsForDate(SelectedDate));
    }

    private void SetRunningFlags(string? runningTaskId)
    {
        foreach (var t in DayTasks)
            t.IsTimerRunning = t.Id == runningTaskId;
    }

    private void TickTimer()
    {
        UpdateSessionText();
        TickPomodoro();
        TickReminders();
        if (_running is null) return;
        RunningElapsed = FormatSeconds((long)_running.EffectiveDuration.TotalSeconds);
        var runDate = DateOnly.FromDateTime(_running.StartTime);
        var vm = DayTasks.FirstOrDefault(t => t.Id == _running.TaskId);
        if (vm is not null)
            vm.TimeText = FormatSeconds(_times.GetSecondsForTaskOnDate(_running.TaskId, runDate));
        RefreshLiveTotals(runDate);
    }

    public void StopTimerOnExit() => StopCurrentTimer();

    private void UpdateSessionText() => SessionText = $"本次运行 {FormatSpan(DateTime.Now - _sessionStart)}";

    private static string FormatSpan(TimeSpan ts) =>
        ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes}:{ts.Seconds:D2}";

    private static string FormatSeconds(long sec) => DurationFormatter.Precise(sec);

    // ═══ 番茄钟 ═══

    [ObservableProperty] private PomodoroPhase _phase = PomodoroPhase.Idle;
    private int _pomodoroRemaining;
    private int _pomodoroTotal;

    private static readonly Brush RingWorkBrush = Frozen(0x0F, 0xDC, 0x78);
    private static readonly Brush RingPausedBrush = Frozen(0xA1, 0xA1, 0xAA);
    private static readonly Brush RingBreakBrush = Frozen(0xEF, 0xAA, 0x17);
    private static readonly Brush RingIdleBrush = Frozen(0xD4, 0xD4, 0xD8);

    private int WorkMinutes => Math.Max(1, _settings.PomodoroWorkMinutes);
    private int BreakMinutes => Math.Max(1, _settings.PomodoroBreakMinutes);

    /// <summary>设置中是否启用番茄钟（关闭时控制按钮禁用）。</summary>
    public bool PomodoroEnabled => _settings.PomodoroEnabled;

    /// <summary>进度环颜色：专注绿 / 暂停灰 / 休息橙 / 空闲浅灰。</summary>
    public Brush PomodoroPhaseBrush => Phase switch
    {
        PomodoroPhase.Working => RingWorkBrush,
        PomodoroPhase.Paused => RingPausedBrush,
        PomodoroPhase.Breaking => RingBreakBrush,
        _ => RingIdleBrush
    };

    /// <summary>进度环剩余比例（1=满环），空闲时显示满环。</summary>
    public double PomodoroRingFraction =>
        Phase is PomodoroPhase.Idle ? 1.0
        : _pomodoroTotal <= 0 ? 0
        : Math.Clamp(_pomodoroRemaining / (double)_pomodoroTotal, 0, 1);

    public string PomodoroBigText => Phase is PomodoroPhase.Idle ? $"{WorkMinutes:D2}:00" : MmSs(Math.Max(0, _pomodoroRemaining));

    public string PomodoroPhaseLabel => !_settings.PomodoroEnabled
        ? "已在设置中关闭"
        : Phase switch
        {
            PomodoroPhase.Working => "专注中",
            PomodoroPhase.Paused => "已暂停",
            PomodoroPhase.Breaking => "休息中",
            _ => "准备开始"
        };

    public string PomodoroMainText => Phase switch
    {
        PomodoroPhase.Working => "暂停",
        PomodoroPhase.Paused => "继续",
        PomodoroPhase.Breaking => "跳过休息",
        _ => "开始专注"
    };

    public bool PomodoroResetVisible => Phase != PomodoroPhase.Idle;

    public string PomodoroDurationHint => $"专注 {WorkMinutes} 分钟 · 休息 {BreakMinutes} 分钟";

    partial void OnPhaseChanged(PomodoroPhase value) => NotifyPomodoroDisplay();

    private void NotifyPomodoroDisplay()
    {
        OnPropertyChanged(nameof(PomodoroBigText));
        OnPropertyChanged(nameof(PomodoroRingFraction));
        OnPropertyChanged(nameof(PomodoroPhaseBrush));
        OnPropertyChanged(nameof(PomodoroPhaseLabel));
        OnPropertyChanged(nameof(PomodoroMainText));
        OnPropertyChanged(nameof(PomodoroResetVisible));
    }

    /// <summary>任务计时与番茄钟的联动模式（实时读取设置）。</summary>
    private TaskLinkMode LinkMode => _settings.PomodoroLink;

    /// <summary>联动：开始任务计时时，番茄钟同步进入专注。</summary>
    private void SyncPomodoroOnTaskStart()
    {
        if (LinkMode == TaskLinkMode.Off || !_settings.PomodoroEnabled) return;
        switch (Phase)
        {
            case PomodoroPhase.Idle:
            case PomodoroPhase.Breaking: // 休息中开始任务：跳过休息，开始新一轮专注
                _pomodoroTotal = _pomodoroRemaining = WorkMinutes * 60;
                Phase = PomodoroPhase.Working;
                break;
            case PomodoroPhase.Paused:   // 暂停中开始任务：继续上一轮专注
                Phase = PomodoroPhase.Working;
                break;
            case PomodoroPhase.Working:  // 已在专注中，不打断
                break;
        }
        NotifyPomodoroDisplay();
    }

    /// <summary>联动：停止任务计时时，番茄钟归零（「启动和停止」及以上模式）。</summary>
    private void SyncPomodoroOnTaskStop()
    {
        if (LinkMode is not (TaskLinkMode.StartStop or TaskLinkMode.Full)) return;
        if (Phase == PomodoroPhase.Idle) return;
        Phase = PomodoroPhase.Idle;
        _pomodoroRemaining = 0;
        _pomodoroTotal = 0;
        NotifyPomodoroDisplay();
    }

    /// <summary>主按钮：空闲→开始专注；专注→暂停；暂停→继续；休息→跳过休息。</summary>
    [RelayCommand]
    private void TogglePomodoro()
    {
        switch (Phase)
        {
            case PomodoroPhase.Idle:
                _pomodoroTotal = _pomodoroRemaining = WorkMinutes * 60;
                Phase = PomodoroPhase.Working;
                break;
            case PomodoroPhase.Working:
                Phase = PomodoroPhase.Paused;
                break;
            case PomodoroPhase.Paused:
                Phase = PomodoroPhase.Working;
                break;
            case PomodoroPhase.Breaking:
                Phase = PomodoroPhase.Idle;
                break;
        }
        NotifyPomodoroDisplay();
    }

    /// <summary>重置回空闲状态（完整联动模式下同步停止任务计时）。</summary>
    [RelayCommand]
    private void ResetPomodoro()
    {
        Phase = PomodoroPhase.Idle;
        _pomodoroRemaining = 0;
        _pomodoroTotal = 0;
        if (LinkMode == TaskLinkMode.Full) StopCurrentTimer();
        NotifyPomodoroDisplay();
    }

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke();

    private void TickPomodoro()
    {
        if (!_settings.PomodoroEnabled)
        {
            if (Phase != PomodoroPhase.Idle) Phase = PomodoroPhase.Idle;
            return;
        }
        if (Phase is PomodoroPhase.Idle or PomodoroPhase.Paused) return;

        _pomodoroRemaining--;
        if (_pomodoroRemaining <= 0) CompletePomodoroPhase();
        else NotifyPomodoroDisplay();
    }

    private void CompletePomodoroPhase()
    {
        if (Phase == PomodoroPhase.Working)
        {
            bool stopLinkedTask = LinkMode == TaskLinkMode.Full && _running is not null;
            if (stopLinkedTask) StopCurrentTimer();
            _notify("番茄钟", stopLinkedTask
                ? $"专注 {WorkMinutes} 分钟完成，已停止任务计时，休息一下吧"
                : $"专注 {WorkMinutes} 分钟完成，休息一下吧");
            if (_settings.PomodoroAutoStartBreak)
            {
                _pomodoroTotal = _pomodoroRemaining = BreakMinutes * 60;
                Phase = PomodoroPhase.Breaking;
            }
            else Phase = PomodoroPhase.Idle;
        }
        else if (Phase == PomodoroPhase.Breaking)
        {
            _notify("番茄钟", "休息结束，开始下一个专注吧");
            Phase = PomodoroPhase.Idle;
        }
        NotifyPomodoroDisplay();
    }

    private static string MmSs(int seconds) => $"{seconds / 60:D2}:{seconds % 60:D2}";

    /// <summary>设置保存后刷新番茄钟显示。</summary>
    public void OnSettingsSaved()
    {
        if (!_settings.PomodoroEnabled) Phase = PomodoroPhase.Idle;
        OnPropertyChanged(nameof(PomodoroEnabled));
        OnPropertyChanged(nameof(PomodoroDurationHint));
        NotifyPomodoroDisplay();
    }

    // ═══ 健康提醒 ═══

    private int _sedentarySeconds;
    private int _waterSeconds;

    private void TickReminders()
    {
        if (_settings.SedentaryEnabled)
        {
            _sedentarySeconds++;
            if (_sedentarySeconds >= Math.Max(1, _settings.SedentaryMinutes) * 60)
            {
                _sedentarySeconds = 0;
                _notify("久坐提醒", $"已连续坐了 {Math.Max(1, _settings.SedentaryMinutes)} 分钟，起来活动一下吧");
            }
        }
        else _sedentarySeconds = 0;

        if (_settings.WaterEnabled)
        {
            _waterSeconds++;
            if (_waterSeconds >= Math.Max(1, _settings.WaterMinutes) * 60)
            {
                _waterSeconds = 0;
                _notify("喝水提醒", "该喝水了，补充一下水分");
            }
        }
        else _waterSeconds = 0;
    }

    // ═══ 当日计时显示 ═══

    private void RefreshDayTimeText()
    {
        long sec = _times.GetSecondsForDate(SelectedDate);
        DayTimeText = sec >= 60 ? $"累计 {DurationFormatter.Compact(sec)}" : "";
    }

    /// <summary>
    /// 计时运行时每秒刷新「当日累计」显示。
    /// 日历格与面板用的是同一天的同一个值，因此每秒只查一次数据库（原来查了两次）。
    /// </summary>
    private void RefreshLiveTotals(DateOnly runDate)
    {
        long sec = _times.GetSecondsForDate(runDate);
        var cell = Cells.FirstOrDefault(c => c.Date == runDate);
        if (cell is not null)
            cell.TimeText = DurationFormatter.Compact(sec);
        if (SelectedDate == runDate)
            DayTimeText = sec >= 60 ? $"累计 {DurationFormatter.Compact(sec)}" : "";
    }

    // ═══ 消费记账 ═══

    [RelayCommand]
    private void OpenHeatmap() => HeatmapRequested?.Invoke();

    /// <summary>按类型记一笔消费（金额非法则忽略）。</summary>
    [RelayCommand]
    private void AddExpense(string category)
    {
        if (!decimal.TryParse(NewExpenseAmount?.Trim(), out decimal amount) || amount <= 0 || amount > 1_000_000)
            return;
        _expenses.Add(Math.Round(amount, 2), category, SelectedDate);
        NewExpenseAmount = "";
        RefreshDayExpenses();
    }

    private void OnDeleteExpense(ExpenseItemViewModel vm)
    {
        _expenses.Delete(vm.Id);
        RefreshDayExpenses();
    }

    private void RefreshDayExpenses()
    {
        var list = _expenses.GetByDate(SelectedDate);
        DayExpenses.Clear();
        foreach (var e in list)
            DayExpenses.Add(new ExpenseItemViewModel(e) { OnDelete = OnDeleteExpense });
        decimal total = list.Sum(e => e.Amount);
        ExpenseTotalText = list.Count > 0 ? $"合计 ¥{total:0.00}" : "";
    }

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public void Dispose() => _ticker.Stop();
}
