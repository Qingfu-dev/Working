using System.Windows;
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

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
        _watcher = new InboxWatcher(taskRepo);
        _watcher.TasksImported += n => Dispatcher.Invoke(() =>
        {
            _mainVm.RefreshAll();
            _tray?.ShowBalloon("AI 任务入列", $"{n} 条任务已加入列表");
        });
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

    /// <summary>统一通知出口：写日志 + 托盘气泡。</summary>
    private void Notify(string title, string message)
    {
        AppLog.Info($"通知: {title}｜{message}");
        _tray?.ShowBalloon(title, message);
    }

    private void ShowSettings()
    {
        var vm = new SettingsViewModel(_settings, _autostart!);
        vm.Saved += () =>
        {
            _mainVm?.OnSettingsSaved();
            Notify("任务表", "设置已保存");
        };
        var window = new SettingsWindow(vm);
        window.Show();
        window.Activate();
    }

    private void ShowHeatmap()
    {
        if (_timeRepo is null) return;
        var vm = new HeatmapViewModel(_timeRepo, DateOnly.FromDateTime(DateTime.Today));
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
