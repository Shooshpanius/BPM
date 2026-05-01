namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Элемент пакета миграции версий (таблица bpm_version_migration_items).
/// Описывает перевод конкретного экземпляра процесса на заданную версию.
/// </summary>
public class BpmVersionMigrationItem
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на пакет миграции.</summary>
    public Guid PackageId { get; set; }

    /// <summary>Ссылка на экземпляр процесса.</summary>
    public Guid InstanceId { get; set; }

    /// <summary>Ссылка на определение процесса (денормализация для удобства фильтрации).</summary>
    public Guid ProcessId { get; set; }

    /// <summary>Ссылка на целевую версию, на которую нужно перевести экземпляр.</summary>
    public Guid TargetVersionId { get; set; }

    /// <summary>Статус обработки данного элемента.</summary>
    public BpmMigrationItemStatus Status { get; set; } = BpmMigrationItemStatus.New;

    /// <summary>Комментарий при ошибке или несовместимости.</summary>
    public string? ErrorComment { get; set; }

    /// <summary>Ссылка на ручное изменение (заполняется при статусе RequiresManualHandling).</summary>
    public string? ManualChangeUrl { get; set; }

    /// <summary>Время завершения обработки элемента.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    // Навигационные свойства
    public BpmVersionMigrationPackage Package { get; set; } = null!;
    public BpmInstance Instance { get; set; } = null!;
    public BpmProcess Process { get; set; } = null!;
    public BpmProcessVersion TargetVersion { get; set; } = null!;
}
