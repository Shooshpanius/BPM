namespace CoreBPM.Server.Domain.Tasks;

/// <summary>
/// Настройки уведомлений пользователя по задачам (таблица user_task_notification_settings, FR-TASK-02.3).
/// Каждая строка — один тип события + канал + включён/выключен.
/// </summary>
public class UserTaskNotificationSettings
{
    public Guid Id { get; set; }

    /// <summary>Пользователь, для которого настроены уведомления.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Тип события задачи: TaskAssigned, TaskDone, TaskOverdue, TaskCommentAdded,
    /// TaskReminder, TaskRescheduled, TaskReopened, TaskQuestionAsked, TaskMentioned, и т.д.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Отправлять ли in-app уведомление (SignalR).</summary>
    public bool InApp { get; set; } = true;

    /// <summary>Отправлять ли email-уведомление.</summary>
    public bool Email { get; set; } = false;
}
