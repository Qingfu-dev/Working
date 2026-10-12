using System.Windows;
using TaskTable.App.ViewModels;

namespace TaskTable.App.Views;

public partial class HeatmapWindow : Window
{
    public HeatmapWindow(HeatmapViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        TitleBar.MouseLeftButtonDown += (_, _) =>
        {
            try { DragMove(); }
            catch (InvalidOperationException) { /* 鼠标已释放，忽略 */ }
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}