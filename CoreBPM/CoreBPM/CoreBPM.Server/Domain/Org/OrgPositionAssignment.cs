namespace CoreBPM.Server.Domain.Org;

/// <summary>
/// Назначение пользователя на должность (таблица org_position_assignments).
/// Поддерживает несколько назначений на разные должности (основное + совмещения),
/// ставку, даты начала/окончания и историю назначений.
/// </summary>
public class OrgPositionAssignment
{
    public Guid Id { get; set; }

    /// <summary>Пользователь, которому назначена должность.</summary>
    public Guid UserId { get; set; }

    /// <summary>Должность.</summary>
    public Guid PositionId { get; set; }

    /// <summary>Организация (денормализовано для удобства фильтрации).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Ставка занятости (0.25, 0.5, 0.75 или 1.0).</summary>
    public decimal Rate { get; set; } = 1.0m;

    /// <summary>Признак основного назначения (у пользователя может быть только одно активное основное).</summary>
    public bool IsPrimary { get; set; } = true;

    /// <summary>Дата начала действия назначения.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Дата окончания назначения. Если не задана — назначение бессрочное.</summary>
    public DateOnly? EndDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public OrgUser User { get; set; } = null!;
    public OrgPosition Position { get; set; } = null!;
    public OrgOrganization Organization { get; set; } = null!;
}
