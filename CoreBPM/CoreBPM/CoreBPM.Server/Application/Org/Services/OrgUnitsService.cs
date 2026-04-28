using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;
using CoreBPM.Server.Domain.Org;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Org.Services;

/// <summary>Реализация сервиса управления деревом подразделений.</summary>
public class OrgUnitsService : IOrgUnitsService
{
    private readonly AppDbContext _db;

    public OrgUnitsService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrgUnitTreeDto>> GetTreeAsync(
        Guid organizationId,
        DepartmentStatus? status = null,
        string? search = null,
        CancellationToken ct = default)
    {
        var orgExists = await _db.OrgOrganizations.AnyAsync(o => o.Id == organizationId, ct);
        if (!orgExists)
            throw new NotFoundException($"Организация {organizationId} не найдена");

        var query = _db.OrgDepartments
            .AsNoTracking()
            .Where(d => d.OrganizationId == organizationId);

        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var allRaw = await query
            .Select(d => new
            {
                d.Id,
                d.ParentId,
                d.Name,
                d.ShortName,
                d.Code,
                d.Description,
                d.Status,
                d.Path,
                DirectEmployeesCount = _db.OrgPositionAssignments
                    .Where(a => a.Position.DepartmentId == d.Id &&
                                a.StartDate <= today &&
                                (a.EndDate == null || a.EndDate >= today))
                    .Select(a => a.UserId)
                    .Distinct()
                    .Count()
            })
            .ToListAsync(ct);

        // Находим узлы, совпадающие с поиском
        HashSet<Guid>? matchIds = null;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = search.Trim().ToLowerInvariant();
            matchIds = allRaw
                .Where(d =>
                    d.Name.ToLowerInvariant().Contains(pattern) ||
                    (d.Code != null && d.Code.ToLowerInvariant().Contains(pattern)))
                .Select(d => d.Id)
                .ToHashSet();
        }

        // Строим DTO-словарь
        var lookup = allRaw.ToDictionary(
            d => d.Id,
            d => new OrgUnitTreeDto
            {
                Id = d.Id,
                OrganizationId = organizationId,
                ParentId = d.ParentId,
                Name = d.Name,
                ShortName = d.ShortName,
                Code = d.Code,
                Description = d.Description,
                Status = d.Status,
                Path = d.Path,
                DirectEmployeesCount = d.DirectEmployeesCount,
                Children = new List<OrgUnitTreeDto>()
            });

        // Вычисляем TotalEmployeesCount рекурсивно по Path (потомки — те, чей Path начинается с нашего Path + "/")
        foreach (var node in lookup.Values)
        {
            var prefix = node.Path + "/";
            node.TotalEmployeesCount = node.DirectEmployeesCount +
                allRaw.Where(d => d.Path.StartsWith(prefix)).Sum(d => d.DirectEmployeesCount);
        }

        // Если есть поиск — определяем множество узлов для отображения (сами совпавшие + все их предки)
        HashSet<Guid>? displayIds = null;
        if (matchIds != null)
        {
            displayIds = new HashSet<Guid>(matchIds);
            foreach (var matchId in matchIds)
            {
                if (!lookup.TryGetValue(matchId, out var matchNode)) continue;
                // Добавляем всех предков через Path
                var segments = matchNode.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                foreach (var seg in segments)
                {
                    if (Guid.TryParse(seg, out var ancestorId))
                        displayIds.Add(ancestorId);
                }
            }
        }

        // Строим дерево
        var roots = new List<OrgUnitTreeDto>();

        foreach (var dto in lookup.Values)
        {
            if (displayIds != null && !displayIds.Contains(dto.Id))
                continue;

            if (dto.ParentId.HasValue && lookup.TryGetValue(dto.ParentId.Value, out var parent)
                && (displayIds == null || displayIds.Contains(parent.Id)))
                ((List<OrgUnitTreeDto>)parent.Children).Add(dto);
            else
                roots.Add(dto);
        }

        // Фильтруем дочерние узлы, которые не должны отображаться
        if (displayIds != null)
            FilterChildren(roots, displayIds);

        SortNodes(roots);
        return roots;
    }

    /// <inheritdoc />
    public async Task<OrgUnitDto> GetByIdAsync(Guid unitId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var dept = await _db.OrgDepartments
            .AsNoTracking()
            .Where(d => d.Id == unitId)
            .Select(d => new
            {
                d.Id,
                d.OrganizationId,
                d.ParentId,
                d.Name,
                d.ShortName,
                d.Code,
                d.Description,
                d.Status,
                d.Path,
                d.CreatedAt,
                d.UpdatedAt,
                DirectEmployeesCount = _db.OrgPositionAssignments
                    .Where(a => a.Position.DepartmentId == d.Id &&
                                a.StartDate <= today &&
                                (a.EndDate == null || a.EndDate >= today))
                    .Select(a => a.UserId)
                    .Distinct()
                    .Count()
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Подразделение {unitId} не найдено");

        // Подсчёт всех сотрудников с учётом вложенности через Path
        var prefix = dept.Path + "/";
        var totalEmployees = dept.DirectEmployeesCount + await _db.OrgDepartments
            .AsNoTracking()
            .Where(d => d.OrganizationId == dept.OrganizationId && d.Path.StartsWith(prefix))
            .SumAsync(d => _db.OrgPositionAssignments
                .Where(a => a.Position.DepartmentId == d.Id &&
                            a.StartDate <= today &&
                            (a.EndDate == null || a.EndDate >= today))
                .Select(a => a.UserId)
                .Distinct()
                .Count(), ct);

        // Формируем breadcrumb из Path
        var breadcrumb = await BuildBreadcrumbAsync(dept.Path, ct);

        return new OrgUnitDto
        {
            Id = dept.Id,
            OrganizationId = dept.OrganizationId,
            ParentId = dept.ParentId,
            Name = dept.Name,
            ShortName = dept.ShortName,
            Code = dept.Code,
            Description = dept.Description,
            Status = dept.Status,
            Path = dept.Path,
            Breadcrumb = breadcrumb,
            DirectEmployeesCount = dept.DirectEmployeesCount,
            TotalEmployeesCount = totalEmployees,
            CreatedAt = dept.CreatedAt,
            UpdatedAt = dept.UpdatedAt
        };
    }

    /// <inheritdoc />
    public async Task<OrgUnitDto> CreateAsync(CreateUnitRequest request, Guid? callerUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Наименование подразделения обязательно");

        var orgExists = await _db.OrgOrganizations.AnyAsync(o => o.Id == request.OrganizationId, ct);
        if (!orgExists)
            throw new NotFoundException($"Организация {request.OrganizationId} не найдена");

        string parentPath = string.Empty;
        if (request.ParentId.HasValue)
        {
            var parent = await _db.OrgDepartments.FindAsync(new object[] { request.ParentId.Value }, ct)
                ?? throw new NotFoundException($"Родительское подразделение {request.ParentId} не найдено");

            if (parent.OrganizationId != request.OrganizationId)
                throw new ValidationException("Родительское подразделение должно принадлежать той же организации");

            parentPath = parent.Path;
        }

        await ValidateCodeUniqueAsync(request.OrganizationId, request.Code, null, ct);

        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var dept = new OrgDepartment
        {
            Id = id,
            OrganizationId = request.OrganizationId,
            ParentId = request.ParentId,
            Name = request.Name.Trim(),
            ShortName = request.ShortName?.Trim(),
            Code = request.Code?.Trim(),
            Description = request.Description?.Trim(),
            Path = $"{parentPath}/{id}",
            Status = DepartmentStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.OrgDepartments.Add(dept);

        var newValueJson = JsonSerializer.Serialize(new { dept.Name, dept.ShortName, dept.Code, dept.Description });
        _db.OrgDepartmentHistories.Add(new OrgDepartmentHistory
        {
            Id = Guid.NewGuid(),
            DepartmentId = dept.Id,
            ChangedByUserId = callerUserId,
            ChangedAt = now,
            ChangeType = DepartmentChangeType.Created,
            OldValue = null,
            NewValue = newValueJson
        });

        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(dept.Id, ct);
    }

    /// <inheritdoc />
    public async Task<OrgUnitDto> UpdateAsync(Guid unitId, UpdateUnitRequest request, Guid? callerUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Наименование подразделения обязательно");

        var dept = await _db.OrgDepartments.FindAsync(new object[] { unitId }, ct)
            ?? throw new NotFoundException($"Подразделение {unitId} не найдено");

        await ValidateCodeUniqueAsync(dept.OrganizationId, request.Code, unitId, ct);

        var oldValueJson = JsonSerializer.Serialize(new { dept.Name, dept.ShortName, dept.Code, dept.Description, dept.Status });

        var wasArchived = dept.Status == DepartmentStatus.Archived;
        dept.Name = request.Name.Trim();
        dept.ShortName = request.ShortName?.Trim();
        dept.Code = request.Code?.Trim();
        dept.Description = request.Description?.Trim();
        dept.Status = request.Status;
        dept.UpdatedAt = DateTimeOffset.UtcNow;

        var newValueJson = JsonSerializer.Serialize(new { dept.Name, dept.ShortName, dept.Code, dept.Description, dept.Status });

        var changeType = (wasArchived && request.Status == DepartmentStatus.Active)
            ? DepartmentChangeType.Restored
            : (!wasArchived && request.Status == DepartmentStatus.Archived)
                ? DepartmentChangeType.Archived
                : DepartmentChangeType.Updated;

        _db.OrgDepartmentHistories.Add(new OrgDepartmentHistory
        {
            Id = Guid.NewGuid(),
            DepartmentId = unitId,
            ChangedByUserId = callerUserId,
            ChangedAt = dept.UpdatedAt,
            ChangeType = changeType,
            OldValue = oldValueJson,
            NewValue = newValueJson
        });

        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(unitId, ct);
    }

    /// <inheritdoc />
    public async Task ArchiveAsync(Guid unitId, Guid? callerUserId, CancellationToken ct = default)
    {
        var dept = await _db.OrgDepartments.FindAsync(new object[] { unitId }, ct)
            ?? throw new NotFoundException($"Подразделение {unitId} не найдено");

        if (dept.Status == DepartmentStatus.Archived)
            throw new ValidationException("Подразделение уже архивировано");

        var hasActiveChildren = await _db.OrgDepartments.AnyAsync(
            d => d.ParentId == unitId && d.Status == DepartmentStatus.Active, ct);
        if (hasActiveChildren)
            throw new ValidationException("Нельзя архивировать подразделение с активными дочерними подразделениями");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var hasEmployees = await _db.OrgPositionAssignments
            .AnyAsync(a => a.Position.DepartmentId == unitId &&
                           a.StartDate <= today &&
                           (a.EndDate == null || a.EndDate >= today), ct);
        if (hasEmployees)
            throw new ValidationException("Нельзя архивировать подразделение с активными сотрудниками");

        var oldValueJson = JsonSerializer.Serialize(new { dept.Name, dept.Status });
        var now = DateTimeOffset.UtcNow;
        dept.Status = DepartmentStatus.Archived;
        dept.UpdatedAt = now;

        _db.OrgDepartmentHistories.Add(new OrgDepartmentHistory
        {
            Id = Guid.NewGuid(),
            DepartmentId = unitId,
            ChangedByUserId = callerUserId,
            ChangedAt = now,
            ChangeType = DepartmentChangeType.Archived,
            OldValue = oldValueJson,
            NewValue = JsonSerializer.Serialize(new { dept.Name, Status = DepartmentStatus.Archived })
        });

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<OrgUnitDto> MoveAsync(Guid unitId, MoveUnitRequest request, Guid? callerUserId, CancellationToken ct = default)
    {
        var dept = await _db.OrgDepartments.FindAsync(new object[] { unitId }, ct)
            ?? throw new NotFoundException($"Подразделение {unitId} не найдено");

        OrgDepartment? newParent = null;
        if (request.NewParentId.HasValue)
        {
            if (request.NewParentId.Value == unitId)
                throw new ValidationException("Подразделение не может быть родителем само себе");

            newParent = await _db.OrgDepartments.FindAsync(new object[] { request.NewParentId.Value }, ct)
                ?? throw new NotFoundException($"Целевое родительское подразделение {request.NewParentId} не найдено");

            if (newParent.OrganizationId != dept.OrganizationId)
                throw new ValidationException("Целевое подразделение должно принадлежать той же организации");

            // Запрет перемещения в своего потомка
            if (newParent.Path.StartsWith(dept.Path + "/") || newParent.Path == dept.Path)
                throw new ValidationException("Нельзя переместить подразделение в его собственного потомка");
        }

        var oldPath = dept.Path;

        // Вычисляем новый Path, используя уже загруженного newParent
        string newParentPath = newParent?.Path ?? string.Empty;
        var newPath = $"{newParentPath}/{unitId}";

        // Обновляем Path у всех потомков (у которых Path начинается с oldPath/)
        var oldPrefix = oldPath + "/";
        var newPrefix = newPath + "/";

        var descendants = await _db.OrgDepartments
            .Where(d => d.OrganizationId == dept.OrganizationId && d.Path.StartsWith(oldPrefix))
            .ToListAsync(ct);

        foreach (var desc in descendants)
            desc.Path = newPrefix + desc.Path[oldPrefix.Length..];

        var now = DateTimeOffset.UtcNow;
        var oldValueJson = JsonSerializer.Serialize(new { dept.ParentId, Path = oldPath });
        dept.ParentId = request.NewParentId;
        dept.Path = newPath;
        dept.UpdatedAt = now;

        _db.OrgDepartmentHistories.Add(new OrgDepartmentHistory
        {
            Id = Guid.NewGuid(),
            DepartmentId = unitId,
            ChangedByUserId = callerUserId,
            ChangedAt = now,
            ChangeType = DepartmentChangeType.Moved,
            OldValue = oldValueJson,
            NewValue = JsonSerializer.Serialize(new { ParentId = request.NewParentId, Path = newPath })
        });

        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(unitId, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UnitHistoryDto>> GetHistoryAsync(Guid unitId, CancellationToken ct = default)
    {
        var unitExists = await _db.OrgDepartments.AnyAsync(d => d.Id == unitId, ct);
        if (!unitExists)
            throw new NotFoundException($"Подразделение {unitId} не найдено");

        return await _db.OrgDepartmentHistories
            .AsNoTracking()
            .Where(h => h.DepartmentId == unitId)
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new UnitHistoryDto
            {
                Id = h.Id,
                DepartmentId = h.DepartmentId,
                ChangedByUserId = h.ChangedByUserId,
                ChangedByUserName = h.ChangedByUser != null ? h.ChangedByUser.DisplayName : null,
                ChangedAt = h.ChangedAt,
                ChangeType = h.ChangeType,
                OldValue = h.OldValue,
                NewValue = h.NewValue
            })
            .ToListAsync(ct);
    }

    // ─── Вспомогательные методы ───

    private async Task<IReadOnlyList<BreadcrumbItemDto>> BuildBreadcrumbAsync(string path, CancellationToken ct)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return [];

        var ids = segments.Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
                          .Where(g => g.HasValue)
                          .Select(g => g!.Value)
                          .ToList();

        var names = await _db.OrgDepartments
            .AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .Select(d => new { d.Id, d.Name })
            .ToListAsync(ct);

        var nameMap = names.ToDictionary(n => n.Id, n => n.Name);

        return ids
            .Where(id => nameMap.ContainsKey(id))
            .Select(id => new BreadcrumbItemDto { Id = id, Name = nameMap[id] })
            .ToList();
    }

    private async Task ValidateCodeUniqueAsync(Guid organizationId, string? code, Guid? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return;

        var trimmed = code.Trim();
        var exists = await _db.OrgDepartments.AnyAsync(d =>
            d.OrganizationId == organizationId &&
            d.Code == trimmed &&
            (excludeId == null || d.Id != excludeId.Value), ct);

        if (exists)
            throw new ValidationException($"Подразделение с кодом '{trimmed}' уже существует в этой организации");
    }

    private static void FilterChildren(List<OrgUnitTreeDto> nodes, HashSet<Guid> displayIds)
    {
        foreach (var node in nodes)
        {
            var children = (List<OrgUnitTreeDto>)node.Children;
            children.RemoveAll(c => !displayIds.Contains(c.Id));
            FilterChildren(children, displayIds);
        }
    }

    private static void SortNodes(List<OrgUnitTreeDto> nodes)
    {
        nodes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        foreach (var node in nodes)
            SortNodes((List<OrgUnitTreeDto>)node.Children);
    }
}
