using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;
using CoreBPM.Server.Domain.Org;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Org.Services;

/// <summary>
/// Реализация сервиса визуализации оргструктуры (FR-ORG-01.4).
/// Объединяет данные из подразделений, должностей и назначений в единое дерево.
/// </summary>
public class OrgChartService : IOrgChartService
{
    private readonly AppDbContext _db;

    public OrgChartService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<OrgChartDto> GetChartAsync(
        Guid organizationId,
        string? search = null,
        bool extended = false,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Загружаем все активные подразделения организации
        var departments = await _db.OrgDepartments
            .AsNoTracking()
            .Where(d => d.OrganizationId == organizationId && d.Status == DepartmentStatus.Active)
            .Select(d => new { d.Id, d.ParentId, d.Name, d.ShortName })
            .ToListAsync(ct);

        // Загружаем все активные назначения с данными пользователей и должностей
        var assignments = await _db.OrgPositionAssignments
            .AsNoTracking()
            .Where(a => a.OrganizationId == organizationId &&
                        a.StartDate <= today &&
                        (a.EndDate == null || a.EndDate >= today))
            .Include(a => a.User)
            .Include(a => a.Position)
            .Include(a => a.Department)
            .ToListAsync(ct);

        // Для каждого пользователя выбираем одно назначение для карточки
        // (основное → последнее вступившее в силу)
        var assignmentsByUser = assignments
            .GroupBy(a => a.UserId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(a => a.IsPrimary)
                    .ThenByDescending(a => a.StartDate)
                    .ThenByDescending(a => a.CreatedAt)
                    .First());

        // Определяем эффективное подразделение каждого назначения:
        // явное DepartmentId на назначении → DepartmentId должности → null (без подразделения)
        var employeeCards = assignmentsByUser.Values
            .Select(a => new
            {
                Card = new OrgChartEmployeeDto
                {
                    UserId = a.UserId,
                    DisplayName = a.User.DisplayName,
                    AvatarUrl = a.User.AvatarUrl,
                    WorkEmail = a.User.WorkEmail,
                    Phone = a.User.Phone,
                    PositionId = a.PositionId,
                    PositionName = a.Position.Name,
                    PositionCategory = a.Position.Category,
                    Rate = extended ? a.Rate : null
                },
                DepartmentId = a.DepartmentId ?? a.Position.DepartmentId
            })
            .ToList();

        // Группируем сотрудников по подразделениям
        var employeesByDept = employeeCards
            .Where(x => x.DepartmentId.HasValue)
            .GroupBy(x => x.DepartmentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Card).ToList());

        var unassignedEmployees = employeeCards
            .Where(x => !x.DepartmentId.HasValue)
            .Select(x => x.Card)
            .ToList();

        // Вакансии (только при extended=true)
        Dictionary<Guid, List<OrgChartVacancyDto>> vacanciesByDept = new();
        if (extended)
        {
            var positions = await _db.OrgPositions
                .AsNoTracking()
                .Where(p => p.OrganizationId == organizationId &&
                            p.Status == PositionStatus.Active &&
                            p.DepartmentId.HasValue)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.DepartmentId,
                    p.PlannedHeadcount
                })
                .ToListAsync(ct);

            // Занятые ставки по должностям
            var occupiedByPosition = assignments
                .GroupBy(a => a.PositionId)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Rate));

            foreach (var pos in positions)
            {
                var occupied = occupiedByPosition.GetValueOrDefault(pos.Id, 0m);
                var vacancy = pos.PlannedHeadcount - occupied;
                if (vacancy <= 0 || !pos.DepartmentId.HasValue) continue;

                var deptId = pos.DepartmentId.Value;
                if (!vacanciesByDept.TryGetValue(deptId, out var list))
                {
                    list = new List<OrgChartVacancyDto>();
                    vacanciesByDept[deptId] = list;
                }

                list.Add(new OrgChartVacancyDto
                {
                    PositionId = pos.Id,
                    PositionName = pos.Name,
                    VacancyCount = vacancy
                });
            }
        }

        // Строим дерево подразделений
        var deptLookup = departments.ToDictionary(
            d => d.Id,
            d => new OrgChartNodeDto
            {
                Id = d.Id,
                Name = d.Name,
                ShortName = d.ShortName,
                Employees = employeesByDept.GetValueOrDefault(d.Id, []),
                Vacancies = vacanciesByDept.GetValueOrDefault(d.Id, []),
                Children = new List<OrgChartNodeDto>()
            });

        var roots = new List<OrgChartNodeDto>();

        foreach (var dept in departments)
        {
            if (dept.ParentId.HasValue && deptLookup.TryGetValue(dept.ParentId.Value, out var parent))
                ((List<OrgChartNodeDto>)parent.Children).Add(deptLookup[dept.Id]);
            else
                roots.Add(deptLookup[dept.Id]);
        }

        // Подсчёт сотрудников (включая дочерние узлы)
        foreach (var root in roots)
            ComputeTotalCounts(root);

        // Сортировка узлов и сотрудников
        SortTree(roots);
        SortEmployees(deptLookup.Values);

        // Применяем текстовый поиск
        List<OrgChartNodeDto> resultDepts;
        List<OrgChartEmployeeDto> resultUnassigned;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = search.Trim().ToUpperInvariant();
            resultDepts = FilterTree(roots, pattern);
            resultUnassigned = unassignedEmployees
                .Where(e => MatchesSearch(e, pattern))
                .ToList();
        }
        else
        {
            resultDepts = roots;
            resultUnassigned = unassignedEmployees;
        }

        return new OrgChartDto
        {
            Departments = resultDepts,
            UnassignedEmployees = resultUnassigned
        };
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private static int ComputeTotalCounts(OrgChartNodeDto node)
    {
        var total = node.Employees.Count;
        foreach (var child in node.Children)
            total += ComputeTotalCounts(child);
        node.TotalEmployeesCount = total;
        return total;
    }

    private static void SortTree(List<OrgChartNodeDto> nodes)
    {
        nodes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        foreach (var node in nodes)
            SortTree((List<OrgChartNodeDto>)node.Children);
    }

    private static void SortEmployees(IEnumerable<OrgChartNodeDto> nodes)
    {
        foreach (var node in nodes)
        {
            var sorted = node.Employees
                .OrderByDescending(e => e.PositionCategory == PositionCategory.Managerial)
                .ThenBy(e => e.DisplayName, StringComparer.CurrentCulture)
                .ToList();
            node.Employees = sorted;
        }
    }

    /// <summary>Рекурсивно фильтрует дерево по поисковому шаблону.</summary>
    private static List<OrgChartNodeDto> FilterTree(IEnumerable<OrgChartNodeDto> nodes, string pattern)
    {
        var result = new List<OrgChartNodeDto>();

        foreach (var node in nodes)
        {
            var deptMatches = node.Name.ToUpperInvariant().Contains(pattern);
            var filteredChildren = FilterTree(node.Children, pattern);
            var filteredEmployees = deptMatches
                ? node.Employees.ToList()
                : node.Employees.Where(e => MatchesSearch(e, pattern)).ToList();
            var filteredVacancies = deptMatches || filteredEmployees.Count > 0
                ? node.Vacancies.ToList()
                : node.Vacancies.Where(v => v.PositionName.ToUpperInvariant().Contains(pattern)).ToList();

            if (deptMatches || filteredChildren.Count > 0 || filteredEmployees.Count > 0 || filteredVacancies.Count > 0)
            {
                result.Add(new OrgChartNodeDto
                {
                    Id = node.Id,
                    Name = node.Name,
                    ShortName = node.ShortName,
                    TotalEmployeesCount = node.TotalEmployeesCount,
                    Employees = deptMatches ? node.Employees : filteredEmployees,
                    Vacancies = filteredVacancies,
                    Children = filteredChildren
                });
            }
        }

        return result;
    }

    private static bool MatchesSearch(OrgChartEmployeeDto e, string pattern)
        => e.DisplayName.ToUpperInvariant().Contains(pattern) ||
           (e.PositionName?.ToUpperInvariant().Contains(pattern) ?? false) ||
           (e.WorkEmail?.ToUpperInvariant().Contains(pattern) ?? false);
}
