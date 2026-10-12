namespace TaskTable.Core.Models;

public sealed class Expense
{
    public long Id { get; set; }
    public decimal Amount { get; set; }
    public string Category { get; set; } = ExpenseCategories.Meals;
    public DateOnly Date { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>消费类型（固定四类）。</summary>
public static class ExpenseCategories
{
    public const string Meals = "吃饭";
    public const string Snacks = "零食";
    public const string Shopping = "购物";
    public const string Extra = "额外";

    public static readonly string[] All = [Meals, Snacks, Shopping, Extra];
}