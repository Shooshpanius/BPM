using CoreBPM.Server.Domain.Org;

namespace CoreBPM.Server.Application.Org.DTOs;

/// <summary>Элемент breadcrumb (хлебной крошки) подразделения.</summary>
public class BreadcrumbItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Полное DTO подразделения с breadcrumb и счётчиками сотрудников.</summary>
public class OrgUnitDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public DepartmentStatus Status { get; set; }
    public string Path { get; set; } = string.Empty;

    /// <summary>Хлебные крошки от корня до текущего узла включительно.</summary>
    public IReadOnlyList<BreadcrumbItemDto> Breadcrumb { get; set; } = [];

    /// <summary>Количество прямых сотрудников (только в этом подразделении).</summary>
    public int DirectEmployeesCount { get; set; }

    /// <summary>Суммарное количество сотрудников, включая все вложенные подразделения.</summary>
    public int TotalEmployeesCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>DTO узла дерева подразделений.</summary>
public class OrgUnitTreeDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public DepartmentStatus Status { get; set; }
    public string Path { get; set; } = string.Empty;

    /// <summary>Количество прямых сотрудников.</summary>
    public int DirectEmployeesCount { get; set; }

    /// <summary>Суммарное количество сотрудников с учётом вложенности.</summary>
    public int TotalEmployeesCount { get; set; }

    public IReadOnlyList<OrgUnitTreeDto> Children { get; set; } = [];
}

/// <summary>Запрос на создание подразделения.</summary>
public class CreateUnitRequest
{
    /// <summary>Идентификатор организации.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Родительское подразделение. Null — корневое.</summary>
    public Guid? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
}

/// <summary>Запрос на обновление подразделения (Path не обновляется, используйте MoveUnit).</summary>
public class UpdateUnitRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public DepartmentStatus Status { get; set; }
}

/// <summary>Запрос на перемещение подразделения в новый родительский узел.</summary>
public class MoveUnitRequest
{
    /// <summary>Новый родительский узел. Null — перемещает на корневой уровень.</summary>
    public Guid? NewParentId { get; set; }
}

/// <summary>DTO записи истории изменений подразделения.</summary>
public class UnitHistoryDto
{
    public Guid Id { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? ChangedByUserName { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public DepartmentChangeType ChangeType { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
