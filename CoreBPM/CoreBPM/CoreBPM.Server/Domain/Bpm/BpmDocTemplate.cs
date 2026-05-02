namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Шаблон документа процесса (таблица bpm_doc_templates).</summary>
public class BpmDocTemplate
{
    public Guid Id { get; set; }

    /// <summary>Название шаблона.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Бинарное содержимое файла шаблона (.docx).</summary>
    public byte[] FileContent { get; set; } = Array.Empty<byte>();

    /// <summary>Оригинальное имя файла.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Идентификатор пользователя, загрузившего шаблон.</summary>
    public Guid UploadedByUserId { get; set; }

    public DateTimeOffset UploadedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
