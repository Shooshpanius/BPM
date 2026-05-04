namespace CoreBPM.Server.Domain.Tasks;

/// <summary>
/// Настройки уведомлений пользователя по типу события (таблица user_task_notification_settings, FR-MSG-02.2).
/// Каждая строка — один тип события + выбранные каналы доставки.
/// </summary>
public class UserTaskNotificationSettings
{
    public Guid Id { get; set; }

    /// <summary>Пользователь, для которого настроены уведомления.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Тип события: TaskAssigned, TaskDone, TaskOverdue, TaskCommentAdded,
    /// TaskReminder, TaskRescheduled, TaskReopened, TaskQuestionAsked, TaskMentioned,
    /// ChatMessageReceived, ChannelPostPublished, ChannelInvite и т.д.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Отправлять ли in-app уведомление (SignalR).</summary>
    public bool InApp { get; set; } = true;

    /// <summary>Отправлять ли email-уведомление.</summary>
    public bool Email { get; set; } = false;

    /// <summary>Отправлять ли SMS-уведомление.</summary>
    public bool Sms { get; set; } = false;

    /// <summary>Отправлять ли Web Push-уведомление.</summary>
    public bool Push { get; set; } = false;
}
