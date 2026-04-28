using CoreBPM.Server.Domain.Org;

namespace CoreBPM.Server.Application.Admin.DTOs;

/// <summary>DTO подразделения (плоское представление).</summary>
public class DepartmentDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public DepartmentStatus Status { get; set; }

    /// <summary>Признак активного подразделения (Status == Active).</summary>
    public bool IsActive => Status == DepartmentStatus.Active;

    public int EmployeesCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>DTO подразделения с вложенными дочерними узлами (дерево).</summary>
public class DepartmentTreeDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public DepartmentStatus Status { get; set; }

    /// <summary>Признак активного подразделения (Status == Active).</summary>
    public bool IsActive => Status == DepartmentStatus.Active;

    public int EmployeesCount { get; set; }
    public IReadOnlyList<DepartmentTreeDto> Children { get; set; } = [];
}

/// <summary>Запрос на создание подразделения.</summary>
public class CreateDepartmentRequest
{
    /// <summary>Идентификатор организации, которой принадлежит подразделение.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Идентификатор родительского подразделения. Null — создаётся корневое подразделение.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Наименование подразделения.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Краткое название (аббревиатура).</summary>
    public string? ShortName { get; set; }

    /// <summary>Уникальный код в рамках организации.</summary>
    public string? Code { get; set; }

    public string? Description { get; set; }
}

/// <summary>Запрос на обновление данных подразделения.</summary>
public class UpdateDepartmentRequest
{
    /// <summary>Новый родитель. Null — перемещает подразделение на корневой уровень.</summary>
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public DepartmentStatus Status { get; set; }
}
