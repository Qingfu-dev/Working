using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TaskTable.App.Dashboard;

namespace TaskTable.App.Views;

/// <summary>
/// 模块浮窗：从主窗口拖出的模块独立成窗，可自由移动/缩放；标题栏图钉＝置顶；
/// 拖回主窗口的模块卡片上＝自动吸回停靠；不设关闭按钮，模块只会在停靠与浮窗之间流转。
/// </summary>
public partial class FloatingModuleWindow : Window
{
    private readonly DockHost _owner;
    private readonly string _moduleId;
    private readonly DispatcherTimer _saveDebounce = new() { Interval = TimeSpan.FromMilliseconds(400) };

    private bool _dragActive;
    private bool _dragMoved;
    private Point _dragOffset;
    private bool _silentClose;

    public FloatingModuleWindow(string moduleId, string title, FrameworkElement view, DockHost owner, bool topmost)
    {
        InitializeComponent();
        _moduleId = moduleId;
        _owner = owner;
        Title = $"任务表 · {title}";
        TitleText.Text = title;
        DataContext = owner.DataContext;
        ModuleHost.Content = view;
        Topmost = topmost;
        UpdatePinVisual();

        // 标题栏右键：关闭该窗口 / 添加窗口（与停靠卡片共用同一菜单）
        Header.ContextMenu = owner.BuildModuleMenu(moduleId);
        AutomationProperties.SetName(Header, $"拖动条·{title}");
        AutomationProperties.SetAutomationId(Header, $"float-header-{moduleId}");
        AutomationProperties.SetAutomationId(this, $"float-window-{moduleId}");

        _saveDebounce.Tick += (_, _) =>
        {
            _saveDebounce.Stop();
            _owner.NotifyFloatChanged();
        };
        LocationChanged += (_, _) =>
        {
            if (!_dragActive) QueueSave();   // 拖动过程中不写盘，释放后统一保存
        };
        SizeChanged += (_, _) => QueueSave();
        Closing += OnClosing;
    }

    /// <summary>供 DockHost 快照到 layout.json。</summary>
    public FloatState ToState() => new()
    {
        Module = _moduleId,
        Left = Left,
        Top = Top,
        Width = Width,
        Height = Height,
        Topmost = Topmost,
    };

    /// <summary>吸回停靠 / 重置布局时静默关闭：不再补写本浮窗状态。</summary>
    public void PrepareCloseSilently()
    {
        _silentClose = true;
        _saveDebounce.Stop();
        ModuleHost.Content = null;
        try { Close(); }
        catch (InvalidOperationException) { /* 已在关闭流程中，忽略 */ }
    }

    private void QueueSave()
    {
        if (_silentClose) return;
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_silentClose) return;
        if (Dispatcher.HasShutdownStarted)
        {
            // 应用退出：记录最终位置后正常关闭
            _saveDebounce.Stop();
            _owner.SaveFloatStateNow();
            return;
        }
        // 其他关闭途径（如 Alt+F4）：模块不凭空消失，收回主窗口（延迟到 Closing 结束后再关，避免重入）
        e.Cancel = true;
        Dispatcher.BeginInvoke(() => _owner.DockBackHome(_moduleId));
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindButton(e.OriginalSource as DependencyObject) is not null) return;
        _dragActive = true;
        _dragMoved = false;
        _dragOffset = e.GetPosition(this);
        Header.CaptureMouse();
    }

    private void Header_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragActive) return;
        var screenPx = CursorScreenPoint();
        var dip = ToDip(screenPx);
        Left = dip.X - _dragOffset.X;
        Top = dip.Y - _dragOffset.Y;
        _dragMoved = true;
        _owner.UpdateExternalPreview(screenPx);
    }

    private void Header_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragActive) return;
        // 释放捕获 → LostMouseCapture 内统一结算落点（与 WPF 自动释放的时序无关）
        if (Header.IsMouseCaptured) Header.ReleaseMouseCapture();
        else EndHeaderDrag();
    }

    private void Header_LostMouseCapture(object sender, MouseEventArgs e) => EndHeaderDrag();

    /// <summary>拖动结算：落在主窗口卡片上＝吸回停靠，否则记录新位置。</summary>
    private void EndHeaderDrag()
    {
        if (!_dragActive) return;
        _dragActive = false;
        if (!_dragMoved) return;                                              // 只是点击标题栏，不结算
        if (Mouse.LeftButton == MouseButtonState.Pressed) return;             // 捕获被其他操作抢走，不视为释放

        if (_owner.TryDockBack(_moduleId, CursorScreenPoint())) return;       // 已吸回，窗口由 DockHost 关闭
        _owner.HideExternalPreview();
        _saveDebounce.Stop();
        _owner.SaveFloatStateNow();
    }

    /// <summary>屏幕物理像素 → DIP（与 Window.Left/Top 同一坐标系）。</summary>
    private Point ToDip(Point screenPx)
    {
        var matrix = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;
        return matrix is { } m ? m.Transform(screenPx) : screenPx;
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdatePinVisual();
        _saveDebounce.Stop();
        _owner.SaveFloatStateNow();
    }

    private void UpdatePinVisual()
    {
        bool pinned = Topmost;
        PinPath.Fill = pinned ? (Brush)FindResource("BrandBrush") : Brushes.Transparent;
        PinPath.Stroke = pinned ? (Brush)FindResource("BrandBrush") : (Brush)FindResource("TextMutedBrush");
        PinButton.ToolTip = pinned ? "已置顶（点击取消）" : "置顶显示";
    }

    private static ButtonBase? FindButton(DependencyObject? src)
    {
        while (src is not null)
        {
            if (src is ButtonBase button) return button;
            src = src is Visual ? VisualTreeHelper.GetParent(src) : LogicalTreeHelper.GetParent(src);
        }
        return null;
    }

    /// <summary>光标屏幕坐标（物理像素，与 WPF PointToScreen 同一坐标系）。</summary>
    private static Point CursorScreenPoint()
        => GetCursorPos(out var p) ? new Point(p.X, p.Y) : default;

    [StructLayout(LayoutKind.Sequential)]
    private struct WinPoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out WinPoint p);
}