using System.Text.Json;

namespace TaskTable.Infra.System;

/// <summary>任务计时与番茄钟的联动模式。</summary>
public enum TaskLinkMode
{
    /// <summary>关闭联动，两者互不影响。</summary>
    Off = 0,
    /// <summary>仅启动：开始任务计时时番茄钟同步开始专注。</summary>
    StartOnly = 1,
    /// <summary>启动和停止：开始任务同步开始专注；停止任务时番茄钟归零；番茄钟结束仅提醒。</summary>
    StartStop = 2,
    /// <summary>完整联动：番茄钟专注结束自动停止任务计时并结算；手动重置番茄钟也会停止任务计时。</summary>
    Full = 3
}

/// <summary>
/// 应用设置：%AppData%\TaskTable\settings.json。
/// 缺失字段自动使用默认值，读取/写入失败不阻塞启动。
/// </summary>
public sealed class AppSettings
{
    // ── 番茄钟 ──
    public bool PomodoroEnabled { get; set; } = true;
    public int PomodoroWorkMinutes { get; set; } = 25;
    public int PomodoroBreakMinutes { get; set; } = 5;
    public bool PomodoroAutoStartBreak { get; set; } = true;

    /// <summary>任务计时与番茄钟联动模式。</summary>
    public TaskLinkMode PomodoroLink { get; set; } = TaskLinkMode.Full;

    // ── 界面 ──
    /// <summary>是否显示悬停提示（鼠标悬停在按钮等控件上的说明气泡）。</summary>
    public bool ShowTooltips { get; set; } = true;

    // ── 健康提醒 ──
    public bool SedentaryEnabled { get; set; } = true;
    public int SedentaryMinutes { get; set; } = 45;
    public bool WaterEnabled { get; set; } = true;
    public int WaterMinutes { get; set; } = 60;

    private static string FilePath => Path.Combine(AppPaths.Root, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
                if (!Enum.IsDefined(settings.PomodoroLink)) settings.PomodoroLink = TaskLinkMode.Full;
                return settings;
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"设置读取失败，使用默认值: {ex.Message}");
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            AppPaths.EnsureAll();
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            AppLog.Info("设置已保存");
        }
        catch (Exception ex)
        {
            AppLog.Error("设置保存失败", ex);
        }
    }
}