using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskTable.Infra.System;

namespace TaskTable.App.ViewModels;

/// <summary>设置窗口 VM：番茄钟 / 健康提醒 / 系统。</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly AutostartService _autostart;

    [ObservableProperty] private bool _pomodoroEnabled;
    [ObservableProperty] private string _pomodoroWorkText = "";
    [ObservableProperty] private string _pomodoroBreakText = "";
    [ObservableProperty] private bool _pomodoroAutoStartBreak;

    /// <summary>任务联动模式（0 关闭 / 1 仅启动 / 2 启动和停止 / 3 完整联动）。</summary>
    [ObservableProperty] private int _pomodoroLinkIndex;

    [ObservableProperty] private bool _showTooltips;

    partial void OnPomodoroLinkIndexChanged(int value) => OnPropertyChanged(nameof(PomodoroLinkHint));

    /// <summary>当前联动模式的说明文案。</summary>
    public string PomodoroLinkHint => PomodoroLinkIndex switch
    {
        0 => "关闭：任务计时与番茄钟互不影响",
        1 => "仅启动：开始任务计时时，番茄钟同步开始专注",
        2 => "启动和停止：开始任务时同步开始专注，停止任务时番茄钟归零；番茄钟结束仅提醒",
        _ => "完整联动：在「启动和停止」的基础上，番茄钟专注结束会自动停止任务计时并结算；手动重置番茄钟也会一并停止任务计时"
    };

    [ObservableProperty] private bool _sedentaryEnabled;
    [ObservableProperty] private string _sedentaryMinutesText = "";
    [ObservableProperty] private bool _waterEnabled;
    [ObservableProperty] private string _waterMinutesText = "";

    [ObservableProperty] private bool _autostartEnabled;

    public string InboxPath => AppPaths.InboxDir;

    // ── 关于软件 ──
    public string AppVersionText => AppInfo.VersionText;
    public string AuthorText => AppInfo.Author;
    public bool HasTutorialUrl => AppInfo.HasTutorialUrl;
    public string TutorialText => AppInfo.HasTutorialUrl ? "观看视频教程" : "视频教程制作中，敬请期待";

    public event Action? Saved;
    public event Action? Cancelled;

    public SettingsViewModel(AppSettings settings, AutostartService autostart)
    {
        _settings = settings;
        _autostart = autostart;

        PomodoroEnabled = settings.PomodoroEnabled;
        PomodoroWorkText = settings.PomodoroWorkMinutes.ToString();
        PomodoroBreakText = settings.PomodoroBreakMinutes.ToString();
        PomodoroAutoStartBreak = settings.PomodoroAutoStartBreak;
        PomodoroLinkIndex = Math.Clamp((int)settings.PomodoroLink, 0, 3);
        ShowTooltips = settings.ShowTooltips;
        SedentaryEnabled = settings.SedentaryEnabled;
        SedentaryMinutesText = settings.SedentaryMinutes.ToString();
        WaterEnabled = settings.WaterEnabled;
        WaterMinutesText = settings.WaterMinutes.ToString();
        AutostartEnabled = autostart.IsEnabled;
    }

    [RelayCommand]
    private void Save()
    {
        _settings.PomodoroEnabled = PomodoroEnabled;
        _settings.PomodoroWorkMinutes = ParseMinutes(PomodoroWorkText, _settings.PomodoroWorkMinutes, 1, 240);
        _settings.PomodoroBreakMinutes = ParseMinutes(PomodoroBreakText, _settings.PomodoroBreakMinutes, 1, 240);
        _settings.PomodoroAutoStartBreak = PomodoroAutoStartBreak;
        _settings.PomodoroLink = (TaskLinkMode)Math.Clamp(PomodoroLinkIndex, 0, 3);
        _settings.ShowTooltips = ShowTooltips;
        _settings.SedentaryEnabled = SedentaryEnabled;
        _settings.SedentaryMinutes = ParseMinutes(SedentaryMinutesText, _settings.SedentaryMinutes, 1, 240);
        _settings.WaterEnabled = WaterEnabled;
        _settings.WaterMinutes = ParseMinutes(WaterMinutesText, _settings.WaterMinutes, 1, 240);
        _settings.Save();

        try { _autostart.SetEnabled(AutostartEnabled); }
        catch (Exception ex) { AppLog.Warn($"自启动设置失败: {ex.Message}"); }

        Saved?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();

    private static int ParseMinutes(string? text, int fallback, int min, int max)
        => int.TryParse(text?.Trim(), out int value) ? Math.Clamp(value, min, max) : fallback;
}