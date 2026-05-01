namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Задание в очереди исполнения (таблица bpm_execution_jobs).
/// Представляет одну асинхронную операцию: шаг Service Task, Script Task, RPA-задача, срабатывание таймера.
/// Система делает до MaxAttempts попыток выполнения; после исчерпания — переходит в Failed.
/// </summary>
public class BpmExecutionJob
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на определение процесса.</summary>
    public Guid ProcessId { get; set; }

    /// <summary>Ссылка на экземпляр процесса (null для таймерных стартовых событий).</summary>
    public Guid? InstanceId { get; set; }

    /// <summary>Ссылка на версию процесса.</summary>
    public Guid ProcessVersionId { get; set; }

    /// <summary>Идентификатор BPMN-элемента, к которому относится задание.</summary>
    public string ElementId { get; set; } = string.Empty;

    /// <summary>Тип BPMN-элемента (serviceTask, scriptTask, timerEvent, rpaTask и т.д.).</summary>
    public string ElementType { get; set; } = string.Empty;

    /// <summary>Человекочитаемое название операции.</summary>
    public string? OperationName { get; set; }

    /// <summary>Текущий статус задания.</summary>
    public BpmJobStatus Status { get; set; } = BpmJobStatus.Pending;

    /// <summary>Номер текущей / последней попытки (0 — ещё не запускалось).</summary>
    public int AttemptNumber { get; set; } = 0;

    /// <summary>Максимальное число попыток (по умолчанию 9).</summary>
    public int MaxAttempts { get; set; } = 9;

    /// <summary>Время следующего запуска (для Scheduled/Pending).</summary>
    public DateTimeOffset? NextRunAt { get; set; }

    /// <summary>Время начала последней попытки выполнения.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Время успешного завершения.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Время фиксации последней ошибки.</summary>
    public DateTimeOffset? FailedAt { get; set; }

    /// <summary>Текст последней ошибки (сообщение исключения).</summary>
    public string? LastError { get; set; }

    /// <summary>Идентификатор сервера-исполнителя (hostname:PID).</summary>
    public string? ServerHost { get; set; }

    /// <summary>Признак таймерного задания; при ошибке таймер можно только отменить.</summary>
    public bool IsTimer { get; set; } = false;

    /// <summary>Крайний срок для таймеров эскалации (Boundary Timer).</summary>
    public DateTimeOffset? TimerDeadline { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public BpmProcess Process { get; set; } = null!;
    public BpmProcessVersion ProcessVersion { get; set; } = null!;
    public BpmInstance? Instance { get; set; }
}
