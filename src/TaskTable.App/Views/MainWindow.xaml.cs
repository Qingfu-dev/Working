using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using TaskTable.App.ViewModels;

namespace TaskTable.App.Views;

public partial class MainWindow : Window
{
    private const int WM_GETMINMAXINFO = 0x0024;
    private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        TitleBar.MouseLeftButtonDown += TitleBar_MouseDown;
        StatusStrip.MouseLeftButtonDown += TitleBar_MouseDown;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Dock.CancelDrag();
        };
        StateChanged += OnStateChanged;

        // DataContext 就绪后恢复上次退出时处于浮窗状态的模块
        Dock.RestoreFloats();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
    }

    /// <summary>无边框窗口最大化时限制在工作区内（不覆盖任务栏）。</summary>
    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            ApplyWorkAreaMaxSize(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static void ApplyWorkAreaMaxSize(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                var work = info.rcWork;
                var full = info.rcMonitor;
                mmi.ptMaxPosition.X = work.Left - full.Left;
                mmi.ptMaxPosition.Y = work.Top - full.Top;
                mmi.ptMaxSize.X = work.Right - work.Left;
                mmi.ptMaxSize.Y = work.Bottom - work.Top;
                mmi.ptMaxTrackSize.X = work.Right - work.Left;
                mmi.ptMaxTrackSize.Y = work.Bottom - work.Top;
            }
        }
        Marshal.StructureToPtr(mmi, lParam, true);
    }

    /// <summary>自定义窗体拖动：双击切换最大化；仅从空白标题区域触发，按钮自行处理点击。</summary>
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }
        try { DragMove(); }
        catch (InvalidOperationException) { /* 鼠标已释放，忽略 */ }
    }

    private void MaxRestore_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        bool maximized = WindowState == WindowState.Maximized;
        // 最大化时贴满工作区：去外距、直角；还原时恢复圆角悬浮
        RootCard.Margin = maximized ? new Thickness(0) : new Thickness(6);
        RootCard.CornerRadius = maximized ? new CornerRadius(0) : (CornerRadius)FindResource("RadiusCard");
        RootCard.Effect = maximized ? null : (System.Windows.Media.Effects.Effect)new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 18, ShadowDepth = 2, Opacity = 0.18, Color = System.Windows.Media.Colors.Black
        };
        MaxGlyph.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreGlyph.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
        MaxButton.ToolTip = maximized ? "还原" : "最大化";
        System.Windows.Automation.AutomationProperties.SetName(MaxButton, maximized ? "还原" : "最大化");
    }

    private void ResetLayout_Click(object sender, RoutedEventArgs e) => Dock.ResetLayout();

    public void ShowAndActivate()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => Hide();

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    // ═══ Win32 互操作 ═══

    [StructLayout(LayoutKind.Sequential)]
    private struct WinPoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public WinPoint ptReserved;
        public WinPoint ptMaxSize;
        public WinPoint ptMaxPosition;
        public WinPoint ptMinTrackSize;
        public WinPoint ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public WinRect rcMonitor;
        public WinRect rcWork;
        public int dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
}