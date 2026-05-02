using CoreBPM.Server.Domain.Tasks;
namespace CoreBPM.Server.Application.Tasks.DTOs;
public class TaskTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? DefaultAssigneeUserId { get; set; }
    public string? DefaultAssigneeName { get; set; }
    public string DefaultPriority { get; set; } = string.Empty;
    public string? DefaultCategoryId { get; set; }
    public string? Description { get; set; }
    public string ControlType { get; set; } = string.Empty;
    public int? PlannedEffortMinutes { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsPublic { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public class CreateTaskTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid? DefaultAssigneeUserId { get; set; }
    public TaskPriority DefaultPriority { get; set; } = TaskPriority.Medium;
    public string? DefaultCategoryId { get; set; }
    public string? Description { get; set; }
    public TaskControlType ControlType { get; set; } = TaskControlType.None;
    public int? PlannedEffortMinutes { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsPublic { get; set; }
}
