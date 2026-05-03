namespace CoreBPM.Server.Application.Tasks.DTOs;

/// <summary>DTO вопроса по задаче (FR-TASK-02.1).</summary>
public class TaskQuestionDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public Guid RecipientId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string? AnswerText { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
