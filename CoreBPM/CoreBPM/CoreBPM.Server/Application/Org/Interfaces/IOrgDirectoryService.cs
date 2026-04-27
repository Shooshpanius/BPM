using CoreBPM.Server.Application.Org.DTOs;

namespace CoreBPM.Server.Application.Org.Interfaces;

/// <summary>Сервис адресной книги — read-only представление оргструктуры для всех аутентифицированных пользователей.</summary>
public interface IOrgDirectoryService
{
    /// <summary>Возвращает список активных организаций.</summary>
    Task<IReadOnlyList<DirectoryOrganizationDto>> GetOrganizationsAsync(CancellationToken ct = default);

    /// <summary>Возвращает иерархическое дерево подразделений указанной организации.</summary>
    Task<IReadOnlyList<DirectoryDepartmentTreeDto>> GetDepartmentTreeAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Возвращает список сотрудников.
    /// Если передан <paramref name="departmentId"/> — фильтрует по подразделению.
    /// Если передан <paramref name="organizationId"/> — фильтрует по организации.
    /// Если передан <paramref name="search"/> — текстовый поиск по имени, email, должности.
    /// </summary>
    Task<IReadOnlyList<DirectoryEmployeeDto>> GetEmployeesAsync(
        Guid? organizationId,
        Guid? departmentId,
        string? search,
        CancellationToken ct = default);
}
