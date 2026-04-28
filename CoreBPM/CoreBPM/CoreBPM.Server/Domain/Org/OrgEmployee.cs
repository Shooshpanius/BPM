namespace CoreBPM.Server.Domain.Org;

/// <summary>
/// Сотрудник — связь пользователя с организацией (таблица org_employees).
/// Пара (UserId, OrganizationId) уникальна в рамках организации.
/// Должность и подразделение сотрудника определяются действующим назначением (OrgPositionAssignment).
/// Сотрудник считается действующим только при наличии активного назначения в этой организации.
/// </summary>
public class OrgEmployee
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на профиль пользователя (org_users).</summary>
    public Guid UserId { get; set; }

    /// <summary>Ссылка на организацию (org_organizations).</summary>
    public Guid OrganizationId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public OrgUser User { get; set; } = null!;
    public OrgOrganization Organization { get; set; } = null!;
}
