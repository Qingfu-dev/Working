using System.Runtime.InteropServices;
using System.Windows.Interop;
using TaskTable.Infra.System;

namespace TaskTable.App.Services;

/// <summary>全局快捷键：Alt+Space → 快速添加。挂在不可见后台消息窗口上（不进任务栏 / Alt+Tab / 任务管理器「应用」列表），窗口隐藏到托盘时依然有效。</summary>
public sealed class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int MOD_ALT = 0x0001;
    private const int MOD_CONTROL = 0x0002;
    private const int VK_SPACE = 0x20;
    private const int VK_Q = 0x51;
    private const int HOTKEY_ID = 0x7001;

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int SW_HIDE = 0;

    /// <summary>候选热键，按优先级回退（Alt+Space 常被 PowerToys 等占用）。</summary>
    private static readonly (int Mod, int Key, string Name)[] Candidates =
    [
        (MOD_ALT, VK_SPACE, "Alt+Space"),
        (MOD_ALT | MOD_CONTROL, VK_SPACE, "Ctrl+Alt+Space"),
        (MOD_ALT, VK_Q, "Alt+Q")
    ];

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private readonly HwndSource _source;
    private readonly Action _callback;

    public HotkeyService(Action callback)
    {
        _callback = callback;
        var parameters = new HwndSourceParameters("TaskTable.Hotkey")
        {
            Width = 0, Height = 0, PositionX = 0, PositionY = 0,
            WindowStyle = 0, // 不带 WS_VISIBLE：纯后台消息窗口，始终不可见
            ExtendedWindowStyle = WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
        };
        _source = new HwndSource(parameters);
        // 兜底：确保工具窗口样式与隐藏状态落实（避免以任何形式出现在窗口列表里）
        var exStyle = (long)GetWindowLongPtr(_source.Handle, GWL_EXSTYLE);
        SetWindowLongPtr(_source.Handle, GWL_EXSTYLE, (IntPtr)(exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
        ShowWindow(_source.Handle, SW_HIDE);
        _source.AddHook(WndProc);

        foreach (var (mod, key, name) in Candidates)
        {
            if (RegisterHotKey(_source.Handle, HOTKEY_ID, mod, key))
            {
                RegisteredHotkey = name;
                AppLog.Info($"全局快捷键已注册: {name}");
                return;
            }
        }
        RegisteredHotkey = null;
        AppLog.Warn("所有候选全局快捷键均注册失败");

    }

    /// <summary>实际生效的热键名（未注册成功时为 null）。</summary>
    public string? RegisteredHotkey { get; }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            handled = true;
            _callback();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterHotKey(_source.Handle, HOTKEY_ID);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
