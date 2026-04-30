namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Экземпляр бизнес-процесса (таблица bpm_instances).
/// Создаётся при каждом запуске опубликованного процесса.
/// </summary>
public class BpmInstance
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на определение процесса.</summary>
    public Guid ProcessId { get; set; }

    /// <summary>Ссылка на версию процесса, по которой был запущен экземпляр.</summary>
    public Guid ProcessVersionId { get; set; }

    /// <summary>Название экземпляра (заполняется при запуске).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Текущее состояние экземпляра.</summary>
    public BpmInstanceState State { get; set; } = BpmInstanceState.Active;

    /// <summary>Источник запуска.</summary>
    public BpmInstanceLaunchSource LaunchSource { get; set; } = BpmInstanceLaunchSource.Manual;

    /// <summary>Идентификатор пользователя-инициатора.</summary>
    public Guid? InitiatorUserId { get; set; }

    /// <summary>Идентификатор пользователя, ответственного за достижение результата.
    /// По умолчанию совпадает с инициатором; может быть изменён в ходе выполнения.</summary>
    public Guid? ResponsibleUserId { get; set; }

    /// <summary>Ссылка на родительский экземпляр (для Call Activity).</summary>
    public Guid? ParentInstanceId { get; set; }

    /// <summary>Внешний ключ корреляции — для вебхуков и Message Start Event.</summary>
    public string? ExternalReference { get; set; }

    /// <summary>Причина прерывания экземпляра.</summary>
    public string? CancelReason { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public BpmProcess Process { get; set; } = null!;
    public BpmProcessVersion ProcessVersion { get; set; } = null!;
    public ICollection<BpmInstanceVariable> Variables { get; set; } = new List<BpmInstanceVariable>();
}
