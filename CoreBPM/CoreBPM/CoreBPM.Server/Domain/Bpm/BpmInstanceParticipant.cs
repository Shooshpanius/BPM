namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Участник экземпляра бизнес-процесса (таблица bpm_instance_participants).
/// Участники видят экземпляр в своём разделе «Мои процессы».
/// </summary>
public class BpmInstanceParticipant
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на экземпляр процесса.</summary>
    public Guid InstanceId { get; set; }

    /// <summary>Идентификатор пользователя-участника.</summary>
    public Guid UserId { get; set; }

    /// <summary>Отображаемое имя участника (денормализованное для быстрого вывода).</summary>
    public string? DisplayName { get; set; }

    /// <summary>Кто добавил участника.</summary>
    public Guid? AddedByUserId { get; set; }

    public DateTimeOffset AddedAt { get; set; }

    // Навигационные свойства
    public BpmInstance Instance { get; set; } = null!;
}
