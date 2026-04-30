namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Мягкая блокировка BPMN-диаграммы для предотвращения конкурентного редактирования.
/// Хранится в таблице bpm_diagram_locks. Одна запись — один заблокированный процесс.
/// Блокировка активна, пока LockedUntil > UtcNow (обновляется heartbeat-ом каждые 30 с).
/// </summary>
public class BpmDiagramLock
{
    public Guid Id { get; set; }

    /// <summary>ID процесса, диаграмма которого заблокирована.</summary>
    public Guid ProcessId { get; set; }

    /// <summary>ID пользователя, захватившего блокировку.</summary>
    public Guid LockedByUserId { get; set; }

    /// <summary>Отображаемое имя пользователя (для показа предупреждения другим).</summary>
    public string LockedByDisplayName { get; set; } = string.Empty;

    /// <summary>Момент захвата блокировки.</summary>
    public DateTimeOffset LockedAt { get; set; }

    /// <summary>
    /// Время, до которого блокировка считается действующей.
    /// Продлевается heartbeat-ом каждые 30 с.
    /// </summary>
    public DateTimeOffset LockedUntil { get; set; }

    // Навигационные свойства
    public BpmProcess Process { get; set; } = null!;
}
