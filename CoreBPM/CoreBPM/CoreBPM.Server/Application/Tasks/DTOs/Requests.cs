using CoreBPM.Server.Domain.Tasks;
namespace CoreBPM.Server.Application.Tasks.DTOs;

public class CreateTaskRequest
{
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public string? CategoryId { get; set; }
    public Guid AssigneeUserId { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public TaskDateCorrectionMode DateCorrectionMode { get; set; } = TaskDateCorrectionMode.None;
    public int? PlannedEffortMinutes { get; set; }
    public TaskControlType ControlType { get; set; } = TaskControlType.None;
    public Guid? ControllerUserId { get; set; }
    public Guid? ParentTaskId { get; set; }
    public Guid? ApproverId { get; set; }
    public List<Guid> CoExecutorIds { get; set; } = new();
    public List<Guid> ObserverIds { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public DateTimeOffset? ReminderAt { get; set; }
}

public class UpdateTaskRequest
{
    public string? Subject { get; set; }
    public string? Description { get; set; }
    public TaskPriority? Priority { get; set; }
    public string? CategoryId { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public int? PlannedEffortMinutes { get; set; }
    public TaskControlType? ControlType { get; set; }
    public Guid? ControllerUserId { get; set; }
}

public class ReassignTaskRequest
{
    public Guid AssigneeUserId { get; set; }
    public string? Comment { get; set; }
}

public class AddTaskParticipantRequest
{
    public Guid UserId { get; set; }
    public TaskParticipantRole Role { get; set; }
}

public class AddTaskCommentRequest
{
    public string Body { get; set; } = string.Empty;
}

public class AddTaskRelationRequest
{
    public Guid TargetTaskId { get; set; }
    public TaskRelationType RelationType { get; set; }
}

public class AddTaskTagRequest
{
    public string Value { get; set; } = string.Empty;
}

public class AddTaskAttachmentRequest
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

public class TaskListFilter
{
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid? AuthorId { get; set; }
    public string? CategoryId { get; set; }
    public string? TagValue { get; set; }
    public DateTimeOffset? DateFrom { get; set; }
    public DateTimeOffset? DateTo { get; set; }
    public bool? IsOverdue { get; set; }
    public string? Search { get; set; }
    public string SortBy { get; set; } = "created_at";
    public string SortDir { get; set; } = "desc";
}
