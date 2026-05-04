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
    /// Возвращает страницу сотрудников с поддержкой фильтрации, сортировки и пагинации.
    /// </summary>
    Task<DirectoryEmployeesPagedDto> GetEmployeesAsync(
        Guid? organizationId,
        Guid? departmentId,
        string? search,
        string? position = null,
        string? sortBy = null,
        string? sortDir = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Возвращает всех сотрудников для экспорта (без пагинации).
    /// </summary>
    Task<IReadOnlyList<DirectoryEmployeeDto>> GetEmployeesForExportAsync(
        Guid? organizationId,
        Guid? departmentId,
        string? search,
        string? position = null,
        string? sortBy = null,
        CancellationToken ct = default);
}
