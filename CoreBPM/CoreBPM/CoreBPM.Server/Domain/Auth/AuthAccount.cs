using CoreBPM.Server.Domain.Org;

namespace CoreBPM.Server.Domain.Auth;

/// <summary>Учётная запись для аутентификации (таблица auth_accounts).</summary>
public class AuthAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsLocked { get; set; } = false;
    public int FailedLoginCount { get; set; } = 0;
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset LastPasswordChangeAt { get; set; }
    public bool MustChangePassword { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public OrgUser User { get; set; } = null!;
    public ICollection<AuthSession> Sessions { get; set; } = new List<AuthSession>();
    public ICollection<AuthUserRole> UserRoles { get; set; } = new List<AuthUserRole>();
}
