namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Определение бизнес-процесса (таблица bpm_processes).</summary>
public class BpmProcess
{
    public Guid Id { get; set; }

    /// <summary>Идентификатор организации-владельца процесса.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Название процесса.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Описание назначения процесса.</summary>
    public string? Description { get; set; }

    /// <summary>Идентификатор пользователя, создавшего процесс.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>Теги процесса (JSON-массив строк).</summary>
    public string TagsJson { get; set; } = "[]";

    /// <summary>Является ли процесс шаблоном.</summary>
    public bool IsTemplate { get; set; } = false;

    /// <summary>Разрешён ручной запуск с портала.</summary>
    public bool LaunchFromPortalEnabled { get; set; } = true;

    /// <summary>Показывать процесс в глобальном списке запуска.</summary>
    public bool ShowInStartList { get; set; } = true;

    /// <summary>Разрешён запуск из внешних систем.</summary>
    public bool ExternalStartEnabled { get; set; } = false;

    /// <summary>JSON-массив допустимых методов внешнего запуска (GET/POST/SOAP).</summary>
    public string ExternalStartMethodsJson { get; set; } = "[]";

    /// <summary>Ограничения доступа для внешнего запуска (например, список IP/CIDR).</summary>
    public string? ExternalStartAllowedIps { get; set; }

    /// <summary>SHA-256 хэш токена внешнего запуска.</summary>
    public string? ExternalStartTokenHash { get; set; }

    /// <summary>Безопасный preview токена (например, последние 4 символа).</summary>
    public string? ExternalStartTokenPreview { get; set; }

    public DateTimeOffset? ExternalStartTokenUpdatedAt { get; set; }

    /// <summary>Режим формирования названия экземпляра.</summary>
    public BpmInstanceNameMode InstanceNameMode { get; set; } = BpmInstanceNameMode.Manual;

    /// <summary>Запрашивать название экземпляра при запуске.</summary>
    public bool RequestInstanceNameOnStart { get; set; } = true;

    /// <summary>Шаблон названия экземпляра.</summary>
    public string? InstanceNameTemplate { get; set; }

    /// <summary>Имя класса структуры данных процесса.</summary>
    public string DataClassName { get; set; } = string.Empty;

    /// <summary>Имя таблицы БД для хранения данных процесса.</summary>
    public string DataTableName { get; set; } = string.Empty;

    /// <summary>Имя класса метрик процесса.</summary>
    public string ProcessMetricsClassName { get; set; } = string.Empty;

    /// <summary>Имя таблицы метрик процесса.</summary>
    public string ProcessMetricsTableName { get; set; } = string.Empty;

    /// <summary>Имя класса метрик экземпляра процесса.</summary>
    public string InstanceMetricsClassName { get; set; } = string.Empty;

    /// <summary>Имя таблицы метрик экземпляра процесса.</summary>
    public string InstanceMetricsTableName { get; set; } = string.Empty;

    /// <summary>Включён второй рантайм процесса.</summary>
    public bool SecondRuntimeEnabled { get; set; } = false;

    public DateTimeOffset? SecondRuntimeUpgradedAt { get; set; }

    // ── KPI-цели процесса (FR-BPM-03.2) ──────────────────────────────────────

    /// <summary>Целевое время цикла экземпляра процесса в минутах.</summary>
    public double? TargetCycleTimeMinutes { get; set; }

    /// <summary>Целевой процент экземпляров, завершённых в срок (0–100).</summary>
    public double? TargetOnTimePercent { get; set; }

    /// <summary>Целевая стоимость одного экземпляра процесса (по трудозатратам).</summary>
    public decimal? TargetCostPerInstance { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Мягкое удаление.</summary>
    public bool IsDeleted { get; set; } = false;

    // Навигационные свойства
    public ICollection<BpmProcessVersion> Versions { get; set; } = new List<BpmProcessVersion>();
}
