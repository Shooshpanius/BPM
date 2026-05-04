namespace CoreBPM.Server.Domain.Notify;

/// <summary>Участник чата (таблица notify_chat_members, FR-MSG-01.1).</summary>
public class NotifyChatMember
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Является ли участник администратором чата.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Уведомления от чата отключены для участника.</summary>
    public bool IsMuted { get; set; }

    public DateTimeOffset JoinedAt { get; set; }

    public NotifyChat? Chat { get; set; }
}
