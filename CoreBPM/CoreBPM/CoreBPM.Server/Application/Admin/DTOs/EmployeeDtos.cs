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
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? Position { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Запрос на создание сотрудника (привязка пользователя к организации и подразделению).</summary>
public class CreateEmployeeRequest
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }

    /// <summary>Подразделение организации, в которое зачисляется сотрудник. Обязательно.</summary>
    public Guid DepartmentId { get; set; }

    public string? Position { get; set; }
}

/// <summary>Запрос на обновление данных сотрудника.</summary>
public class UpdateEmployeeRequest
{
    /// <summary>Новое подразделение. Должно принадлежать той же организации.</summary>
    public Guid DepartmentId { get; set; }

    public string? Position { get; set; }
    public bool IsActive { get; set; }
}
