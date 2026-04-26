namespace CoreBPM.Server.Application.Org.DTOs;

/// <summary>DTO профиля пользователя.</summary>
public class UserDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string WorkEmail { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; }
}
