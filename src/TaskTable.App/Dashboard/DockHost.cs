using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using TaskTable.App.Views.Modules;

namespace TaskTable.App.Dashboard;

/// <summary>
/// 模块停靠容器：每个模块是一张带标题栏的卡片；拖动标题栏可重排布局——
/// 拖到其他模块边缘＝分屏吸附（上下/左右），拖到中心＝交换位置；分隔条可调宽；标题栏双击＝模块最大化。
/// </summary>
public sealed class DockHost : Grid
{
    private enum DockSide { Left, Right, Top, Bottom, Center }

    private static readonly (string Id, string Title)[] Registry =
    {
        ("pomodoro", "番茄钟"),
        ("calendar", "日历"),
        ("tasks", "当日任务"),
        ("expenses", "消费记账"),
    };

    private static readonly Brush AccentBrush = Frozen(Color.FromRgb(0x0F, 0xDC, 0x78));
    private static readonly Brush AccentSoftBrush = Frozen(Color.FromArgb(0x30, 0x0F, 0xDC, 0x78));
    private static readonly Brush MutedBrush = Frozen(Color.FromRgb(0x52, 0x52, 0x5B));

    private readonly Dictionary<string, FrameworkElement> _views = new();
    private readonly List<Border> _cards = new();
    private readonly Canvas _overlay = new() { IsHitTestVisible = false };

    private DockNode _layout;
    private string? _maximized;

    // 拖拽状态
    private string? _dragModule;
    private Point _dragStart;
    private bool _dragging;
    private bool _cancelDrag;
    private string? _hoverModule;
    private DockSide _hoverSide;
    private Border? _ghost;
    private Border? _zoneRect;
    private Border? _cardRing;

    public DockHost()
    {
        Background = Brushes.Transparent;
        (_layout, _maximized) = DockLayoutStore.Load();
        Rebuild();
    }

    /// <summary>恢复默认布局（左列番茄钟+记账 / 中列日历 / 右列当日任务）。</summary>
    public void ResetLayout()
    {
        _layout = DockNode.Default();
        _maximized = null;
        Rebuild();
        SaveLayout();
    }

    /// <summary>拖拽过程中按 Esc 取消当前拖动。</summary>
    public void CancelDrag()
    {
        if (_dragModule is null) return;
        _cancelDrag = true;
        if (IsMouseCaptured) ReleaseMouseCapture();
        else EndDrag();
    }

    private void SaveLayout() => DockLayoutStore.Save(_layout, _maximized);

    private static string TitleOf(string id) => Registry.First(r => r.Id == id).Title;

    // ═══════════ 构建 ═══════════

    private void Rebuild()
    {
        foreach (var view in _views.Values) Detach(view);
        _cards.Clear();
        _overlay.Children.Clear();
        _ghost = _zoneRect = _cardRing = null;
        _dragModule = null;
        _dragging = false;
        Children.Clear();

        var root = _maximized is null ? _layout : DockNode.Leaf(_maximized);
        Children.Add(BuildNode(root));
        Children.Add(_overlay);
    }

    private UIElement BuildNode(DockNode node)
    {
        if (node.Module is not null) return BuildCard(node.Module);

        bool horizontal = node.Orientation != "V";
        var children = node.Children!;
        var weights = node.Weights is { Count: > 0 } w && w.Count == children.Count
            ? w
            : Enumerable.Repeat(1.0, children.Count).ToList();

        var grid = new Grid();
        for (int i = 0; i < children.Count; i++)
        {
            double star = Math.Max(0.05, weights[i]);
            if (horizontal)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(star, GridUnitType.Star),
                    MinWidth = 150
                });
                var el = BuildNode(children[i]);
                SetColumn(el, i * 2);
                grid.Children.Add(el);
                if (i < children.Count - 1)
                    AddSplitter(grid, node, horizontal: true, index: i * 2 + 1);
            }
            else
            {
                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = new GridLength(star, GridUnitType.Star),
                    MinHeight = 110
                });
                var el = BuildNode(children[i]);
                SetRow(el, i * 2);
                grid.Children.Add(el);
                if (i < children.Count - 1)
                    AddSplitter(grid, node, horizontal: false, index: i * 2 + 1);
            }
        }
        return grid;
    }

    private void AddSplitter(Grid grid, DockNode node, bool horizontal, int index)
    {
        if (horizontal) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
        else grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });

        var splitter = new GridSplitter
        {
            ResizeDirection = horizontal ? GridResizeDirection.Columns : GridResizeDirection.Rows,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Focusable = false,
        };
        splitter.SetResourceReference(StyleProperty, "DockSplitter");
        splitter.DragCompleted += (_, _) =>
        {
            ReadBackWeights(node, grid, horizontal);
            SaveLayout();
        };

        if (horizontal) SetColumn(splitter, index);
        else SetRow(splitter, index);
        grid.Children.Add(splitter);
    }

    /// <summary>分隔条拖完后：把像素宽度换算回比例权重，并恢复星号宽度（窗口缩放时保持比例）。</summary>
    private static void ReadBackWeights(DockNode node, Grid grid, bool horizontal)
    {
        int count = horizontal ? grid.ColumnDefinitions.Count : grid.RowDefinitions.Count;
        var weights = new List<double>();
        double total = 0;
        for (int i = 0; i < count; i += 2)
        {
            double v = Math.Max(1, horizontal ? grid.ColumnDefinitions[i].ActualWidth : grid.RowDefinitions[i].ActualHeight);
            weights.Add(v);
            total += v;
        }
        for (int k = 0; k < weights.Count; k++) weights[k] /= total;
        node.Weights = weights;

        for (int i = 0, k = 0; i < count; i += 2, k++)
        {
            if (horizontal) grid.ColumnDefinitions[i].Width = new GridLength(weights[k], GridUnitType.Star);
            else grid.RowDefinitions[i].Height = new GridLength(weights[k], GridUnitType.Star);
        }
    }

    private UIElement BuildCard(string moduleId)
    {
        var card = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = ResBrush("CardBrush", Brushes.White),
            BorderBrush = ResBrush("BorderBrush", Brushes.Gray),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(4),
            ClipToBounds = true,
            Tag = moduleId,
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = BuildHeader(moduleId);
        SetRow(header, 0);
        grid.Children.Add(header);

        var view = GetView(moduleId);
        Detach(view);
        SetRow(view, 1);
        grid.Children.Add(view);

        card.Child = grid;
        _cards.Add(card);
        return card;
    }

    private FrameworkElement BuildHeader(string moduleId)
    {
        var header = new Grid
        {
            Height = 34,
            Background = Brushes.Transparent,
            Cursor = Cursors.SizeAll,
            Tag = moduleId,
            ToolTip = "拖动此栏调整布局：拖到其他模块边缘＝分屏，拖到中心＝交换；双击＝最大化此模块",
        };
        ToolTipService.SetShowDuration(header, 8000);

        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var grip = new UniformGrid
        {
            Rows = 3,
            Columns = 2,
            Width = 8,
            Height = 12,
            Margin = new Thickness(12, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        for (int i = 0; i < 6; i++)
            grip.Children.Add(new Ellipse
            {
                Width = 2.4,
                Height = 2.4,
                Fill = MutedBrush,
                Opacity = 0.55,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
        header.Children.Add(grip);

        var title = new TextBlock
        {
            Text = TitleOf(moduleId),
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = ResBrush("TextMutedBrush", MutedBrush),
            VerticalAlignment = VerticalAlignment.Center,
        };
        SetColumn(title, 1);
        header.Children.Add(title);

        var maxBtn = new Button
        {
            Width = 26,
            Height = 26,
            Margin = new Thickness(0, 0, 6, 0),
            ToolTip = _maximized == moduleId ? "还原模块" : "最大化此模块",
        };
        if (ResStyle("IconButton") is { } style) maxBtn.Style = style;
        AutomationProperties.SetName(maxBtn, _maximized == moduleId ? "还原模块" : "最大化模块");
        maxBtn.Content = new Path
        {
            Data = Geometry.Parse(_maximized == moduleId ? "M5,2 H12 V9 M2,5 H9 V12 H2 Z" : "M3,3 H11 V11 H3 Z"),
            Stroke = ResBrush("TextMutedBrush", MutedBrush),
            StrokeThickness = 1.4,
            StrokeLineJoin = PenLineJoin.Round,
            Width = 13,
            Height = 13,
            Stretch = Stretch.Uniform,
        };
        maxBtn.Click += (_, _) => ToggleModuleMaximize(moduleId);
        SetColumn(maxBtn, 2);
        header.Children.Add(maxBtn);

        header.MouseLeftButtonDown += Header_MouseLeftButtonDown;
        return header;
    }

    private void ToggleModuleMaximize(string moduleId)
    {
        _maximized = _maximized == moduleId ? null : moduleId;
        Rebuild();
        SaveLayout();
    }

    private FrameworkElement GetView(string moduleId)
    {
        if (_views.TryGetValue(moduleId, out var view)) return view;
        FrameworkElement created = moduleId switch
        {
            "pomodoro" => new PomodoroPanel(),
            "calendar" => new CalendarPanel(),
            "tasks" => new DayTasksPanel(),
            _ => new ExpensesPanel(),
        };
        _views[moduleId] = created;
        return created;
    }

    private static void Detach(FrameworkElement el)
    {
        switch (el.Parent)
        {
            case null: break;
            case ContentControl cc: cc.Content = null; break;
            case Decorator d: d.Child = null; break;
            case Panel p: p.Children.Remove(el); break;
        }
    }

    // ═══════════ 拖拽 ═══════════

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string moduleId) return;
        if (FindButton(e.OriginalSource as DependencyObject) is not null) return;
        if (e.ClickCount == 2)
        {
            ToggleModuleMaximize(moduleId);
            e.Handled = true;
            return;
        }
        _dragModule = moduleId;
        _dragStart = e.GetPosition(this);
        _dragging = false;
        _cancelDrag = false;
        CaptureMouse();
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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragModule is null) return;
        var p = e.GetPosition(this);
        if (!_dragging)
        {
            if (Math.Abs(p.X - _dragStart.X) < 6 && Math.Abs(p.Y - _dragStart.Y) < 6) return;
            _dragging = true;
            ShowDragVisuals(_dragModule);
        }
        MoveGhost(p);
        UpdateDropTarget(p);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragModule is null) return;
        // 释放捕获 → OnLostMouseCapture 内统一结算落点（与 WPF 自动释放的时序无关）
        if (IsMouseCaptured) ReleaseMouseCapture();
        else EndDrag();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_dragModule is null) return;
        if (_dragging && !_cancelDrag && _hoverModule is not null && _hoverModule != _dragModule)
            ApplyDrop(_dragModule, _hoverModule, _hoverSide);
        EndDrag();
    }

    private void ApplyDrop(string dragged, string target, DockSide side)
    {
        if (dragged == target) return;
        if (side == DockSide.Center)
        {
            DockTree.Swap(_layout, dragged, target);
        }
        else
        {
            var pruned = DockTree.Remove(_layout, dragged);
            if (pruned is null) return;
            var (orientation, before) = side switch
            {
                DockSide.Left => ("H", true),
                DockSide.Right => ("H", false),
                DockSide.Top => ("V", true),
                _ => ("V", false),
            };
            _layout = DockTree.InsertAround(pruned, target, dragged, orientation, before);
        }
        Rebuild();
        SaveLayout();
    }

    private void EndDrag()
    {
        _dragModule = null;
        _dragging = false;
        _cancelDrag = false;
        _hoverModule = null;
        if (_ghost is not null) { _overlay.Children.Remove(_ghost); _ghost = null; }
        if (_zoneRect is not null) { _overlay.Children.Remove(_zoneRect); _zoneRect = null; }
        if (_cardRing is not null) { _overlay.Children.Remove(_cardRing); _cardRing = null; }
        Mouse.OverrideCursor = null;
    }

    private void ShowDragVisuals(string moduleId)
    {
        _ghost = new Border
        {
            Background = ResBrush("CardBrush", Brushes.White),
            BorderBrush = AccentBrush,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 6, 14, 6),
            Opacity = 0.94,
            Child = new TextBlock
            {
                Text = TitleOf(moduleId),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = ResBrush("TextBrush", Brushes.Black),
            },
            Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.25, Color = Colors.Black },
        };
        _zoneRect = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderBrush = AccentBrush,
            BorderThickness = new Thickness(2),
            Background = AccentSoftBrush,
            Visibility = Visibility.Collapsed,
        };
        _cardRing = new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderBrush = AccentBrush,
            BorderThickness = new Thickness(2),
            Visibility = Visibility.Collapsed,
        };
        _overlay.Children.Add(_zoneRect);
        _overlay.Children.Add(_cardRing);
        _overlay.Children.Add(_ghost);
        Mouse.OverrideCursor = Cursors.SizeAll;
    }

    private void MoveGhost(Point p)
    {
        if (_ghost is null) return;
        _ghost.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double w = _ghost.DesiredSize.Width, h = _ghost.DesiredSize.Height;
        Canvas.SetLeft(_ghost, Math.Clamp(p.X + 14, 4, Math.Max(4, ActualWidth - w - 4)));
        Canvas.SetTop(_ghost, Math.Clamp(p.Y + 16, 4, Math.Max(4, ActualHeight - h - 4)));
    }

    private void UpdateDropTarget(Point p)
    {
        _hoverModule = null;
        if (!new Rect(new Point(0, 0), RenderSize).Contains(p))
        {
            HideZone();
            return;
        }
        foreach (var card in _cards)
        {
            var rect = CardRect(card);
            if (!rect.Contains(p)) continue;
            var id = (string)card.Tag;
            if (id == _dragModule)
            {
                HideZone();
                return;
            }
            _hoverModule = id;
            _hoverSide = ResolveSide(p, rect);
            ShowZone(rect, _hoverSide);
            return;
        }
        HideZone();
    }

    private Rect CardRect(Border card)
    {
        var t = card.TransformToAncestor(this);
        return t.TransformBounds(new Rect(card.RenderSize));
    }

    /// <summary>边缘 30% 区域＝分屏吸附方向，中心＝交换。</summary>
    private static DockSide ResolveSide(Point p, Rect r)
    {
        double nx = (p.X - r.X) / r.Width, ny = (p.Y - r.Y) / r.Height;
        double dl = nx, dr = 1 - nx, dt = ny, db = 1 - ny;
        double m = Math.Min(Math.Min(dl, dr), Math.Min(dt, db));
        if (m > 0.3) return DockSide.Center;
        if (m == dl) return DockSide.Left;
        if (m == dr) return DockSide.Right;
        if (m == dt) return DockSide.Top;
        return DockSide.Bottom;
    }

    private void ShowZone(Rect cardRect, DockSide side)
    {
        if (_zoneRect is null || _cardRing is null) return;
        var inner = new Rect(cardRect.X + 8, cardRect.Y + 8, Math.Max(1, cardRect.Width - 16), Math.Max(1, cardRect.Height - 16));
        Rect zone = side switch
        {
            DockSide.Left => new Rect(inner.X, inner.Y, inner.Width / 2, inner.Height),
            DockSide.Right => new Rect(inner.X + inner.Width / 2, inner.Y, inner.Width / 2, inner.Height),
            DockSide.Top => new Rect(inner.X, inner.Y, inner.Width, inner.Height / 2),
            DockSide.Bottom => new Rect(inner.X, inner.Y + inner.Height / 2, inner.Width, inner.Height / 2),
            _ => inner,
        };
        Place(_zoneRect, zone);
        Place(_cardRing, cardRect);
        _zoneRect.Visibility = Visibility.Visible;
        _cardRing.Visibility = Visibility.Visible;
        _cardRing.Opacity = side == DockSide.Center ? 0.9 : 0.55;
    }

    private void HideZone()
    {
        if (_zoneRect is not null) _zoneRect.Visibility = Visibility.Collapsed;
        if (_cardRing is not null) _cardRing.Visibility = Visibility.Collapsed;
    }

    private static void Place(FrameworkElement el, Rect r)
    {
        Canvas.SetLeft(el, r.X);
        Canvas.SetTop(el, r.Y);
        el.Width = r.Width;
        el.Height = r.Height;
    }

    // ═══════════ 小工具 ═══════════

    private static Brush ResBrush(string key, Brush fallback) =>
        Application.Current?.TryFindResource(key) as Brush ?? fallback;

    private static Style? ResStyle(string key) => Application.Current?.TryFindResource(key) as Style;

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}