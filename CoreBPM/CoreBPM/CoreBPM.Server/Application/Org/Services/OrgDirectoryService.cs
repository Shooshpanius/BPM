using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Org.Services;

/// <summary>Реализация сервиса адресной книги.</summary>
public class OrgDirectoryService : IOrgDirectoryService
{
    private readonly AppDbContext _db;

    public OrgDirectoryService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DirectoryOrganizationDto>> GetOrganizationsAsync(CancellationToken ct = default)
    {
        return await _db.OrgOrganizations
            .AsNoTracking()
            .Where(o => o.IsActive)
            .OrderBy(o => o.Name)
            .Select(o => new DirectoryOrganizationDto
            {
                Id = o.Id,
                Name = o.Name,
                EmployeesCount = o.Employees.Count(e => e.IsActive)
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DirectoryDepartmentTreeDto>> GetDepartmentTreeAsync(Guid organizationId, CancellationToken ct = default)
    {
        var all = await _db.OrgDepartments
            .AsNoTracking()
            .Where(d => d.OrganizationId == organizationId && d.Status == Domain.Org.DepartmentStatus.Active)
            .Select(d => new
            {
                d.Id,
                d.ParentId,
                d.Name,
                EmployeesCount = d.Employees.Count(e => e.IsActive)
            })
            .ToListAsync(ct);

        var lookup = all.ToDictionary(
            d => d.Id,
            d => new DirectoryDepartmentTreeDto
            {
                Id = d.Id,
                OrganizationId = organizationId,
                ParentId = d.ParentId,
                Name = d.Name,
                EmployeesCount = d.EmployeesCount,
                Children = new List<DirectoryDepartmentTreeDto>()
            });

        var roots = new List<DirectoryDepartmentTreeDto>();

        foreach (var dto in lookup.Values)
        {
            if (dto.ParentId.HasValue && lookup.TryGetValue(dto.ParentId.Value, out var parent))
                ((List<DirectoryDepartmentTreeDto>)parent.Children).Add(dto);
            else
                roots.Add(dto);
        }

        // Сортировка узлов по имени
        SortChildren(roots);

        return roots;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DirectoryEmployeeDto>> GetEmployeesAsync(
        Guid? organizationId,
        Guid? departmentId,
        string? search,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _db.OrgEmployees
            .AsNoTracking()
            .Include(e => e.User)
            .Include(e => e.Organization)
            .Include(e => e.Department)
            .Where(e => e.IsActive && e.User.IsActive)
            .AsQueryable();

        if (departmentId.HasValue)
            query = query.Where(e => e.DepartmentId == departmentId.Value);
        else if (organizationId.HasValue)
            query = query.Where(e => e.OrganizationId == organizationId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(e =>
                EF.Functions.ILike(e.User.DisplayName, pattern) ||
                EF.Functions.ILike(e.User.WorkEmail, pattern));
        }

        var employees = await query
            .OrderBy(e => e.User.LastName)
            .ThenBy(e => e.User.FirstName)
            .ToListAsync(ct);

        // Загружаем активные назначения для полученных сотрудников
        var userIds = employees.Select(e => e.UserId).Distinct().ToList();
        // Определяем organizationId для фильтрации назначений:
        // - если указан departmentId, берём организацию первого найденного сотрудника
        //   (все они в одном подразделении, значит и в одной организации)
        // - если departmentId не задан, используем переданный organizationId напрямую
        var filterOrgId = departmentId.HasValue
            ? employees.FirstOrDefault()?.OrganizationId
            : organizationId;

        // Загружаем только основные (IsPrimary = true) действующие назначения.
        // По бизнес-правилу у пользователя может быть не более одного активного основного назначения.
        var assignmentsQuery = _db.OrgPositionAssignments
            .AsNoTracking()
            .Include(a => a.Position)
            .Where(a => userIds.Contains(a.UserId) &&
                        a.IsPrimary &&
                        (a.EndDate == null || a.EndDate >= today));

        if (filterOrgId.HasValue)
            assignmentsQuery = assignmentsQuery.Where(a => a.OrganizationId == filterOrgId.Value);

        var assignments = await assignmentsQuery.ToListAsync(ct);

        // Ключ (UserId, OrganizationId): при глобально уникальном основном назначении
        // каждый пользователь даёт не более одной записи в словаре.
        var assignmentsByKey = assignments
            .ToDictionary(a => (a.UserId, a.OrganizationId));

        return employees.Select(e =>
        {
            assignmentsByKey.TryGetValue((e.UserId, e.OrganizationId), out var assignment);
            return new DirectoryEmployeeDto
            {
                Id = e.Id,
                UserId = e.UserId,
                DisplayName = e.User.DisplayName,
                FirstName = e.User.FirstName,
                LastName = e.User.LastName,
                MiddleName = e.User.MiddleName,
                WorkEmail = e.User.WorkEmail,
                Phone = e.User.Phone,
                AvatarUrl = e.User.AvatarUrl,
                Position = assignment?.Position?.Name,
                OrganizationId = e.OrganizationId,
                OrganizationName = e.Organization.Name,
                DepartmentId = e.DepartmentId,
                DepartmentName = e.Department?.Name
            };
        }).ToList();
    }

    private static void SortChildren(List<DirectoryDepartmentTreeDto> nodes)
    {
        nodes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        foreach (var node in nodes)
            SortChildren((List<DirectoryDepartmentTreeDto>)node.Children);
    }
}
