namespace CoreBPM.Server.Application.Admin.DTOs;

/// <summary>
/// DTO сотрудника организации.
/// Должность (positionId / positionName) вычисляется из активного назначения (OrgPositionAssignment) — read-only.
/// </summary>
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

    /// <summary>Идентификатор должности из активного назначения (read-only, нельзя задать напрямую).</summary>
    public Guid? PositionId { get; set; }

    /// <summary>Название должности из активного назначения (read-only).</summary>
    public string? PositionName { get; set; }

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
}

/// <summary>Запрос на обновление данных сотрудника. Должность задаётся отдельно через /api/admin/assignments.</summary>
public class UpdateEmployeeRequest
{
    /// <summary>Новое подразделение. Должно принадлежать той же организации.</summary>
    public Guid DepartmentId { get; set; }

    public bool IsActive { get; set; }
}
