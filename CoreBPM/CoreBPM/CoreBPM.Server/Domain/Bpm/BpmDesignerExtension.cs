namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Пользовательское расширение палитры BPMN-дизайнера (таблица bpm_designer_extensions).
/// Расширение — именованная C#-операция, добавляемая в раздел Plug-ins.
/// </summary>
public class BpmDesignerExtension
{
    public Guid Id { get; set; }

    /// <summary>Организация-владелец расширения.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Название расширения.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Описание расширения.</summary>
    public string? Description { get; set; }

    /// <summary>Путь папки для группировки (например «Интеграции/1С»). Null — корень.</summary>
    public string? FolderPath { get; set; }

    /// <summary>Тело C#-сценария операции.</summary>
    public string ScriptBody { get; set; } = string.Empty;

    /// <summary>Признак публикации: опубликованные расширения доступны в палитре дизайнера.</summary>
    public bool IsPublished { get; set; } = false;

    /// <summary>Пользователь, создавший расширение.</summary>
    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Мягкое удаление.</summary>
    public bool IsDeleted { get; set; } = false;
}
