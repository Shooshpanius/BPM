namespace CoreBPM.Server.Domain.Tasks;
/// <summary>Сохранённый фильтр задач (таблица task_saved_filters).</summary>
public class TaskSavedFilter
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FilterJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}
