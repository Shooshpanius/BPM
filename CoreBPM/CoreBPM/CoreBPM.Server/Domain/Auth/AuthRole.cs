namespace CoreBPM.Server.Domain.Auth;

/// <summary>Роль пользователя (таблица auth_roles).</summary>
public class AuthRole
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Системные роли нельзя удалить.</summary>
    public bool IsSystem { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Навигационное свойство
    public ICollection<AuthUserRole> UserRoles { get; set; } = new List<AuthUserRole>();
}
