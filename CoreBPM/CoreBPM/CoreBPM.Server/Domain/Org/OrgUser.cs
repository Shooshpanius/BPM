using CoreBPM.Server.Domain.Auth;

namespace CoreBPM.Server.Domain.Org;

/// <summary>Профиль пользователя в оргструктуре (таблица org_users).</summary>
public class OrgUser
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string WorkEmail { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? MobilePhone { get; set; }
    public string? InternalPhone { get; set; }
    public string? PersonalEmail { get; set; }
    public string? Bio { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string BirthDateVisibility { get; set; } = "all";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public AuthAccount? Account { get; set; }
    public ICollection<OrgEmployee> Employees { get; set; } = new List<OrgEmployee>();
}
