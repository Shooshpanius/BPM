namespace CoreBPM.Server.Domain.Org;

/// <summary>
/// Вложение должности — файл должностной инструкции (таблица org_position_attachments).
/// </summary>
public class OrgPositionAttachment
{
    public Guid Id { get; set; }

    /// <summary>Должность, к которой относится вложение.</summary>
    public Guid PositionId { get; set; }

    /// <summary>Оригинальное имя файла.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME-тип файла.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Путь к файлу на диске (относительный).</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Описание / назначение вложения.</summary>
    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Навигационные свойства
    public OrgPosition Position { get; set; } = null!;
}
