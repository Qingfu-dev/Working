using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TaskTable.App.ViewModels;

namespace TaskTable.App.Views;

public partial class QuickAddWindow : Window
{
    public QuickAddWindow(QuickAddViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        vm.Saved += _ => Close();
        vm.Cancelled += Close;

        TitleBar.MouseLeftButtonDown += (_, _) =>
        {
            try { DragMove(); }
            catch (InvalidOperationException) { /* 鼠标已释放，忽略 */ }
        };

        Loaded += (_, _) =>
        {
            TitleBox.Focus();
            TitleBox.SelectAll();
        };
    }

    private void Prio_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is QuickAddViewModel vm && sender is RadioButton { Tag: string tag }
            && int.TryParse(tag, out int index))
            vm.PriorityIndex = index;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
