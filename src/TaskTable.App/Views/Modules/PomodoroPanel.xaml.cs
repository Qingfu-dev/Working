using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TaskTable.App.ViewModels;

namespace TaskTable.App.Views.Modules;

public partial class PomodoroPanel : UserControl
{
    private INotifyPropertyChanged? _vm;
    private double _ringSize = 150;

    public PomodoroPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += (_, _) => LayoutRing();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= Vm_PropertyChanged;
        _vm = DataContext as INotifyPropertyChanged;
        if (_vm is not null) _vm.PropertyChanged += Vm_PropertyChanged;
        UpdateArc();
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.PomodoroRingFraction)) UpdateArc();
    }

    /// <summary>环随模块大小自适应（模块最大化时变大）。</summary>
    private void LayoutRing()
    {
        double size = Math.Clamp(Math.Min(ActualWidth, ActualHeight) - 96, 132, 320);
        if (Math.Abs(size - _ringSize) < 0.5) return;
        _ringSize = size;
        RingGrid.Width = size;
        RingGrid.Height = size;
        RingCanvas.Width = size;
        RingCanvas.Height = size;
        RingBase.Width = size - 10;
        RingBase.Height = size - 10;
        UpdateArc();
    }

    /// <summary>按剩余比例绘制进度弧：起点正上方，顺时针。</summary>
    private void UpdateArc()
    {
        double fraction = (DataContext as MainViewModel)?.PomodoroRingFraction ?? 1.0;
        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction <= 0.001)
        {
            RingArc.Data = null;
            return;
        }

        double s = _ringSize;
        double c = s / 2;
        double r = s / 2 - 5;
        var start = new Point(c, c - r);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            if (fraction >= 0.9999)
            {
                // 整圆需要两段弧
                ctx.BeginFigure(start, false, false);
                ctx.ArcTo(new Point(c, c + r), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                ctx.ArcTo(start, new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
            }
            else
            {
                double rad = (fraction * 360 - 90) * Math.PI / 180;
                var end = new Point(c + r * Math.Cos(rad), c + r * Math.Sin(rad));
                ctx.BeginFigure(start, false, false);
                ctx.ArcTo(end, new Size(r, r), 0, fraction > 0.5, SweepDirection.Clockwise, true, false);
            }
        }
        geometry.Freeze();
        RingArc.Data = geometry;
    }
}