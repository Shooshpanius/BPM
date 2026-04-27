namespace CoreBPM.Server.Application.Admin.DTOs;

/// <summary>DTO пользователя для отображения в списке (системном).</summary>
public class AdminUserListItemDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string WorkEmail { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public string? Username { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Запрос на создание пользователя (профиль + учётная запись).</summary>
public class CreateUserRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string WorkEmail { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>Запрос на обновление профиля пользователя.</summary>
public class UpdateUserRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string WorkEmail { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
}
