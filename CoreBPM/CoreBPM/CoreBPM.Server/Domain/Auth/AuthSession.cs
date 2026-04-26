namespace CoreBPM.Server.Domain.Auth;

/// <summary>Сессия пользователя — хранит хеш refresh-токена (таблица auth_sessions).</summary>
public class AuthSession
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string RefreshTokenHash { get; set; } = string.Empty;
    public string? DeviceInfo { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
    public bool IsRevoked { get; set; } = false;

    // Навигационное свойство
    public AuthAccount Account { get; set; } = null!;
}
