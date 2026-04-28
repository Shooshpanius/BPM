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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await _db.OrgOrganizations
            .AsNoTracking()
            .Where(o => o.IsActive)
            .OrderBy(o => o.Name)
            .Select(o => new DirectoryOrganizationDto
            {
                Id = o.Id,
                Name = o.Name,
                // Активный сотрудник = есть действующее назначение в организации
                EmployeesCount = _db.OrgPositionAssignments
                    .Where(a => a.OrganizationId == o.Id &&
                                a.StartDate <= today &&
                                (a.EndDate == null || a.EndDate >= today))
                    .Select(a => a.UserId)
                    .Distinct()
                    .Count()
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DirectoryDepartmentTreeDto>> GetDepartmentTreeAsync(Guid organizationId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Число сотрудников в подразделении = число уникальных пользователей с активным назначением.
        // Эффективное подразделение назначения: сначала a.DepartmentId (явно на назначении),
        // затем a.Position.DepartmentId (через должность).
        var deptCounts = await _db.OrgPositionAssignments
            .AsNoTracking()
            .Where(a => a.OrganizationId == organizationId &&
                        (a.DepartmentId.HasValue || a.Position.DepartmentId.HasValue) &&
                        a.StartDate <= today &&
                        (a.EndDate == null || a.EndDate >= today))
            .GroupBy(a => a.DepartmentId ?? a.Position.DepartmentId)
            .Where(g => g.Key.HasValue)
            .Select(g => new { DeptId = g.Key!.Value, Count = g.Select(a => a.UserId).Distinct().Count() })
            .ToDictionaryAsync(x => x.DeptId, x => x.Count, ct);

        var all = await _db.OrgDepartments
            .AsNoTracking()
            .Where(d => d.OrganizationId == organizationId && d.Status == Domain.Org.DepartmentStatus.Active)
            .Select(d => new
            {
                d.Id,
                d.ParentId,
                d.Name
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
                EmployeesCount = deptCounts.GetValueOrDefault(d.Id, 0),
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

        // Базовый запрос: только пользователи с активными профилями
        var query = _db.OrgEmployees
            .AsNoTracking()
            .Include(e => e.User)
            .Include(e => e.Organization)
            .Where(e => e.User.IsActive)
            .AsQueryable();

        if (departmentId.HasValue)
        {
            // Только сотрудники с активным назначением в данное подразделение.
            // Эффективное подразделение: сначала a.DepartmentId (явно на назначении),
            // затем a.Position.DepartmentId (через должность).
            query = query.Where(e => _db.OrgPositionAssignments.Any(a =>
                a.UserId == e.UserId &&
                a.OrganizationId == e.OrganizationId &&
                (a.DepartmentId == departmentId.Value ||
                 (a.DepartmentId == null && a.Position.DepartmentId == departmentId.Value)) &&
                a.StartDate <= today &&
                (a.EndDate == null || a.EndDate >= today)));
        }
        else if (organizationId.HasValue)
        {
            // Только сотрудники организации с хотя бы одним активным назначением
            query = query.Where(e => e.OrganizationId == organizationId.Value &&
                _db.OrgPositionAssignments.Any(a =>
                    a.UserId == e.UserId &&
                    a.OrganizationId == e.OrganizationId &&
                    a.StartDate <= today &&
                    (a.EndDate == null || a.EndDate >= today)));
        }
        else
        {
            // Без фильтра по организации — только сотрудники с хотя бы одним активным назначением
            query = query.Where(e => _db.OrgPositionAssignments.Any(a =>
                a.UserId == e.UserId &&
                a.OrganizationId == e.OrganizationId &&
                a.StartDate <= today &&
                (a.EndDate == null || a.EndDate >= today)));
        }

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

        // Загружаем все действующие назначения.
        // Включаем как Department самого назначения, так и Department должности —
        // чтобы корректно заполнить эффективное подразделение в DTO.
        // Приоритет выбора для карточки:
        // 1) основное назначение (если есть),
        // 2) иначе самое позднее действующее на текущую дату.
        var assignmentsQuery = _db.OrgPositionAssignments
            .AsNoTracking()
            .Include(a => a.Position).ThenInclude(p => p.Department)
            .Include(a => a.Department)
            .Where(a => userIds.Contains(a.UserId) &&
                        a.StartDate <= today &&
                        (a.EndDate == null || a.EndDate >= today));

        if (filterOrgId.HasValue)
            assignmentsQuery = assignmentsQuery.Where(a => a.OrganizationId == filterOrgId.Value);

        var assignments = await assignmentsQuery.ToListAsync(ct);

        // Ключ (UserId, OrganizationId): выбираем одно назначение для карточки
        // по приоритету "основное -> самое позднее действующее".
        var assignmentsByKey = assignments
            .GroupBy(a => (a.UserId, a.OrganizationId))
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(a => a.IsPrimary)
                    .ThenByDescending(a => a.StartDate)
                    .ThenByDescending(a => a.CreatedAt)
                    .First());

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
                DepartmentId = assignment?.DepartmentId ?? assignment?.Position?.DepartmentId,
                DepartmentName = assignment?.Department?.Name ?? assignment?.Position?.Department?.Name
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
