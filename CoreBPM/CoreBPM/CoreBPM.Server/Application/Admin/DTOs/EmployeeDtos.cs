namespace CoreBPM.Server.Application.Admin.DTOs;

/// <summary>DTO сотрудника организации.</summary>
public class EmployeeDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public string UserWorkEmail { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string? Position { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Запрос на создание сотрудника (привязка пользователя к организации).</summary>
public class CreateEmployeeRequest
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
    public string? Position { get; set; }
}

/// <summary>Запрос на обновление данных сотрудника.</summary>
public class UpdateEmployeeRequest
{
    public string? Position { get; set; }
    public bool IsActive { get; set; }
}
