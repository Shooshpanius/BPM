namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Конфигурация пользовательских статусов экземпляров процесса (таблица bpm_instance_status_configs).
/// Один процесс — один конфиг; создаётся лениво при первом сохранении.
/// </summary>
public class BpmInstanceStatusConfig
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на бизнес-процесс.</summary>
    public Guid ProcessId { get; set; }

    /// <summary>
    /// ID переменной контекста процесса (типа List), в которой хранится текущий статус экземпляра.
    /// Null — переменная ещё не привязана.
    /// </summary>
    public Guid? LinkedVariableId { get; set; }

    /// <summary>Действие над статусом при прерывании экземпляра.</summary>
    public BpmInterruptAction OnInterruptAction { get; set; } = BpmInterruptAction.KeepCurrent;

    /// <summary>
    /// ID сценария, выполняемого при прерывании, если OnInterruptAction == RunScript.
    /// Хранится как строка (ссылка на будущий модуль сценариев).
    /// </summary>
    public string? OnInterruptScriptId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public BpmProcess Process { get; set; } = null!;
}
