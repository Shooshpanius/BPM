namespace CoreBPM.Server.Application.Tasks.DTOs;
public class TaskSummaryDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public Guid AssigneeUserId { get; set; }
    public string AssigneeName { get; set; } = string.Empty;
    public DateTimeOffset DueDate { get; set; }
    public bool IsOverdue { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<string> Tags { get; set; } = new();
}
