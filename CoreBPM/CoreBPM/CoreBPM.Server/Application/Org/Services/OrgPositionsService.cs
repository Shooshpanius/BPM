using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;
using CoreBPM.Server.Domain.Org;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Org.Services;

/// <summary>Реализация сервиса управления должностями.</summary>
public class OrgPositionsService : IOrgPositionsService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public OrgPositionsService(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PositionResponse>> GetPositionsAsync(
        Guid? departmentId = null,
        Guid? organizationId = null,
        PositionCategory? category = null,
        PositionStatus? status = null,
        CancellationToken ct = default)
    {
        var query = _db.OrgPositions
            .AsNoTracking()
            .Include(p => p.Department)
            .Include(p => p.RoleMappings)
            .Include(p => p.Attachments)
            .AsQueryable();

        if (departmentId.HasValue)
            query = query.Where(p => p.DepartmentId == departmentId.Value);

        if (organizationId.HasValue)
            query = query.Where(p => p.OrganizationId == organizationId.Value);

        if (category.HasValue)
            query = query.Where(p => p.Category == category.Value);

        // По умолчанию показываем только активные; если явно передан статус — фильтруем по нему
        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);
        else
            query = query.Where(p => p.Status == PositionStatus.Active);

        var positions = await query
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return positions.Select(MapToResponse).ToList();
    }

    /// <inheritdoc />
    public async Task<PositionResponse> GetPositionByIdAsync(Guid positionId, CancellationToken ct = default)
    {
        var position = await _db.OrgPositions
            .AsNoTracking()
            .Include(p => p.Department)
            .Include(p => p.RoleMappings)
            .Include(p => p.Attachments)
            .FirstOrDefaultAsync(p => p.Id == positionId, ct)
            ?? throw new NotFoundException($"Должность {positionId} не найдена");

        return MapToResponse(position);
    }

    /// <inheritdoc />
    public async Task<PositionResponse> CreatePositionAsync(CreatePositionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Наименование должности обязательно");

        if (request.PlannedHeadcount <= 0)
            throw new ValidationException("Плановое число ставок должно быть больше нуля");

        var orgExists = await _db.OrgOrganizations.AnyAsync(o => o.Id == request.OrganizationId, ct);
        if (!orgExists)
            throw new NotFoundException($"Организация {request.OrganizationId} не найдена");

        if (request.DepartmentId.HasValue)
        {
            var dept = await _db.OrgDepartments
                .FirstOrDefaultAsync(d => d.Id == request.DepartmentId.Value, ct);
            if (dept == null)
                throw new NotFoundException($"Подразделение {request.DepartmentId.Value} не найдено");
            if (dept.OrganizationId != request.OrganizationId)
                throw new ValidationException("Подразделение не принадлежит указанной организации");
        }

        await ValidateCodeUniqueAsync(request.OrganizationId, request.Code, null, ct);

        var now = DateTimeOffset.UtcNow;
        var position = new OrgPosition
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            Name = request.Name.Trim(),
            Code = request.Code?.Trim(),
            Description = request.Description?.Trim(),
            DepartmentId = request.DepartmentId,
            Category = request.Category,
            Status = PositionStatus.Active,
            PlannedHeadcount = request.PlannedHeadcount,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.OrgPositions.Add(position);
        await _db.SaveChangesAsync(ct);

        return await GetPositionByIdAsync(position.Id, ct);
    }

    /// <inheritdoc />
    public async Task<PositionResponse> UpdatePositionAsync(Guid positionId, UpdatePositionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Наименование должности обязательно");

        if (request.PlannedHeadcount <= 0)
            throw new ValidationException("Плановое число ставок должно быть больше нуля");

        var position = await _db.OrgPositions.FindAsync(new object[] { positionId }, ct)
            ?? throw new NotFoundException($"Должность {positionId} не найдена");

        if (request.DepartmentId.HasValue)
        {
            var dept = await _db.OrgDepartments
                .FirstOrDefaultAsync(d => d.Id == request.DepartmentId.Value, ct);
            if (dept == null)
                throw new NotFoundException($"Подразделение {request.DepartmentId.Value} не найдено");
            if (dept.OrganizationId != position.OrganizationId)
                throw new ValidationException("Подразделение не принадлежит организации этой должности");
        }

        await ValidateCodeUniqueAsync(position.OrganizationId, request.Code, positionId, ct);

        position.Name = request.Name.Trim();
        position.Code = request.Code?.Trim();
        position.Description = request.Description?.Trim();
        position.DepartmentId = request.DepartmentId;
        position.Category = request.Category;
        position.Status = request.Status;
        position.PlannedHeadcount = request.PlannedHeadcount;
        position.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetPositionByIdAsync(positionId, ct);
    }

    /// <inheritdoc />
    public async Task ArchivePositionAsync(Guid positionId, CancellationToken ct = default)
    {
        var position = await _db.OrgPositions.FindAsync(new object[] { positionId }, ct)
            ?? throw new NotFoundException($"Должность {positionId} не найдена");

        if (position.Status == PositionStatus.Archived)
            throw new ValidationException("Должность уже архивирована");

        // TODO FR-ORG-01.3: проверить отсутствие действующих назначений на эту должность

        position.Status = PositionStatus.Archived;
        position.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PositionRoleMappingResponse>> GetRoleMappingsAsync(Guid positionId, CancellationToken ct = default)
    {
        var positionExists = await _db.OrgPositions.AnyAsync(p => p.Id == positionId, ct);
        if (!positionExists)
            throw new NotFoundException($"Должность {positionId} не найдена");

        return await _db.OrgPositionRoleMappings
            .AsNoTracking()
            .Where(r => r.PositionId == positionId)
            .OrderBy(r => r.RoleName)
            .Select(r => new PositionRoleMappingResponse
            {
                Id = r.Id,
                PositionId = r.PositionId,
                RoleName = r.RoleName,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PositionRoleMappingResponse>> SetRoleMappingsAsync(
        Guid positionId,
        SetPositionRoleMappingsRequest request,
        CancellationToken ct = default)
    {
        var positionExists = await _db.OrgPositions.AnyAsync(p => p.Id == positionId, ct);
        if (!positionExists)
            throw new NotFoundException($"Должность {positionId} не найдена");

        // Валидация: имена ролей не должны быть пустыми
        var roleNames = request.RoleNames
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Удаляем все существующие привязки
        var existing = await _db.OrgPositionRoleMappings
            .Where(r => r.PositionId == positionId)
            .ToListAsync(ct);
        _db.OrgPositionRoleMappings.RemoveRange(existing);

        // Добавляем новые
        var now = DateTimeOffset.UtcNow;
        foreach (var roleName in roleNames)
        {
            _db.OrgPositionRoleMappings.Add(new OrgPositionRoleMapping
            {
                Id = Guid.NewGuid(),
                PositionId = positionId,
                RoleName = roleName,
                CreatedAt = now
            });
        }

        await _db.SaveChangesAsync(ct);

        return await GetRoleMappingsAsync(positionId, ct);
    }

    /// <inheritdoc />
    public async Task<PositionAttachmentResponse> AddAttachmentAsync(
        Guid positionId,
        IFormFile file,
        string? description,
        CancellationToken ct = default)
    {
        var positionExists = await _db.OrgPositions.AnyAsync(p => p.Id == positionId, ct);
        if (!positionExists)
            throw new NotFoundException($"Должность {positionId} не найдена");

        if (file == null || file.Length == 0)
            throw new ValidationException("Файл не может быть пустым");

        // Сохраняем файл в uploads/positions/{positionId}/
        var uploadsRoot = Path.Combine(_env.ContentRootPath, "uploads", "positions", positionId.ToString());
        Directory.CreateDirectory(uploadsRoot);

        var safeFileName = Path.GetFileName(file.FileName);
        var uniqueName = $"{Guid.NewGuid()}_{safeFileName}";
        var fullPath = Path.Combine(uploadsRoot, uniqueName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
            await file.CopyToAsync(stream, ct);

        // Относительный путь для хранения в БД
        var relativePath = Path.Combine("uploads", "positions", positionId.ToString(), uniqueName);

        var now = DateTimeOffset.UtcNow;
        var attachment = new OrgPositionAttachment
        {
            Id = Guid.NewGuid(),
            PositionId = positionId,
            FileName = safeFileName,
            ContentType = file.ContentType,
            FilePath = relativePath,
            Description = description?.Trim(),
            CreatedAt = now
        };

        _db.OrgPositionAttachments.Add(attachment);
        await _db.SaveChangesAsync(ct);

        return new PositionAttachmentResponse
        {
            Id = attachment.Id,
            PositionId = attachment.PositionId,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            Description = attachment.Description,
            CreatedAt = attachment.CreatedAt
        };
    }

    /// <inheritdoc />
    public async Task DeleteAttachmentAsync(Guid attachmentId, CancellationToken ct = default)
    {
        var attachment = await _db.OrgPositionAttachments.FindAsync(new object[] { attachmentId }, ct)
            ?? throw new NotFoundException($"Вложение {attachmentId} не найдено");

        // Удаляем физический файл (игнорируем ошибку, если файл уже отсутствует)
        var fullPath = Path.Combine(_env.ContentRootPath, attachment.FilePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        _db.OrgPositionAttachments.Remove(attachment);
        await _db.SaveChangesAsync(ct);
    }

    // ─── Вспомогательные методы ───

    private static PositionResponse MapToResponse(OrgPosition p) => new()
    {
        Id = p.Id,
        OrganizationId = p.OrganizationId,
        Name = p.Name,
        Code = p.Code,
        Description = p.Description,
        DepartmentId = p.DepartmentId,
        DepartmentName = p.Department?.Name,
        Category = p.Category,
        Status = p.Status,
        PlannedHeadcount = p.PlannedHeadcount,
        OccupiedHeadcount = 0, // будет рассчитываться при реализации FR-ORG-01.3
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
        RoleMappings = p.RoleMappings
            .OrderBy(r => r.RoleName)
            .Select(r => new PositionRoleMappingResponse
            {
                Id = r.Id,
                PositionId = r.PositionId,
                RoleName = r.RoleName,
                CreatedAt = r.CreatedAt
            })
            .ToList(),
        Attachments = p.Attachments
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new PositionAttachmentResponse
            {
                Id = a.Id,
                PositionId = a.PositionId,
                FileName = a.FileName,
                ContentType = a.ContentType,
                Description = a.Description,
                CreatedAt = a.CreatedAt
            })
            .ToList()
    };

    private async Task ValidateCodeUniqueAsync(Guid organizationId, string? code, Guid? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return;

        var trimmed = code.Trim();
        var exists = await _db.OrgPositions.AnyAsync(p =>
            p.OrganizationId == organizationId &&
            p.Code == trimmed &&
            (excludeId == null || p.Id != excludeId.Value), ct);

        if (exists)
            throw new ValidationException($"Должность с кодом '{trimmed}' уже существует в этой организации");
    }
}
