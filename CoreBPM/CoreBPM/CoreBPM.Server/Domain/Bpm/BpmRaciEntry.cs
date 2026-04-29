namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Запись RACI-матрицы ответственности бизнес-процесса (таблица bpm_raci_entries).
/// Ячейка матрицы «этап процесса × роль».
/// </summary>
public class BpmRaciEntry
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на определение процесса.</summary>
    public Guid ProcessId { get; set; }

    /// <summary>Название этапа / шага процесса.</summary>
    public string Stage { get; set; } = string.Empty;

    /// <summary>Роль (название роли или группы).</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Тип ответственности: R — исполнитель (делает работу), A — ответственный за результат, C — консультант, I — информируется.</summary>
    public BpmRaciType RaciType { get; set; }

    // Навигационные свойства
    public BpmProcess Process { get; set; } = null!;
}
