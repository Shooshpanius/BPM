namespace CoreBPM.Server.Domain.Tasks;

/// <summary>Вопрос по задаче (таблица task_questions, FR-TASK-02.1).</summary>
public class TaskQuestion
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid AuthorUserId { get; set; }
    public Guid RecipientId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? AnswerText { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public TaskItem Task { get; set; } = null!;
}
