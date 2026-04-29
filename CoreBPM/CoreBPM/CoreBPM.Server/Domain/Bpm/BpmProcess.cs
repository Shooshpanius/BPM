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

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Мягкое удаление.</summary>
    public bool IsDeleted { get; set; } = false;

    // Навигационные свойства
    public ICollection<BpmProcessVersion> Versions { get; set; } = new List<BpmProcessVersion>();
}
