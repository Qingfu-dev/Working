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

    [ObservableProperty] private bool _sedentaryEnabled;
    [ObservableProperty] private string _sedentaryMinutesText = "";
    [ObservableProperty] private bool _waterEnabled;
    [ObservableProperty] private string _waterMinutesText = "";

    [ObservableProperty] private bool _autostartEnabled;

    public string InboxPath => AppPaths.InboxDir;

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