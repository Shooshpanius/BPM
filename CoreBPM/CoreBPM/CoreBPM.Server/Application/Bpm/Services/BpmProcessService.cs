using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Domain.Org;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса управления бизнес-процессами.</summary>
public partial class BpmProcessService : IBpmProcessService
{
    private readonly AppDbContext _db;

    public BpmProcessService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmProcessListItemDto>> GetProcessesAsync(Guid organizationId, CancellationToken ct = default)
    {
        var processes = await _db.BpmProcesses
            .AsNoTracking()
            .Where(p => p.OrganizationId == organizationId)
            .Include(p => p.Versions)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return processes.Select(MapToListItem).ToList();
    }

    /// <inheritdoc />
    public async Task<BpmProcessDto> GetProcessByIdAsync(Guid processId, CancellationToken ct = default)
    {
        var process = await _db.BpmProcesses
            .AsNoTracking()
            .Include(p => p.Versions)
            .FirstOrDefaultAsync(p => p.Id == processId, ct)
            ?? throw new NotFoundException($"Процесс {processId} не найден");

        return MapToDto(process);
    }

    /// <inheritdoc />
    public async Task<BpmProcessDto> CreateProcessAsync(CreateBpmProcessRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Название процесса обязательно");

        var orgExists = await _db.OrgOrganizations.AnyAsync(o => o.Id == request.OrganizationId, ct);
        if (!orgExists)
            throw new NotFoundException($"Организация {request.OrganizationId} не найдена");

        var now = DateTimeOffset.UtcNow;
        var process = new BpmProcess
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Создаём первый пустой черновик
        var initialDraft = new BpmProcessVersion
        {
            Id = Guid.NewGuid(),
            ProcessId = process.Id,
            VersionNumber = 1,
            Status = BpmProcessVersionStatus.Draft,
            DiagramXml = null,
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        process.Versions.Add(initialDraft);

        _db.BpmProcesses.Add(process);
        await _db.SaveChangesAsync(ct);

        return await GetProcessByIdAsync(process.Id, ct);
    }

    /// <inheritdoc />
    public async Task<BpmProcessDto> UpdateProcessAsync(Guid processId, UpdateBpmProcessRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Название процесса обязательно");

        var process = await _db.BpmProcesses.FindAsync(new object[] { processId }, ct)
            ?? throw new NotFoundException($"Процесс {processId} не найден");

        process.Name = request.Name.Trim();
        process.Description = request.Description?.Trim();
        process.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetProcessByIdAsync(processId, ct);
    }

    /// <inheritdoc />
    public async Task DeleteProcessAsync(Guid processId, CancellationToken ct = default)
    {
        var process = await _db.BpmProcesses.FindAsync(new object[] { processId }, ct)
            ?? throw new NotFoundException($"Процесс {processId} не найден");

        process.IsDeleted = true;
        process.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmProcessVersionInfoDto>> GetVersionsAsync(Guid processId, CancellationToken ct = default)
    {
        var processExists = await _db.BpmProcesses.AnyAsync(p => p.Id == processId, ct);
        if (!processExists)
            throw new NotFoundException($"Процесс {processId} не найден");

        return await _db.BpmProcessVersions
            .AsNoTracking()
            .Where(v => v.ProcessId == processId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new BpmProcessVersionInfoDto(
                v.Id,
                v.VersionNumber,
                v.Status,
                v.CreatedByUserId,
                v.CreatedAt,
                v.UpdatedAt,
                v.PublishedAt))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BpmDiagramDto> GetDiagramAsync(Guid processId, CancellationToken ct = default)
    {
        var processExists = await _db.BpmProcesses.AnyAsync(p => p.Id == processId, ct);
        if (!processExists)
            throw new NotFoundException($"Процесс {processId} не найден");

        // Возвращаем последний черновик; если его нет — активную версию
        var version = await _db.BpmProcessVersions
            .AsNoTracking()
            .Where(v => v.ProcessId == processId)
            .OrderBy(v => v.Status == BpmProcessVersionStatus.Draft ? 0 : 1)
            .ThenByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Процесс {processId} не имеет версий");

        return MapVersionToDto(version);
    }

    /// <inheritdoc />
    public async Task<BpmDiagramDto> SaveDiagramAsync(Guid processId, SaveDiagramRequest request, Guid savedByUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.DiagramXml))
            throw new ValidationException("XML диаграммы не может быть пустым");

        var processExists = await _db.BpmProcesses.AnyAsync(p => p.Id == processId, ct);
        if (!processExists)
            throw new NotFoundException($"Процесс {processId} не найден");

        var now = DateTimeOffset.UtcNow;
        var maxVersion = await _db.BpmProcessVersions
            .Where(v => v.ProcessId == processId)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;

        var draft = new BpmProcessVersion
        {
            Id = Guid.NewGuid(),
            ProcessId = processId,
            VersionNumber = maxVersion + 1,
            Status = BpmProcessVersionStatus.Draft,
            DiagramXml = request.DiagramXml,
            CreatedByUserId = savedByUserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.BpmProcessVersions.Add(draft);

        var process = await _db.BpmProcesses.FindAsync(new object[] { processId }, ct);
        if (process is not null)
            process.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        return MapVersionToDto(draft);
    }

    // ─── Маппинг ───

    private static BpmProcessListItemDto MapToListItem(BpmProcess p)
    {
        var active = p.Versions.FirstOrDefault(v => v.Status == BpmProcessVersionStatus.Active);
        return new BpmProcessListItemDto(
            p.Id, p.OrganizationId, p.Name, p.Description,
            active?.VersionNumber,
            p.Versions.Count,
            p.CreatedAt, p.UpdatedAt);
    }

    private static BpmProcessDto MapToDto(BpmProcess p)
    {
        var active = p.Versions.FirstOrDefault(v => v.Status == BpmProcessVersionStatus.Active);
        return new BpmProcessDto(
            p.Id, p.OrganizationId, p.Name, p.Description, p.CreatedByUserId,
            active?.VersionNumber,
            p.Versions.Count,
            p.CreatedAt, p.UpdatedAt);
    }

    private static BpmDiagramDto MapVersionToDto(BpmProcessVersion v) => new(
        v.Id, v.VersionNumber, v.Status, v.DiagramXml, v.UpdatedAt, v.PublishedAt);
}
