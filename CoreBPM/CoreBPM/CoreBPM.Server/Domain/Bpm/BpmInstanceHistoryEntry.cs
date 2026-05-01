namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Запись журнала событий экземпляра бизнес-процесса (таблица bpm_instance_history).
/// Отражает все значимые действия: запуск, прерывание, комментарии, смены ответственного и т.д.
/// </summary>
public class BpmInstanceHistoryEntry
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на экземпляр процесса.</summary>
    public Guid InstanceId { get; set; }

    /// <summary>Тип события.</summary>
    public BpmHistoryEventType EventType { get; set; }

    /// <summary>Идентификатор пользователя, инициировавшего событие (null — системное событие).</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>Идентификатор BPMN-элемента, связанного с событием (для аналитики узлов).</summary>
    public string? ElementId { get; set; }

    /// <summary>Имя элемента (кешируется для отображения без загрузки диаграммы).</summary>
    public string? ElementName { get; set; }

    /// <summary>Длительность выполнения элемента в миллисекундах (для ServiceTask, ScriptTask и т.д.).</summary>
    public long? DurationMs { get; set; }

    /// <summary>Текстовое описание события или текст комментария/вопроса.</summary>
    public string? Text { get; set; }

    /// <summary>Дополнительные данные события в JSON (например, старое/новое значение).</summary>
    public string? MetaJson { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    // Навигационные свойства
    public BpmInstance Instance { get; set; } = null!;
}
