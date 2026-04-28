namespace CoreBPM.Server.Application.Admin.DTOs;

/// <summary>
/// DTO сотрудника организации.
/// Должность (positionId / positionName) и подразделение (departmentId / departmentName) вычисляются
/// из активного назначения (OrgPositionAssignment) — read-only.
/// </summary>
public class EmployeeDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public string UserWorkEmail { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>Идентификатор подразделения из активного назначения (read-only, нельзя задать напрямую).</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Название подразделения из активного назначения (read-only).</summary>
    public string? DepartmentName { get; set; }

    /// <summary>Идентификатор должности из активного назначения (read-only, нельзя задать напрямую).</summary>
    public Guid? PositionId { get; set; }

    /// <summary>Название должности из активного назначения (read-only).</summary>
    public string? PositionName { get; set; }

    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Запрос на создание сотрудника (привязка пользователя к организации).</summary>
public class CreateEmployeeRequest
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
}

/// <summary>Запрос на обновление данных сотрудника. Должность и подразделение задаются через /api/admin/assignments.</summary>
public class UpdateEmployeeRequest
{
    public bool IsActive { get; set; }
}
