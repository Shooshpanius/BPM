namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Пакет миграции версий экземпляров процессов (таблица bpm_version_migration_packages).
/// Объединяет группу экземпляров для массового перевода на новую версию.
/// </summary>
public class BpmVersionMigrationPackage
{
    public Guid Id { get; set; }

    /// <summary>Наименование пакета миграции.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Идентификатор пользователя, создавшего пакет.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>Текущий статус пакета.</summary>
    public BpmMigrationPackageStatus Status { get; set; } = BpmMigrationPackageStatus.New;

    /// <summary>Признак активности пакета (используется для фильтрации).</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Время завершения обработки пакета.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Время автоматического запуска пакета по расписанию (UTC). Null — без расписания.</summary>
    public DateTimeOffset? ScheduledAt { get; set; }

    // Навигационные свойства
    public ICollection<BpmVersionMigrationItem> Items { get; set; } = new List<BpmVersionMigrationItem>();
}
