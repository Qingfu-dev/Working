using System.Windows.Controls;
using System.Windows.Input;
using TaskTable.App.ViewModels;

namespace TaskTable.App.Views.Modules;

public partial class CalendarPanel : UserControl
{
    public CalendarPanel() => InitializeComponent();

    private void CalendarGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        => (DataContext as MainViewModel)?.AddTaskCommand.Execute(null);
}