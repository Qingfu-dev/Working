using System.Windows;
using System.Windows.Controls;
using TaskTable.App.ViewModels;

namespace TaskTable.App.Views.Modules;

public partial class ExpensesPanel : UserControl
{
    public ExpensesPanel() => InitializeComponent();

    private void ExpenseDelete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ExpenseItemViewModel vm)
            vm.OnDelete?.Invoke(vm);
    }
}