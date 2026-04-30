using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Глобальный реестр сигналов BPMN. Сигналы используются в Signal-событиях (throw/catch).
/// </summary>
[Table("bpm_signals")]
public class BpmSignal
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Идентификатор организации-владельца.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Отображаемое название сигнала.</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Код сигнала (используется в BPMN XML как signalRef.name).</summary>
    [Required, MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Необязательное описание.</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
