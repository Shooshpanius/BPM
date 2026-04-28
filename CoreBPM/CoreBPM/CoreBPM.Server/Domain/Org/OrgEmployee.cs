namespace CoreBPM.Server.Domain.Org;

/// <summary>
/// Сотрудник — связь пользователя с организацией (таблица org_employees).
/// Пара (UserId, OrganizationId) уникальна в рамках организации.
/// Должность сотрудника определяется действующим назначением (OrgPositionAssignment).
/// </summary>
public class OrgEmployee
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на профиль пользователя (org_users).</summary>
    public Guid UserId { get; set; }

    /// <summary>Ссылка на организацию (org_organizations).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Подразделение организации, в котором состоит сотрудник. Может быть null, если подразделение не назначено.</summary>
    public Guid? DepartmentId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public OrgUser User { get; set; } = null!;
    public OrgOrganization Organization { get; set; } = null!;
    public OrgDepartment? Department { get; set; }
}
