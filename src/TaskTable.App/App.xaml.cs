using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TaskTable.App.Services;
using TaskTable.App.ViewModels;
using TaskTable.App.Views;
using TaskTable.Infra.Data;
using TaskTable.Infra.Inbox;
using TaskTable.Infra.System;

namespace TaskTable.App;

public partial class App : Application
{
    private SingleInstanceGuard? _guard;
    private InboxWatcher? _watcher;
    private TrayService? _tray;
    private HotkeyService? _hotkey;
    private MainViewModel? _mainVm;
    private MainWindow? _mainWindow;
    private AppSettings _settings = new();
    private AutostartService? _autostart;
    private TimeEntryRepository? _timeRepo;
    private ExpenseRepository? _expenseRepo;

    /// <summary>收件箱导入合并窗口（毫秒）：静默这么久没有新导入就统一处理一次。</summary>
    private const int InboxCoalesceWindowMs = 500;

    /// <summary>持续不断有导入时的最大延迟（毫秒）：保证界面不会长时间不刷新。</summary>
    private const int InboxCoalesceMaxDelayMs = 2000;

    private DispatcherTimer? _inboxCoalesce;
    private int _inboxPendingCount;
    private DateTime _inboxLastFlush = DateTime.Now;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // ── 悬停提示开关：新建的窗口在加载时自动套用；提示将弹出时可再拦截一次 ──
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window window)
                    ToolTipService.SetIsEnabled(window, _settings.ShowTooltips);
            }));
        EventManager.RegisterClassHandler(typeof(FrameworkElement), ToolTipService.ToolTipOpeningEvent,
            new ToolTipEventHandler((_, args) =>
            {
                if (!_settings.ShowTooltips) args.Handled = true;
            }));

        AppPaths.EnsureAll();
        AppLog.Info("═══ 任务表启动 ═══");

        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("未处理异常", args.Exception);
            args.Handled = true;
            MessageBox.Show($"发生错误：{args.Exception.Message}\n详情见日志目录。", "任务表",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };

        // ── 单实例 ──
        _guard = new SingleInstanceGuard();
        if (!_guard.TryAcquire())
        {
            SingleInstanceGuard.NotifyFirstInstance();
            Shutdown();
            return;
        }
        _guard.ActivateRequested += () => Dispatcher.Invoke(ShowMain);
        _guard.StartListening();

        // ── 数据层 ──
        var db = new Database();
        var taskRepo = new TaskRepository(db);
        var timeRepo = new TimeEntryRepository(db);
        var expenseRepo = new ExpenseRepository(db);
        _timeRepo = timeRepo;
        _expenseRepo = expenseRepo;

        // ── 开机自启动（首启默认开启） ──
        var autostart = new AutostartService(Environment.ProcessPath
            ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "");
        _autostart = autostart;
        if (!autostart.IsEnabled) autostart.SetEnabled(true);

        // ── 用户设置 ──
        _settings = AppSettings.Load();

        // ── 主视图 ──
        _mainVm = new MainViewModel(taskRepo, timeRepo, expenseRepo, _settings, Notify);
        _mainVm.QuickAddRequested += date => Dispatcher.Invoke(() => ShowQuickAdd(date));
        _mainVm.SettingsRequested += () => Dispatcher.Invoke(ShowSettings);
        _mainVm.HeatmapRequested += () => Dispatcher.Invoke(ShowHeatmap);
        _mainWindow = new MainWindow(_mainVm);

        // ── AI 收件箱 ──
        // 批量导入（一次投放多个 json）时把「全量刷新 + 气泡」合并处理，
        // 否则每个文件都触发一次 RefreshAll，风暴场景会把 UI 线程反复占满。
        _inboxCoalesce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(InboxCoalesceWindowMs) };
        _inboxCoalesce.Tick += (_, _) =>
        {
            _inboxCoalesce!.Stop();
            FlushInboxImports();
        };
        _watcher = new InboxWatcher(taskRepo);
        _watcher.TasksImported += n => Dispatcher.Invoke(() => AccumulateInboxImports(n));
        _watcher.Start();

        // ── 托盘 + 全局热键 ──
        _hotkey = new HotkeyService(() => Dispatcher.Invoke(() => ShowQuickAdd(null)));
        _mainVm.HotkeyName = _hotkey.RegisteredHotkey ?? "托盘菜单";
        _tray = new TrayService(autostart, _hotkey.RegisteredHotkey, ShowMain, () => ShowQuickAdd(null), ShowSettings, ShowHeatmap, ExitApp);

        // ── 启动方式：--silent 静默进托盘 ──
        if (!e.Args.Contains("--silent", StringComparer.OrdinalIgnoreCase))
            ShowMain();

        AppLog.Info("启动完成");
    }

    private void ShowMain() => _mainWindow?.ShowAndActivate();

    /// <summary>把「显示悬停提示」开关应用到当前已打开的所有窗口。</summary>
    private void ApplyTooltipSetting()
    {
        foreach (Window window in Windows)
            ToolTipService.SetIsEnabled(window, _settings.ShowTooltips);
    }

    /// <summary>统一通知出口：写日志 + 托盘气泡。</summary>
    private void Notify(string title, string message)
    {
        AppLog.Info($"通知: {title}｜{message}");
        _tray?.ShowBalloon(title, message);
    }

    /// <summary>
    /// 收件箱导入累积：
    /// 上一批已处理完时，本次导入立即生效（与单个文件导入的原有手感一致）；
    /// 之后再到达的导入合并到合并窗口里统一处理，避免风暴场景反复全量刷新。
    /// </summary>
    private void AccumulateInboxImports(int count)
    {
        if (_inboxPendingCount == 0)
        {
            _inboxPendingCount = count;
            FlushInboxImports();
            ArmInboxCoalesce(InboxCoalesceWindowMs);
            return;
        }
        _inboxPendingCount += count;
        int heldMs = (int)(DateTime.Now - _inboxLastFlush).TotalMilliseconds;
        ArmInboxCoalesce(Math.Clamp(InboxCoalesceMaxDelayMs - heldMs, 1, InboxCoalesceWindowMs));
    }

    private void ArmInboxCoalesce(int waitMs)
    {
        _inboxCoalesce!.Stop();
        _inboxCoalesce.Interval = TimeSpan.FromMilliseconds(waitMs);
        _inboxCoalesce.Start();
    }

    private void FlushInboxImports()
    {
        int n = _inboxPendingCount;
        _inboxPendingCount = 0;
        _inboxLastFlush = DateTime.Now;
        if (n <= 0) return;
        _mainVm?.RefreshAll();
        _tray?.ShowBalloon("AI 任务入列", $"{n} 条任务已加入列表");
    }

    private void ShowSettings()
    {
        var vm = new SettingsViewModel(_settings, _autostart!);
        vm.Saved += () =>
        {
            _mainVm?.OnSettingsSaved();
            ApplyTooltipSetting();
            Notify("任务表", "设置已保存");
        };
        var window = new SettingsWindow(vm);
        window.Show();
        window.Activate();
    }

    private void ShowHeatmap()
    {
        if (_timeRepo is null || _expenseRepo is null) return;
        var vm = new HeatmapViewModel(_timeRepo, _expenseRepo, DateOnly.FromDateTime(DateTime.Today));
        var window = new HeatmapWindow(vm);
        window.Show();
        window.Activate();
    }

    private void ShowQuickAdd(DateOnly? date)
    {
        if (_mainVm is null) return;
        var vm = new QuickAddViewModel(date ?? _mainVm.SelectedDate);
        vm.Saved += task => _mainVm.AddTaskDirect(task);
        var window = new QuickAddWindow(vm);
        window.Show();
        window.Activate();
    }

    private void ExitApp()
    {
        AppLog.Info("用户退出");
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _watcher?.Dispose();
        _tray?.Dispose();
        _mainVm?.Dispose();
        _guard?.Dispose();
        AppLog.Info("═══ 任务表退出 ═══");
        base.OnExit(e);
    }
}
