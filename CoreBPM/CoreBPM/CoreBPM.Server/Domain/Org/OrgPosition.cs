namespace CoreBPM.Server.Domain.Org;

/// <summary>
/// Должность в подразделении организации (таблица org_positions).
/// </summary>
public class OrgPosition
{
    public Guid Id { get; set; }

    /// <summary>Организация, к которой относится должность.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Наименование должности.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Уникальный код должности в рамках подразделения.</summary>
    public string? Code { get; set; }

    /// <summary>Описание должности.</summary>
    public string? Description { get; set; }

    /// <summary>Подразделение, к которому относится должность (необязательно).</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Категория должности (руководящая / рядовая / проектная).</summary>
    public PositionCategory Category { get; set; } = PositionCategory.Regular;

    /// <summary>Статус должности (активная / архивная).</summary>
    public PositionStatus Status { get; set; } = PositionStatus.Active;

    /// <summary>Плановое число ставок по должности.</summary>
    public decimal PlannedHeadcount { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Признак мягкого удаления.</summary>
    public bool IsDeleted { get; set; }

    // Навигационные свойства
    public OrgOrganization? Organization { get; set; }
    public OrgDepartment? Department { get; set; }
    public ICollection<OrgPositionAttachment> Attachments { get; set; } = new List<OrgPositionAttachment>();
    public ICollection<OrgPositionRoleMapping> RoleMappings { get; set; } = new List<OrgPositionRoleMapping>();
    public ICollection<OrgPositionAssignment> Assignments { get; set; } = new List<OrgPositionAssignment>();
}
