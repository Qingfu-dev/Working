using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using TaskTable.Infra.System;

namespace TaskTable.App.Services;

/// <summary>托盘常驻：图标 + 右键菜单（打开/快速添加/收件箱/自启动/退出）。</summary>
public sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _tray;
    private readonly AutostartService _autostart;

    public TrayService(AutostartService autostart, string? hotkeyName,
                       Action showMain, Action quickAdd, Action openSettings, Action openHeatmap, Action exit)
    {
        _autostart = autostart;

        _tray = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
            ToolTipText = "任务表"
        };

        var menu = new ContextMenu();

        var open = new MenuItem { Header = "打开主界面" };
        open.Click += (_, _) => showMain();
        menu.Items.Add(open);

        var add = new MenuItem { Header = hotkeyName is null ? "快速添加" : $"快速添加（{hotkeyName}）" };
        add.Click += (_, _) => quickAdd();
        menu.Items.Add(add);

        var settingsItem = new MenuItem { Header = "设置…" };
        settingsItem.Click += (_, _) => openSettings();
        menu.Items.Add(settingsItem);

        var heatmapItem = new MenuItem { Header = "专注热力图" };
        heatmapItem.Click += (_, _) => openHeatmap();
        menu.Items.Add(heatmapItem);

        var inbox = new MenuItem { Header = "打开 AI 收件箱目录" };
        inbox.Click += (_, _) =>
        {
            AppPaths.EnsureAll();
            Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.InboxDir) { UseShellExecute = true });
        };
        menu.Items.Add(inbox);

        menu.Items.Add(new Separator());

        var autostartItem = new MenuItem { Header = "开机自启动", IsCheckable = true, IsChecked = _autostart.IsEnabled };
        autostartItem.Click += (_, _) => _autostart.SetEnabled(autostartItem.IsChecked);
        menu.Items.Add(autostartItem);
        menu.Opened += (_, _) => autostartItem.IsChecked = _autostart.IsEnabled;

        menu.Items.Add(new Separator());

        var quit = new MenuItem { Header = "退出" };
        quit.Click += (_, _) => exit();
        menu.Items.Add(quit);

        _tray.ContextMenu = menu;
        _tray.TrayMouseDoubleClick += (_, _) => showMain();
    }

    public void ShowBalloon(string title, string message)
        => _tray.ShowBalloonTip(title, message, BalloonIcon.Info);

    public void Dispose() => _tray.Dispose();
}
