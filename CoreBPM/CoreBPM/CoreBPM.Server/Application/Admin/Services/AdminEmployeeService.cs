using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Admin.DTOs;
using CoreBPM.Server.Application.Admin.Interfaces;
using CoreBPM.Server.Domain.Org;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Admin.Services;

/// <summary>Сервис управления сотрудниками организаций.</summary>
public class AdminEmployeeService : IAdminEmployeeService
{
    private readonly AppDbContext _db;

    public AdminEmployeeService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmployeeDto>> GetAllAsync(Guid? organizationId = null, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _db.OrgEmployees
            .AsNoTracking()
            .Include(e => e.User)
            .Include(e => e.Organization)
            .Include(e => e.Department)
            .AsQueryable();

        if (organizationId.HasValue)
            query = query.Where(e => e.OrganizationId == organizationId.Value);

        var employees = await query
            .OrderBy(e => e.Organization.Name)
            .ThenBy(e => e.User.LastName)
            .ThenBy(e => e.User.FirstName)
            .ToListAsync(ct);

        // Загружаем активные назначения для всех сотрудников одним запросом
        var userIds = employees.Select(e => e.UserId).Distinct().ToList();
        var assignments = await _db.OrgPositionAssignments
            .AsNoTracking()
            .Include(a => a.Position)
            .Where(a => userIds.Contains(a.UserId) &&
                        (organizationId == null || a.OrganizationId == organizationId) &&
                        (a.EndDate == null || a.EndDate >= today))
            .ToListAsync(ct);

        // Индексируем по UserId + OrganizationId для быстрого поиска
        var assignmentsByKey = assignments
            .GroupBy(a => (a.UserId, a.OrganizationId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.IsPrimary).First());

        return employees.Select(e =>
        {
            assignmentsByKey.TryGetValue((e.UserId, e.OrganizationId), out var assignment);
            return MapToDto(e, assignment);
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmployeeDto>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var employees = await _db.OrgEmployees
            .AsNoTracking()
            .Include(e => e.User)
            .Include(e => e.Organization)
            .Include(e => e.Department)
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.Organization.Name)
            .ToListAsync(ct);

        var orgIds = employees.Select(e => e.OrganizationId).ToList();
        var assignments = await _db.OrgPositionAssignments
            .AsNoTracking()
            .Include(a => a.Position)
            .Where(a => a.UserId == userId &&
                        orgIds.Contains(a.OrganizationId) &&
                        (a.EndDate == null || a.EndDate >= today))
            .ToListAsync(ct);

        var assignmentsByOrg = assignments
            .GroupBy(a => a.OrganizationId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.IsPrimary).First());

        return employees.Select(e =>
        {
            assignmentsByOrg.TryGetValue(e.OrganizationId, out var assignment);
            return MapToDto(e, assignment);
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, CancellationToken ct = default)
    {
        // Проверяем существование пользователя
        var userExists = await _db.OrgUsers.AnyAsync(u => u.Id == request.UserId, ct);
        if (!userExists)
            throw new NotFoundException($"Пользователь {request.UserId} не найден");

        // Проверяем существование организации
        var orgExists = await _db.OrgOrganizations.AnyAsync(o => o.Id == request.OrganizationId, ct);
        if (!orgExists)
            throw new NotFoundException($"Организация {request.OrganizationId} не найдена");

        // Проверяем существование подразделения и его принадлежность к организации
        var dept = await _db.OrgDepartments.FindAsync(new object[] { request.DepartmentId }, ct)
            ?? throw new NotFoundException($"Подразделение {request.DepartmentId} не найдено");

        if (dept.OrganizationId != request.OrganizationId)
            throw new ValidationException("Подразделение не принадлежит указанной организации");

        // Проверяем уникальность пары пользователь–организация
        var alreadyExists = await _db.OrgEmployees
            .AnyAsync(e => e.UserId == request.UserId && e.OrganizationId == request.OrganizationId, ct);
        if (alreadyExists)
            throw new ValidationException("Данный пользователь уже является сотрудником этой организации");

        var now = DateTimeOffset.UtcNow;
        var employee = new OrgEmployee
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            OrganizationId = request.OrganizationId,
            DepartmentId = request.DepartmentId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.OrgEmployees.Add(employee);
        await _db.SaveChangesAsync(ct);

        // Загружаем навигационные свойства для ответа
        await _db.Entry(employee).Reference(e => e.User).LoadAsync(ct);
        await _db.Entry(employee).Reference(e => e.Organization).LoadAsync(ct);
        await _db.Entry(employee).Reference(e => e.Department).LoadAsync(ct);

        return MapToDto(employee, null);
    }

    /// <inheritdoc />
    public async Task<EmployeeDto> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var employee = await _db.OrgEmployees
            .Include(e => e.User)
            .Include(e => e.Organization)
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException($"Сотрудник {id} не найден");

        // Проверяем новое подразделение
        var dept = await _db.OrgDepartments.FindAsync(new object[] { request.DepartmentId }, ct)
            ?? throw new NotFoundException($"Подразделение {request.DepartmentId} не найдено");

        if (dept.OrganizationId != employee.OrganizationId)
            throw new ValidationException("Подразделение не принадлежит организации сотрудника");

        employee.DepartmentId = request.DepartmentId;
        employee.IsActive = request.IsActive;
        employee.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Обновляем навигационное свойство после изменения FK
        await _db.Entry(employee).Reference(e => e.Department).LoadAsync(ct);

        // Получаем текущее активное назначение для ответа
        var assignment = await _db.OrgPositionAssignments
            .AsNoTracking()
            .Include(a => a.Position)
            .Where(a => a.UserId == employee.UserId &&
                        a.OrganizationId == employee.OrganizationId &&
                        (a.EndDate == null || a.EndDate >= today))
            .OrderByDescending(a => a.IsPrimary)
            .FirstOrDefaultAsync(ct);

        return MapToDto(employee, assignment);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var employee = await _db.OrgEmployees.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException($"Сотрудник {id} не найден");

        _db.OrgEmployees.Remove(employee);
        await _db.SaveChangesAsync(ct);
    }

    private static EmployeeDto MapToDto(OrgEmployee e, OrgPositionAssignment? assignment) => new()
    {
        Id = e.Id,
        UserId = e.UserId,
        UserDisplayName = e.User.DisplayName,
        UserWorkEmail = e.User.WorkEmail,
        OrganizationId = e.OrganizationId,
        OrganizationName = e.Organization.Name,
        DepartmentId = e.DepartmentId,
        DepartmentName = e.Department?.Name,
        PositionId = assignment?.PositionId,
        PositionName = assignment?.Position?.Name,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt
    };
}
