namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Запись об обнаруженном KPI-алерте (превышение порогового времени цикла).
/// Сохраняется в таблице bpm_kpi_alerts (FR-BPM-03.2).
/// </summary>
public class BpmKpiAlert
{
    /// <summary>Идентификатор алерта.</summary>
    public Guid Id { get; set; }

    /// <summary>Идентификатор процесса, по которому сработал алерт.</summary>
    public Guid ProcessId { get; set; }

    /// <summary>Имя процесса на момент срабатывания.</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>Среднее время цикла по последним экземплярам (мин).</summary>
    public double AvgCycleTimeMinutes { get; set; }

    /// <summary>Целевое время цикла процесса (мин).</summary>
    public double TargetCycleTimeMinutes { get; set; }

    /// <summary>Процент превышения относительно целевого значения.</summary>
    public double ExceedPercent { get; set; }

    /// <summary>Когда алерт зафиксирован.</summary>
    public DateTimeOffset DetectedAt { get; set; }
}
