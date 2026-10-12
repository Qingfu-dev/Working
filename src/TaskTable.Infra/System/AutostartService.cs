using Microsoft.Win32;

namespace TaskTable.Infra.System;

/// <summary>开机自启动：HKCU\...\Run 注册表键，免管理员权限，绿色发布可用。</summary>
public sealed class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TaskTable";
    private readonly string _exePath;

    public AutostartService(string exePath) => _exePath = exePath;

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string s
                   && s.Contains(_exePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(ValueName, $"\"{_exePath}\" --silent");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        AppLog.Info($"开机自启动: {(enabled ? "开启" : "关闭")}");
    }
}
