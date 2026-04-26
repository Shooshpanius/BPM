namespace CoreBPM.Server.Domain.Auth;

/// <summary>Связь пользователь–роль (таблица auth_user_roles).</summary>
public class AuthUserRole
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid RoleId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }

    // Навигационные свойства
    public AuthAccount Account { get; set; } = null!;
    public AuthRole Role { get; set; } = null!;
}
