namespace CoreBPM.Server.Domain.Tasks;
/// <summary>Шаблон задачи (таблица task_templates).</summary>
public class TaskTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? DefaultAssigneeUserId { get; set; }
    public TaskPriority DefaultPriority { get; set; } = TaskPriority.Medium;
    public string? DefaultCategoryId { get; set; }
    public string? Description { get; set; }
    public TaskControlType ControlType { get; set; } = TaskControlType.None;
    public int? PlannedEffortMinutes { get; set; }
    public string? TagsJson { get; set; }
    public bool IsPublic { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
