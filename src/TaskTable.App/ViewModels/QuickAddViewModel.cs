using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskTable.Core.Models;

namespace TaskTable.App.ViewModels;

/// <summary>快速添加窗口 VM（人类发布通道）。</summary>
public sealed partial class QuickAddViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _dateText;
    [ObservableProperty] private int _priorityIndex = 1; // 0高 1中 2低

    public Action<TaskItem>? Saved;
    public Action? Cancelled;

    public QuickAddViewModel(DateOnly defaultDate)
    {
        _dateText = defaultDate.ToString("yyyy-MM-dd");
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Title)) return;
        var date = DateOnly.TryParseExact(DateText.Trim(), "yyyy-MM-dd", out var d)
            ? d
            : DateOnly.FromDateTime(DateTime.Today);
        var task = new TaskItem
        {
            Title = Title.Trim(),
            PlanDate = date,
            Priority = PriorityIndex switch { 0 => 1, 2 => 3, _ => 2 },
            Source = "manual"
        };
        Saved?.Invoke(task);
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();
}
