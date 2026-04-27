namespace CoreBPM.Server.Application.Org.DTOs;

/// <summary>Организация в адресной книге.</summary>
public class DirectoryOrganizationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EmployeesCount { get; set; }
}

/// <summary>Узел дерева подразделений в адресной книге.</summary>
public class DirectoryDepartmentTreeDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EmployeesCount { get; set; }
    public IReadOnlyList<DirectoryDepartmentTreeDto> Children { get; set; } = [];
}

/// <summary>Карточка сотрудника в адресной книге.</summary>
public class DirectoryEmployeeDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? MiddleName { get; set; }
    public string WorkEmail { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Position { get; set; }
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
}
