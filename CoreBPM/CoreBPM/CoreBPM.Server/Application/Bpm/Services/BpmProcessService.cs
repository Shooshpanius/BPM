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
    private readonly IBpmDocumentationService _documentation;

    public BpmProcessService(AppDbContext db, IBpmDocumentationService documentation)
    {
        _db = db;
        _documentation = documentation;
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

        var technicalNames = GenerateTechnicalNames(request.Name);
        var now = DateTimeOffset.UtcNow;
        var tagsJson = System.Text.Json.JsonSerializer.Serialize(request.Tags ?? Array.Empty<string>());
        var process = new BpmProcess
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            CreatedByUserId = createdByUserId,
            TagsJson = tagsJson,
            IsTemplate = request.IsTemplate,
            LaunchFromPortalEnabled = true,
            ShowInStartList = true,
            RequestInstanceNameOnStart = true,
            DataClassName = technicalNames.DataClassName,
            DataTableName = technicalNames.DataTableName,
            ProcessMetricsClassName = technicalNames.ProcessMetricsClassName,
            ProcessMetricsTableName = technicalNames.ProcessMetricsTableName,
            InstanceMetricsClassName = technicalNames.InstanceMetricsClassName,
            InstanceMetricsTableName = technicalNames.InstanceMetricsTableName,
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

        var oldName = process.Name;
        process.Name = request.Name.Trim();
        process.Description = request.Description?.Trim();
        process.TagsJson = System.Text.Json.JsonSerializer.Serialize(request.Tags ?? Array.Empty<string>());
        process.IsTemplate = request.IsTemplate;
        process.UpdatedAt = DateTimeOffset.UtcNow;

        if (string.Equals(process.DataClassName, GenerateTechnicalNames(oldName).DataClassName, StringComparison.Ordinal))
        {
            var technicalNames = GenerateTechnicalNames(process.Name);
            process.DataClassName = technicalNames.DataClassName;
            process.DataTableName = technicalNames.DataTableName;
            process.ProcessMetricsClassName = technicalNames.ProcessMetricsClassName;
            process.ProcessMetricsTableName = technicalNames.ProcessMetricsTableName;
            process.InstanceMetricsClassName = technicalNames.InstanceMetricsClassName;
            process.InstanceMetricsTableName = technicalNames.InstanceMetricsTableName;
        }

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
    public async Task<IReadOnlyList<BpmProcessListItemDto>> GetTemplatesAsync(Guid organizationId, CancellationToken ct = default)
    {
        var templates = await _db.BpmProcesses
            .AsNoTracking()
            .Where(p => p.OrganizationId == organizationId && p.IsTemplate && !p.IsDeleted)
            .Include(p => p.Versions)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return templates.Select(MapToListItem).ToList();
    }

    /// <inheritdoc />
    public async Task<BpmProcessDto> CreateFromTemplateAsync(Guid templateId, CreateProcessFromTemplateRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Название процесса обязательно");

        var template = await _db.BpmProcesses
            .AsNoTracking()
            .Include(p => p.Versions)
            .FirstOrDefaultAsync(p => p.Id == templateId && p.IsTemplate, ct)
            ?? throw new NotFoundException($"Шаблон {templateId} не найден");

        // Копируем активную версию шаблона или последний черновик
        var sourceVersion = template.Versions.FirstOrDefault(v => v.Status == BpmProcessVersionStatus.Active)
            ?? template.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        var orgExists = await _db.OrgOrganizations.AnyAsync(o => o.Id == request.OrganizationId, ct);
        if (!orgExists)
            throw new NotFoundException($"Организация {request.OrganizationId} не найдена");

        var technicalNames = GenerateTechnicalNames(request.Name);
        var now = DateTimeOffset.UtcNow;
        var process = new BpmProcess
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? template.Description,
            CreatedByUserId = createdByUserId,
            TagsJson = "[]",
            IsTemplate = false,
            LaunchFromPortalEnabled = template.LaunchFromPortalEnabled,
            ShowInStartList = template.ShowInStartList,
            RequestInstanceNameOnStart = template.RequestInstanceNameOnStart,
            InstanceNameMode = template.InstanceNameMode,
            InstanceNameTemplate = template.InstanceNameTemplate,
            DataClassName = technicalNames.DataClassName,
            DataTableName = technicalNames.DataTableName,
            ProcessMetricsClassName = technicalNames.ProcessMetricsClassName,
            ProcessMetricsTableName = technicalNames.ProcessMetricsTableName,
            InstanceMetricsClassName = technicalNames.InstanceMetricsClassName,
            InstanceMetricsTableName = technicalNames.InstanceMetricsTableName,
            CreatedAt = now,
            UpdatedAt = now
        };

        var initialDraft = new BpmProcessVersion
        {
            Id = Guid.NewGuid(),
            ProcessId = process.Id,
            VersionNumber = 1,
            Status = BpmProcessVersionStatus.Draft,
            DiagramXml = sourceVersion?.DiagramXml,
            CreatedByUserId = createdByUserId,
            ReleaseNotes = $"Создан из шаблона «{template.Name}»",
            CreatedAt = now,
            UpdatedAt = now
        };

        process.Versions.Add(initialDraft);
        _db.BpmProcesses.Add(process);
        await _db.SaveChangesAsync(ct);

        return await GetProcessByIdAsync(process.Id, ct);
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
                v.PublishedAt,
                v.ReleaseNotes))
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
        var tags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(p.TagsJson) ?? new List<string>();
        return new BpmProcessListItemDto(
            p.Id, p.OrganizationId, p.Name, p.Description,
            active?.VersionNumber,
            p.Versions.Count,
            p.CreatedAt, p.UpdatedAt,
            tags, p.IsTemplate);
    }

    private static BpmProcessDto MapToDto(BpmProcess p)
    {
        var active = p.Versions.FirstOrDefault(v => v.Status == BpmProcessVersionStatus.Active);
        var tags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(p.TagsJson) ?? new List<string>();
        return new BpmProcessDto(
            p.Id, p.OrganizationId, p.Name, p.Description, p.CreatedByUserId,
            active?.VersionNumber,
            p.Versions.Count,
            p.CreatedAt, p.UpdatedAt,
            tags, p.IsTemplate);
    }

    private static BpmDiagramDto MapVersionToDto(BpmProcessVersion v) => new(
        v.Id, v.VersionNumber, v.Status, v.DiagramXml, v.UpdatedAt, v.PublishedAt, v.ReleaseNotes);

    private static BpmProcessVersionInfoDto MapVersionInfo(BpmProcessVersion v) => new(
        v.Id, v.VersionNumber, v.Status, v.CreatedByUserId, v.CreatedAt, v.UpdatedAt, v.PublishedAt, v.ReleaseNotes);
}
