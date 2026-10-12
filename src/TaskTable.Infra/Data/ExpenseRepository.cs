using TaskTable.Core.Models;

namespace TaskTable.Infra.Data;

public sealed class ExpenseRepository
{
    private readonly Database _db;
    public ExpenseRepository(Database db) => _db = db;

    public void Add(decimal amount, string category, DateOnly date)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO expenses (amount, category, expense_date, created_at)
            VALUES ($amount, $category, $date, $created);
            """;
        cmd.Parameters.AddWithValue("$amount", (double)amount);
        cmd.Parameters.AddWithValue("$category", category);
        cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$created", DateTime.Now.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public List<Expense> GetByDate(DateOnly date)
    {
        var list = new List<Expense>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, amount, category, expense_date, created_at FROM expenses WHERE expense_date = $date ORDER BY id;";
        cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Expense
            {
                Id = reader.GetInt64(0),
                Amount = (decimal)reader.GetDouble(1),
                Category = reader.GetString(2),
                Date = DateOnly.ParseExact(reader.GetString(3), "yyyy-MM-dd"),
                CreatedAt = DateTime.Parse(reader.GetString(4))
            });
        }
        return list;
    }

    public void Delete(long id)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM expenses WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}