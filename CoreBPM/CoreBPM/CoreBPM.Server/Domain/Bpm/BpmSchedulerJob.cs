namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Задание планировщика для таймерного стартового события (таблица bpm_scheduler_jobs).
/// Хранит конфигурацию cron/ISO-8601-цикла, извлечённую из опубликованной диаграммы.
/// Фактическое исполнение выполняет фоновый сервис (runtime engine).
/// </summary>
public class BpmSchedulerJob
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на определение процесса.</summary>
    public Guid ProcessId { get; set; }

    /// <summary>Ссылка на версию процесса.</summary>
    public Guid ProcessVersionId { get; set; }

    /// <summary>Идентификатор таймерного стартового события в BPMN-диаграмме.</summary>
    public string ElementId { get; set; } = string.Empty;

    /// <summary>Тип таймера: timeDate | timeDuration | timeCycle.</summary>
    public string TimerType { get; set; } = string.Empty;

    /// <summary>Значение таймера (cron-выражение или ISO 8601).</summary>
    public string TimerValue { get; set; } = string.Empty;

    /// <summary>Идентификатор часового пояса (IANA Time Zone, например «Europe/Moscow»).</summary>
    public string? TimeZone { get; set; }

    /// <summary>Задание активно (включено администратором).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Дата/время последнего срабатывания.</summary>
    public DateTimeOffset? LastFiredAt { get; set; }

    /// <summary>Дата/время следующего ожидаемого срабатывания (вычисляется при активации).</summary>
    public DateTimeOffset? NextFireAt { get; set; }

    /// <summary>Текущий статус задания планировщика.</summary>
    public BpmJobStatus Status { get; set; } = BpmJobStatus.Scheduled;

    /// <summary>Количество неудачных попыток запуска подряд.</summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>Текст последней ошибки при попытке запуска.</summary>
    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public BpmProcess Process { get; set; } = null!;
    public BpmProcessVersion ProcessVersion { get; set; } = null!;
}
