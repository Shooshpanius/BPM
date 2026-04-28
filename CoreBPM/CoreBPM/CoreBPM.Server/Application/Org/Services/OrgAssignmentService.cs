using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;
using CoreBPM.Server.Domain.Auth;
using CoreBPM.Server.Domain.Org;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Org.Services;

/// <summary>Реализация сервиса назначений пользователей на должности (FR-ORG-01.3).</summary>
public class OrgAssignmentService : IOrgAssignmentService
{
    private static readonly decimal[] AllowedRates = [0.25m, 0.5m, 0.75m, 1.0m];

    private readonly AppDbContext _db;

    public OrgAssignmentService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssignmentDto>> GetAllAsync(
        Guid? userId = null,
        Guid? positionId = null,
        Guid? organizationId = null,
        bool? activeOnly = null,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _db.OrgPositionAssignments
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Position).ThenInclude(p => p.Department)
            .Include(a => a.Organization)
            .AsQueryable();

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        if (positionId.HasValue)
            query = query.Where(a => a.PositionId == positionId.Value);

        if (organizationId.HasValue)
            query = query.Where(a => a.OrganizationId == organizationId.Value);

        if (activeOnly == true)
            query = query.Where(a => a.EndDate == null || a.EndDate >= today);

        return await query
            .OrderByDescending(a => a.StartDate)
            .ThenBy(a => a.User.LastName)
            .ThenBy(a => a.User.FirstName)
            .Select(a => MapToDto(a, today))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<AssignmentDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var assignment = await _db.OrgPositionAssignments
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Position).ThenInclude(p => p.Department)
            .Include(a => a.Organization)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException($"Назначение {id} не найдено");

        return MapToDto(assignment, today);
    }

    /// <inheritdoc />
    public async Task<AssignmentDto> CreateAsync(CreateAssignmentRequest request, CancellationToken ct = default)
    {
        ValidateRate(request.Rate);

        if (request.EndDate.HasValue && request.EndDate.Value < request.StartDate)
            throw new ValidationException("Дата окончания не может быть раньше даты начала");

        // Проверяем пользователя
        var userExists = await _db.OrgUsers.AnyAsync(u => u.Id == request.UserId, ct);
        if (!userExists)
            throw new NotFoundException($"Пользователь {request.UserId} не найден");

        // Проверяем должность и получаем OrganizationId
        var position = await _db.OrgPositions
            .Include(p => p.RoleMappings)
            .FirstOrDefaultAsync(p => p.Id == request.PositionId, ct)
            ?? throw new NotFoundException($"Должность {request.PositionId} не найдена");

        if (position.Status == PositionStatus.Archived)
            throw new ValidationException("Нельзя назначить пользователя на архивированную должность");

        // Проверяем дублирование активного назначения на ту же должность
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var duplicate = await _db.OrgPositionAssignments.AnyAsync(a =>
            a.UserId == request.UserId &&
            a.PositionId == request.PositionId &&
            (a.EndDate == null || a.EndDate >= today), ct);

        if (duplicate)
            throw new ValidationException("У пользователя уже есть активное назначение на эту должность");

        // Если назначение основное — проверяем, нет ли другого активного основного
        if (request.IsPrimary)
        {
            var hasActivePrimary = await _db.OrgPositionAssignments.AnyAsync(a =>
                a.UserId == request.UserId &&
                a.IsPrimary &&
                (a.EndDate == null || a.EndDate >= today), ct);

            if (hasActivePrimary)
                throw new ValidationException(
                    "У пользователя уже есть активное основное назначение. " +
                    "Завершите его или создайте назначение как совмещение (IsPrimary = false).");
        }

        var now = DateTimeOffset.UtcNow;
        var assignment = new OrgPositionAssignment
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            PositionId = request.PositionId,
            OrganizationId = position.OrganizationId,
            Rate = request.Rate,
            IsPrimary = request.IsPrimary,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.OrgPositionAssignments.Add(assignment);

        // Применяем матрицу ролей должности
        await ApplyRoleMappingsAsync(request.UserId, position.RoleMappings, ct);

        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(assignment.Id, ct);
    }

    /// <inheritdoc />
    public async Task<AssignmentDto> UpdateAsync(Guid id, UpdateAssignmentRequest request, CancellationToken ct = default)
    {
        ValidateRate(request.Rate);

        if (request.EndDate.HasValue && request.EndDate.Value < request.StartDate)
            throw new ValidationException("Дата окончания не может быть раньше даты начала");

        var assignment = await _db.OrgPositionAssignments
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException($"Назначение {id} не найдено");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Если меняем тип на основное — проверяем, нет ли другого активного основного
        if (request.IsPrimary && !assignment.IsPrimary)
        {
            var hasOtherActivePrimary = await _db.OrgPositionAssignments.AnyAsync(a =>
                a.UserId == assignment.UserId &&
                a.IsPrimary &&
                a.Id != id &&
                (a.EndDate == null || a.EndDate >= today), ct);

            if (hasOtherActivePrimary)
                throw new ValidationException(
                    "У пользователя уже есть другое активное основное назначение. " +
                    "Сначала завершите его.");
        }

        assignment.Rate = request.Rate;
        assignment.IsPrimary = request.IsPrimary;
        assignment.StartDate = request.StartDate;
        assignment.EndDate = request.EndDate;
        assignment.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var assignment = await _db.OrgPositionAssignments
            .Include(a => a.Position).ThenInclude(p => p.RoleMappings)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException($"Назначение {id} не найдено");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Если назначение ещё активно — устанавливаем дату окончания на сегодня
        if (assignment.EndDate == null || assignment.EndDate >= today)
        {
            assignment.EndDate = today;
            assignment.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // Снимаем роли, если больше нет других активных назначений с теми же ролями
        await RevokeStaleRolesAsync(assignment.UserId, assignment.PositionId,
            assignment.Position.RoleMappings, ct);

        await _db.SaveChangesAsync(ct);
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private static void ValidateRate(decimal rate)
    {
        if (!AllowedRates.Contains(rate))
            throw new ValidationException(
                $"Ставка {rate} недопустима. Допустимые значения: 0.25, 0.5, 0.75, 1.0");
    }

    /// <summary>
    /// Добавляет роли из матрицы должности на учётную запись пользователя,
    /// если соответствующие роли ещё не назначены.
    /// </summary>
    private async Task ApplyRoleMappingsAsync(
        Guid userId,
        IEnumerable<OrgPositionRoleMapping> roleMappings,
        CancellationToken ct)
    {
        var account = await _db.AuthAccounts
            .Include(a => a.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(a => a.UserId == userId, ct);

        // Если у пользователя нет учётной записи — пропускаем назначение ролей
        if (account == null) return;

        var existingRoleNames = account.UserRoles
            .Select(ur => ur.Role.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTimeOffset.UtcNow;

        foreach (var mapping in roleMappings)
        {
            if (existingRoleNames.Contains(mapping.RoleName)) continue;

            var role = await _db.AuthRoles
                .FirstOrDefaultAsync(r => r.Name == mapping.RoleName, ct);

            if (role == null) continue; // роль не существует в системе — пропускаем

            _db.AuthUserRoles.Add(new AuthUserRole
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                RoleId = role.Id,
                AssignedAt = now
            });
        }
    }

    /// <summary>
    /// Снимает роли должности с пользователя, если у него больше нет
    /// других активных назначений, предоставляющих те же роли.
    /// </summary>
    private async Task RevokeStaleRolesAsync(
        Guid userId,
        Guid terminatedPositionId,
        IEnumerable<OrgPositionRoleMapping> roleMappings,
        CancellationToken ct)
    {
        var account = await _db.AuthAccounts
            .Include(a => a.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(a => a.UserId == userId, ct);

        if (account == null) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Роли, которые всё ещё предоставляются через другие активные назначения
        var retainedRoles = await _db.OrgPositionAssignments
            .AsNoTracking()
            .Where(a =>
                a.UserId == userId &&
                a.PositionId != terminatedPositionId &&
                (a.EndDate == null || a.EndDate >= today))
            .Include(a => a.Position).ThenInclude(p => p.RoleMappings)
            .SelectMany(a => a.Position.RoleMappings.Select(rm => rm.RoleName))
            .Distinct()
            .ToListAsync(ct);

        var retainedSet = new HashSet<string>(retainedRoles, StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in roleMappings)
        {
            if (retainedSet.Contains(mapping.RoleName)) continue;

            var userRole = account.UserRoles
                .FirstOrDefault(ur => string.Equals(ur.Role.Name, mapping.RoleName,
                    StringComparison.OrdinalIgnoreCase));

            if (userRole != null)
                _db.AuthUserRoles.Remove(userRole);
        }
    }

    private static AssignmentDto MapToDto(OrgPositionAssignment a, DateOnly today) => new()
    {
        Id = a.Id,
        UserId = a.UserId,
        UserDisplayName = a.User.DisplayName,
        UserWorkEmail = a.User.WorkEmail,
        PositionId = a.PositionId,
        PositionName = a.Position.Name,
        OrganizationId = a.OrganizationId,
        OrganizationName = a.Organization.Name,
        DepartmentId = a.Position.DepartmentId,
        DepartmentName = a.Position.Department?.Name,
        Rate = a.Rate,
        IsPrimary = a.IsPrimary,
        StartDate = a.StartDate,
        EndDate = a.EndDate,
        IsActive = a.EndDate == null || a.EndDate >= today,
        CreatedAt = a.CreatedAt
    };
}
