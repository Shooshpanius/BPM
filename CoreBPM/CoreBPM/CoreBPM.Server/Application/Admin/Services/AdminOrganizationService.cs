using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Admin.DTOs;
using CoreBPM.Server.Application.Admin.Interfaces;
using CoreBPM.Server.Domain.Org;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Admin.Services;

/// <summary>Сервис управления организациями.</summary>
public class AdminOrganizationService : IAdminOrganizationService
{
    private readonly AppDbContext _db;

    public AdminOrganizationService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrganizationDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.OrgOrganizations
            .AsNoTracking()
            .OrderBy(o => o.Name)
            .Select(o => new OrganizationDto
            {
                Id = o.Id,
                Name = o.Name,
                Description = o.Description,
                IsPrimary = o.IsPrimary,
                IsActive = o.IsActive,
                EmployeesCount = o.Employees.Count(e => e.IsActive),
                CreatedAt = o.CreatedAt
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<OrganizationDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var org = await _db.OrgOrganizations
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new OrganizationDto
            {
                Id = o.Id,
                Name = o.Name,
                Description = o.Description,
                IsPrimary = o.IsPrimary,
                IsActive = o.IsActive,
                EmployeesCount = o.Employees.Count(e => e.IsActive),
                CreatedAt = o.CreatedAt
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Организация {id} не найдена");

        return org;
    }

    /// <inheritdoc />
    public async Task<OrganizationDto> CreateAsync(CreateOrganizationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Наименование организации обязательно");

        // Если запрашивается установка основной — снимаем флаг с других
        if (request.IsPrimary)
            await ClearPrimaryFlagAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var org = new OrgOrganization
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsPrimary = request.IsPrimary,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.OrgOrganizations.Add(org);
        await _db.SaveChangesAsync(ct);

        return MapToDto(org, 0);
    }

    /// <inheritdoc />
    public async Task<OrganizationDto> UpdateAsync(Guid id, UpdateOrganizationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Наименование организации обязательно");

        var org = await _db.OrgOrganizations.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException($"Организация {id} не найдена");

        // Если устанавливается основной флаг — снимаем с других
        if (request.IsPrimary && !org.IsPrimary)
            await ClearPrimaryFlagAsync(ct);

        org.Name = request.Name.Trim();
        org.Description = request.Description?.Trim();
        org.IsPrimary = request.IsPrimary;
        org.IsActive = request.IsActive;
        org.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        var employeesCount = await _db.OrgEmployees.CountAsync(e => e.OrganizationId == id && e.IsActive, ct);
        return MapToDto(org, employeesCount);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var org = await _db.OrgOrganizations.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException($"Организация {id} не найдена");

        var hasActiveEmployees = await _db.OrgEmployees
            .AnyAsync(e => e.OrganizationId == id && e.IsActive, ct);

        if (hasActiveEmployees)
            throw new ValidationException("Невозможно удалить организацию с активными сотрудниками");

        _db.OrgOrganizations.Remove(org);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task SetPrimaryAsync(Guid id, CancellationToken ct = default)
    {
        var org = await _db.OrgOrganizations.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException($"Организация {id} не найдена");

        if (!org.IsActive)
            throw new ValidationException("Нельзя назначить основной неактивную организацию");

        await ClearPrimaryFlagAsync(ct);

        org.IsPrimary = true;
        org.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Снимает флаг IsPrimary со всех организаций.</summary>
    private async Task ClearPrimaryFlagAsync(CancellationToken ct)
    {
        await _db.OrgOrganizations
            .Where(o => o.IsPrimary)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.IsPrimary, false)
                .SetProperty(o => o.UpdatedAt, DateTimeOffset.UtcNow), ct);
    }

    private static OrganizationDto MapToDto(OrgOrganization org, int employeesCount) => new()
    {
        Id = org.Id,
        Name = org.Name,
        Description = org.Description,
        IsPrimary = org.IsPrimary,
        IsActive = org.IsActive,
        EmployeesCount = employeesCount,
        CreatedAt = org.CreatedAt
    };
}
