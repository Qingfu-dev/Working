using System.Windows.Media;
using TaskTable.Core.Models;

namespace TaskTable.App.ViewModels;

/// <summary>消费条目 VM（金额 + 类型）。</summary>
public sealed class ExpenseItemViewModel
{
    private static readonly Brush MealsBrush = Frozen(0xEF, 0xAA, 0x17);
    private static readonly Brush SnacksBrush = Frozen(0xF8, 0x74, 0x54);
    private static readonly Brush ShoppingBrush = Frozen(0x5B, 0x9B, 0xD5);
    private static readonly Brush ExtraBrush = Frozen(0x8A, 0x8A, 0x93);

    public long Id { get; }
    public string CategoryText { get; }
    public string DisplayText { get; }
    public Brush CategoryBrush { get; }

    /// <summary>删除回调（由 MainViewModel 注入）。</summary>
    public Action<ExpenseItemViewModel>? OnDelete { get; set; }

    public ExpenseItemViewModel(Expense model)
    {
        Id = model.Id;
        CategoryText = model.Category;
        DisplayText = $"{model.Category} ¥{model.Amount:0.00}";
        CategoryBrush = model.Category switch
        {
            ExpenseCategories.Meals => MealsBrush,
            ExpenseCategories.Snacks => SnacksBrush,
            ExpenseCategories.Shopping => ShoppingBrush,
            _ => ExtraBrush
        };
    }

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}