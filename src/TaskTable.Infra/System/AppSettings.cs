using System.Text.Json;

namespace TaskTable.Infra.System;

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
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
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