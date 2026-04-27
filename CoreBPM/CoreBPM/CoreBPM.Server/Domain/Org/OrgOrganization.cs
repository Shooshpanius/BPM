namespace CoreBPM.Server.Domain.Org;

/// <summary>Организация (таблица org_organizations).</summary>
public class OrgOrganization
{
    public Guid Id { get; set; }

    /// <summary>Полное наименование организации.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Краткое описание или реквизиты организации.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Признак основной организации. В системе может быть только одна основная организация.
    /// Используется как умолчание при выборе организационного контекста.
    /// </summary>
    public bool IsPrimary { get; set; } = false;

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public ICollection<OrgEmployee> Employees { get; set; } = new List<OrgEmployee>();
    public ICollection<OrgDepartment> Departments { get; set; } = new List<OrgDepartment>();
}
