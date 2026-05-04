namespace CoreBPM.Server.Domain.Notify;

/// <summary>Одноразовый токен действия для actionable email (таблица notify_action_tokens).</summary>
public class NotifyActionToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Идентификатор пользователя-получателя.</summary>
    public Guid UserId { get; set; }

    /// <summary>Тип события (TaskAssigned, ImprovementStatusChanged и т.д.).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Действие по токену (Approve, Reject, Open).</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>GUID-токен — вставляется в ссылку письма.</summary>
    public Guid Token { get; set; } = Guid.NewGuid();

    /// <summary>Идентификатор связанной сущности (TaskId, ImprovementId и т.д.).</summary>
    public Guid? EntityId { get; set; }

    /// <summary>Время истечения токена (24 ч по умолчанию).</summary>
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddHours(24);

    /// <summary>Время использования токена; null — не использован.</summary>
    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
