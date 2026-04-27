using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Admin.DTOs;
using CoreBPM.Server.Application.Admin.Interfaces;
using CoreBPM.Server.Domain.Org;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Admin.Services;

/// <summary>Сервис управления подразделениями организаций.</summary>
public class AdminDepartmentService : IAdminDepartmentService
{
    private readonly AppDbContext _db;

    public AdminDepartmentService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DepartmentDto>> GetAllAsync(Guid? organizationId = null, CancellationToken ct = default)
    {
        var query = _db.OrgDepartments
            .AsNoTracking()
            .Include(d => d.Organization)
            .Include(d => d.Parent)
            .AsQueryable();

        if (organizationId.HasValue)
            query = query.Where(d => d.OrganizationId == organizationId.Value);

        return await query
            .OrderBy(d => d.Organization.Name)
            .ThenBy(d => d.Name)
            .Select(d => new DepartmentDto
            {
                Id = d.Id,
                OrganizationId = d.OrganizationId,
                OrganizationName = d.Organization.Name,
                ParentId = d.ParentId,
                ParentName = d.Parent != null ? d.Parent.Name : null,
                Name = d.Name,
                Description = d.Description,
                IsActive = d.IsActive,
                EmployeesCount = d.Employees.Count(e => e.IsActive),
                CreatedAt = d.CreatedAt
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DepartmentTreeDto>> GetTreeAsync(Guid organizationId, CancellationToken ct = default)
    {
        var orgExists = await _db.OrgOrganizations.AnyAsync(o => o.Id == organizationId, ct);
        if (!orgExists)
            throw new NotFoundException($"Организация {organizationId} не найдена");

        // Загружаем все подразделения организации одним запросом
        var all = await _db.OrgDepartments
            .AsNoTracking()
            .Where(d => d.OrganizationId == organizationId)
            .Select(d => new
            {
                d.Id,
                d.ParentId,
                d.Name,
                d.Description,
                d.IsActive,
                EmployeesCount = d.Employees.Count(e => e.IsActive)
            })
            .ToListAsync(ct);

        // Строим дерево в памяти
        var lookup = all.ToDictionary(
            d => d.Id,
            d => new DepartmentTreeDto
            {
                Id = d.Id,
                OrganizationId = organizationId,
                ParentId = d.ParentId,
                Name = d.Name,
                Description = d.Description,
                IsActive = d.IsActive,
                EmployeesCount = d.EmployeesCount,
                Children = new List<DepartmentTreeDto>()
            });

        var roots = new List<DepartmentTreeDto>();

        foreach (var dto in lookup.Values)
        {
            if (dto.ParentId.HasValue && lookup.TryGetValue(dto.ParentId.Value, out var parent))
                ((List<DepartmentTreeDto>)parent.Children).Add(dto);
            else
                roots.Add(dto);
        }

        return roots;
    }

    /// <inheritdoc />
    public async Task<DepartmentDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dept = await _db.OrgDepartments
            .AsNoTracking()
            .Include(d => d.Organization)
            .Include(d => d.Parent)
            .Where(d => d.Id == id)
            .Select(d => new DepartmentDto
            {
                Id = d.Id,
                OrganizationId = d.OrganizationId,
                OrganizationName = d.Organization.Name,
                ParentId = d.ParentId,
                ParentName = d.Parent != null ? d.Parent.Name : null,
                Name = d.Name,
                Description = d.Description,
                IsActive = d.IsActive,
                EmployeesCount = d.Employees.Count(e => e.IsActive),
                CreatedAt = d.CreatedAt
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Подразделение {id} не найдено");

        return dept;
    }

    /// <inheritdoc />
    public async Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Наименование подразделения обязательно");

        var orgExists = await _db.OrgOrganizations.AnyAsync(o => o.Id == request.OrganizationId, ct);
        if (!orgExists)
            throw new NotFoundException($"Организация {request.OrganizationId} не найдена");

        if (request.ParentId.HasValue)
        {
            var parent = await _db.OrgDepartments.FindAsync(new object[] { request.ParentId.Value }, ct)
                ?? throw new NotFoundException($"Родительское подразделение {request.ParentId} не найдено");

            if (parent.OrganizationId != request.OrganizationId)
                throw new ValidationException("Родительское подразделение должно принадлежать той же организации");
        }

        var now = DateTimeOffset.UtcNow;
        var dept = new OrgDepartment
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            ParentId = request.ParentId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.OrgDepartments.Add(dept);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(dept.Id, ct);
    }

    /// <inheritdoc />
    public async Task<DepartmentDto> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Наименование подразделения обязательно");

        var dept = await _db.OrgDepartments.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException($"Подразделение {id} не найдено");

        if (request.ParentId.HasValue)
        {
            if (request.ParentId.Value == id)
                throw new ValidationException("Подразделение не может быть родителем само себе");

            var parent = await _db.OrgDepartments.FindAsync(new object[] { request.ParentId.Value }, ct)
                ?? throw new NotFoundException($"Родительское подразделение {request.ParentId} не найдено");

            if (parent.OrganizationId != dept.OrganizationId)
                throw new ValidationException("Родительское подразделение должно принадлежать той же организации");

            // Проверяем отсутствие циклических зависимостей
            if (await IsDescendantAsync(dept.OrganizationId, request.ParentId.Value, id, ct))
                throw new ValidationException("Нельзя назначить дочернее подразделение в качестве родителя");
        }

        dept.ParentId = request.ParentId;
        dept.Name = request.Name.Trim();
        dept.Description = request.Description?.Trim();
        dept.IsActive = request.IsActive;
        dept.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(dept.Id, ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var dept = await _db.OrgDepartments.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException($"Подразделение {id} не найдено");

        var hasChildren = await _db.OrgDepartments.AnyAsync(d => d.ParentId == id, ct);
        if (hasChildren)
            throw new ValidationException("Невозможно удалить подразделение, у которого есть дочерние подразделения");

        var hasEmployees = await _db.OrgEmployees.AnyAsync(e => e.DepartmentId == id && e.IsActive, ct);
        if (hasEmployees)
            throw new ValidationException("Невозможно удалить подразделение с активными сотрудниками");

        _db.OrgDepartments.Remove(dept);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Проверяет, является ли <paramref name="candidateAncestorId"/> потомком <paramref name="departmentId"/>
    /// в рамках одной организации. Используется для защиты от циклических зависимостей.
    /// </summary>
    private async Task<bool> IsDescendantAsync(Guid organizationId, Guid candidateAncestorId, Guid departmentId, CancellationToken ct)
    {
        // Загружаем все подразделения организации для обхода в памяти
        var allDepts = await _db.OrgDepartments
            .AsNoTracking()
            .Where(d => d.OrganizationId == organizationId)
            .Select(d => new { d.Id, d.ParentId })
            .ToListAsync(ct);

        var parentMap = allDepts.ToDictionary(d => d.Id, d => d.ParentId);

        // Поднимаемся по дереву от candidateAncestorId вверх к корню
        var visited = new HashSet<Guid>();
        var current = candidateAncestorId;

        while (parentMap.TryGetValue(current, out var parentId))
        {
            if (current == departmentId)
                return true;
            // Защита от повреждённых данных с циклами в БД
            if (!visited.Add(current))
                break;
            if (parentId is null)
                break;
            current = parentId.Value;
        }

        return false;
    }
}
