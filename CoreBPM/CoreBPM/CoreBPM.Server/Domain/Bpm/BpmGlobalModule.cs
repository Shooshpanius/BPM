namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Глобальный C#-модуль — именованная библиотека функций, доступная во всех сценариях (таблица bpm_global_modules).
/// Содержит набор файлов-сценариев (<see cref="BpmGlobalModuleFile"/>).
/// </summary>
public class BpmGlobalModule
{
    public Guid Id { get; set; }

    /// <summary>Организация-владелец модуля.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Название модуля.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Описание назначения модуля.</summary>
    public string? Description { get; set; }

    /// <summary>Признак публикации: опубликованный модуль доступен в среде выполнения сценариев.</summary>
    public bool IsPublished { get; set; } = false;

    /// <summary>Пользователь, создавший модуль.</summary>
    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Дата последней публикации модуля.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Мягкое удаление.</summary>
    public bool IsDeleted { get; set; } = false;

    // Навигационные свойства
    public ICollection<BpmGlobalModuleFile> Files { get; set; } = new List<BpmGlobalModuleFile>();
}
