using System.Diagnostics;
using System.Windows;
using TaskTable.App.ViewModels;
using TaskTable.Infra.System;

namespace TaskTable.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        vm.Saved += Close;
        vm.Cancelled += Close;

        TitleBar.MouseLeftButtonDown += (_, _) =>
        {
            try { DragMove(); }
            catch (InvalidOperationException) { /* 鼠标已释放，忽略 */ }
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void OpenInbox_Click(object sender, RoutedEventArgs e)
    {
        AppPaths.EnsureAll();
        Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.InboxDir) { UseShellExecute = true });
    }
}