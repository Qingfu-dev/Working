using System.Text.Json;
using System.Text.Json.Serialization;
using TaskTable.Core.Models;
using TaskStatus = TaskTable.Core.Models.TaskStatus;

namespace TaskTable.Core.Inbox;

/// <summary>
/// AI 收件箱任务 JSON Schema。单任务对象或对象数组。
/// 除 title 外全部可选。
/// </summary>
public sealed class InboxTaskDto
{
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("date")] public string? Date { get; set; }
    [JsonPropertyName("priority")] public int? Priority { get; set; }
    [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
    [JsonPropertyName("estimateMinutes")] public int? EstimateMinutes { get; set; }
    [JsonPropertyName("source")] public string? Source { get; set; }
}

public static class InboxTaskParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>解析收件箱 JSON（对象或数组）。解析失败抛出 InboxParseException。</summary>
    public static List<TaskItem> Parse(string json, DateOnly fallbackDate)
    {
        List<InboxTaskDto>? dtos;
        try
        {
            json = json.TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
            if (json.StartsWith('['))
                dtos = JsonSerializer.Deserialize<List<InboxTaskDto>>(json, Options);
            else
            {
                var single = JsonSerializer.Deserialize<InboxTaskDto>(json, Options);
                dtos = single is null ? null : [single];
            }
        }
        catch (JsonException ex)
        {
            throw new InboxParseException($"JSON 格式错误: {ex.Message}", ex);
        }

        if (dtos is null || dtos.Count == 0)
            throw new InboxParseException("文件不包含任何任务");

        var tasks = new List<TaskItem>(dtos.Count);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            if (dto is null || string.IsNullOrWhiteSpace(dto.Title))
                throw new InboxParseException($"第 {i + 1} 个任务缺少必填字段 title");

            DateOnly date = fallbackDate;
            if (!string.IsNullOrWhiteSpace(dto.Date)
                && !DateOnly.TryParseExact(dto.Date.Trim(), "yyyy-MM-dd", out date))
                throw new InboxParseException($"第 {i + 1} 个任务 date 格式错误（应为 yyyy-MM-dd）: {dto.Date}");

            int priority = dto.Priority ?? 2;
            if (priority is < 1 or > 3) priority = 2;

            tasks.Add(new TaskItem
            {
                Title = dto.Title.Trim(),
                Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
                PlanDate = date,
                Priority = priority,
                Tags = dto.Tags is { Count: > 0 } ? string.Join(',', dto.Tags) : null,
                EstimateMinutes = dto.EstimateMinutes is > 0 ? dto.EstimateMinutes : null,
                Source = string.IsNullOrWhiteSpace(dto.Source) ? "ai" : dto.Source.Trim(),
                Status = TaskStatus.Todo,
                CreatedAt = DateTime.Now
            });
        }
        return tasks;
    }
}

public sealed class InboxParseException : Exception
{
    public InboxParseException(string message, Exception? inner = null) : base(message, inner) { }
}
