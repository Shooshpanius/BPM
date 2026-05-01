namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// HTML-снапшот документации версии процесса (таблица bpm_process_doc_snapshots).
/// Генерируется автоматически при публикации версии процесса.
/// </summary>
public class BpmProcessDocSnapshot
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на определение процесса.</summary>
    public Guid ProcessId { get; set; }

    /// <summary>Ссылка на конкретную версию процесса.</summary>
    public Guid ProcessVersionId { get; set; }

    /// <summary>Сгенерированный HTML-документ технической документации процесса.</summary>
    public string HtmlContent { get; set; } = string.Empty;

    /// <summary>Дата и время генерации снапшота.</summary>
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>Идентификатор пользователя, опубликовавшего версию (кто инициировал генерацию).</summary>
    public Guid GeneratedByUserId { get; set; }

    // Навигационные свойства
    public BpmProcess Process { get; set; } = null!;
    public BpmProcessVersion ProcessVersion { get; set; } = null!;
}
