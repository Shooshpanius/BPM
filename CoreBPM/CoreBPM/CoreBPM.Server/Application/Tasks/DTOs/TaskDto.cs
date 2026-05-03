using CoreBPM.Server.Domain.Tasks;
namespace CoreBPM.Server.Application.Tasks.DTOs;
public class TaskDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public Guid AssigneeUserId { get; set; }
    public string AssigneeName { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public string DateCorrectionMode { get; set; } = string.Empty;
    public int? PlannedEffortMinutes { get; set; }
    /// <summary>Фактические трудозатраты — сумма всех записей (FR-TASK-01.4).</summary>
    public int ActualEffortMinutes { get; set; }
    public string ControlType { get; set; } = string.Empty;
    public Guid? ControllerUserId { get; set; }
    public string? ControllerName { get; set; }
    /// <summary>Согласующий (участник с ролью Approver), если назначен (FR-TASK-01.3).</summary>
    public Guid? ApproverUserId { get; set; }
    public string? ApproverName { get; set; }
    public Guid? ParentTaskId { get; set; }
    public bool IsOverdue { get; set; }
    public DateTimeOffset? PostponedUntil { get; set; }
    public Guid? SourceInstanceId { get; set; }
    public string? SourceElementId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<TaskParticipantDto> Participants { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public int SubtaskCount { get; set; }
    public int CommentCount { get; set; }
    public int AttachmentCount { get; set; }
}
