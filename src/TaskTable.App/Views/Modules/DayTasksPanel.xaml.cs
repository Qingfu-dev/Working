using System.Windows;
using System.Windows.Controls;
using TaskTable.App.ViewModels;

namespace TaskTable.App.Views.Modules;

public partial class DayTasksPanel : UserControl
{
    public DayTasksPanel() => InitializeComponent();

    private void TimerButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is TaskItemViewModel vm)
            vm.OnStartStopTimer?.Invoke(vm);
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not TaskItemViewModel vm) return;
        var result = MessageBox.Show(Window.GetWindow(this), $"删除任务「{vm.Title}」？", "确认删除",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
            vm.OnDelete?.Invoke(vm);
    }
}