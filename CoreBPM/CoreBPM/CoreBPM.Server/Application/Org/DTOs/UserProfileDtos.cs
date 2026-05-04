namespace CoreBPM.Server.Application.Org.DTOs;

/// <summary>Полный профиль пользователя.</summary>
public class UserProfileDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string WorkEmail { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? MobilePhone { get; set; }
    public string? InternalPhone { get; set; }
    public string? PersonalEmail { get; set; }
    public string? Bio { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string BirthDateVisibility { get; set; } = "all";
    public string? AvatarUrl { get; set; }
    public string? Position { get; set; }
    public string? Department { get; set; }
    public string? Organization { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Запрос обновления профиля пользователя.</summary>
public class UpdateProfileRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? MiddleName { get; set; }
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string? MobilePhone { get; set; }
    public string? InternalPhone { get; set; }
    public string? PersonalEmail { get; set; }
    public string? Bio { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? BirthDateVisibility { get; set; }
}
