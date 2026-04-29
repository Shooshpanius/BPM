namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Версия формы задачи (таблица bpm_task_form_versions).
/// Хранит полную JSON-схему полей и разметки формы.
/// </summary>
public class BpmTaskFormVersion
{
    public Guid Id { get; set; }

    /// <summary>FK на форму.</summary>
    public Guid FormId { get; set; }

    /// <summary>Порядковый номер версии (1, 2, 3, ...).</summary>
    public int VersionNumber { get; set; }

    /// <summary>JSON-схема формы (массив секций → строки → поля).</summary>
    public string Schema { get; set; } = "{}";

    /// <summary>Статус версии.</summary>
    public BpmFormVersionStatus Status { get; set; } = BpmFormVersionStatus.Draft;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    // Навигационные свойства
    public BpmTaskForm Form { get; set; } = null!;
}
