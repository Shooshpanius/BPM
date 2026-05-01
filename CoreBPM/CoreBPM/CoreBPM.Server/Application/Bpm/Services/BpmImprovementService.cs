using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса предложений по улучшению бизнес-процессов (FR-BPM-03.1).</summary>
public class BpmImprovementService : IBpmImprovementService
{
    private readonly AppDbContext _db;

    public BpmImprovementService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<ImprovementDto> CreateAsync(
        Guid processId,
        CreateImprovementRequest request,
        Guid initiatorId,
        CancellationToken ct = default)
    {
        var process = await _db.BpmProcesses.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == processId, ct)
            ?? throw new NotFoundException($"Процесс {processId} не найден");

        var now = DateTimeOffset.UtcNow;
        var improvement = new BpmImprovement
        {
            Id = Guid.NewGuid(),
            ProcessId = processId,
            Subject = request.Subject.Trim(),
            Description = request.Description?.Trim(),
            Status = BpmImprovementStatus.Pending,
            InitiatorUserId = initiatorId,
            SourceInstanceId = request.SourceInstanceId,
            SourceTaskElementId = request.SourceTaskElementId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.BpmImprovements.Add(improvement);
        await _db.SaveChangesAsync(ct);

        return await BuildDtoAsync(improvement, process.Name, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ImprovementDto>> ListByProcessAsync(
        Guid processId,
        CancellationToken ct = default)
    {
        var process = await _db.BpmProcesses.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == processId, ct)
            ?? throw new NotFoundException($"Процесс {processId} не найден");

        var improvements = await _db.BpmImprovements.AsNoTracking()
            .Where(i => i.ProcessId == processId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        return await MapToDtosAsync(improvements, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ImprovementDto>> ListAsync(
        Guid userId,
        bool isAdmin,
        string role,
        Guid? processId = null,
        BpmImprovementStatus? status = null,
        Guid? authorId = null,
        DateTimeOffset? dateFrom = null,
        DateTimeOffset? dateTo = null,
        CancellationToken ct = default)
    {
        var query = _db.BpmImprovements.AsNoTracking().AsQueryable();

        // Фильтрация по роли пользователя
        if (!isAdmin)
        {
            query = role switch
            {
                "My" => query.Where(i => i.InitiatorUserId == userId),
                "Current" => query.Where(i =>
                    i.Status == BpmImprovementStatus.Pending ||
                    i.Status == BpmImprovementStatus.Accepted ||
                    i.Status == BpmImprovementStatus.InProgress),
                _ => query.Where(i =>
                    i.InitiatorUserId == userId ||
                    i.AssignedUserId == userId)
            };
        }
        else if (role == "My")
        {
            query = query.Where(i => i.InitiatorUserId == userId);
        }
        else if (role == "Current")
        {
            query = query.Where(i =>
                i.Status == BpmImprovementStatus.Pending ||
                i.Status == BpmImprovementStatus.Accepted ||
                i.Status == BpmImprovementStatus.InProgress);
        }

        if (processId.HasValue)
            query = query.Where(i => i.ProcessId == processId.Value);

        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        if (authorId.HasValue)
            query = query.Where(i => i.InitiatorUserId == authorId.Value);

        if (dateFrom.HasValue)
            query = query.Where(i => i.CreatedAt >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(i => i.CreatedAt <= dateTo.Value);

        var improvements = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        return await MapToDtosAsync(improvements, ct);
    }

    /// <inheritdoc />
    public async Task<ImprovementDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var improvement = await _db.BpmImprovements.AsNoTracking()
            .Include(i => i.Process)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException($"Предложение {id} не найдено");

        return await BuildDtoAsync(improvement, improvement.Process.Name, ct);
    }

    /// <inheritdoc />
    public async Task<ImprovementDto> AcceptAsync(
        Guid id,
        AcceptImprovementRequest request,
        Guid reviewerId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var improvement = await _db.BpmImprovements
            .Include(i => i.Process)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException($"Предложение {id} не найдено");

        if (improvement.Status != BpmImprovementStatus.Pending)
            throw new ValidationException("Принять можно только предложение в статусе «Ожидает рассмотрения».");

        if (!isAdmin)
            await EnsureIsProcessOwnerAsync(improvement.ProcessId, reviewerId, ct);

        var now = DateTimeOffset.UtcNow;
        improvement.Status = BpmImprovementStatus.Accepted;
        improvement.AssignedUserId = request.AssignedUserId;
        improvement.DueDate = request.DueDate;
        improvement.ReviewComment = request.Comment?.Trim();
        improvement.ReviewedAt = now;
        improvement.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(improvement, improvement.Process.Name, ct);
    }

    /// <inheritdoc />
    public async Task<ImprovementDto> RejectAsync(
        Guid id,
        RejectImprovementRequest request,
        Guid reviewerId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var improvement = await _db.BpmImprovements
            .Include(i => i.Process)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException($"Предложение {id} не найдено");

        if (improvement.Status != BpmImprovementStatus.Pending)
            throw new ValidationException("Отклонить можно только предложение в статусе «Ожидает рассмотрения».");

        if (!isAdmin)
            await EnsureIsProcessOwnerAsync(improvement.ProcessId, reviewerId, ct);

        var now = DateTimeOffset.UtcNow;
        improvement.Status = BpmImprovementStatus.Rejected;
        improvement.ReviewComment = request.Comment?.Trim();
        improvement.ReviewedAt = now;
        improvement.CompletedAt = now;
        improvement.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(improvement, improvement.Process.Name, ct);
    }

    /// <inheritdoc />
    public async Task<ImprovementDto> CompleteAsync(
        Guid id,
        CompleteImprovementRequest request,
        Guid executorId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var improvement = await _db.BpmImprovements
            .Include(i => i.Process)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException($"Предложение {id} не найдено");

        if (improvement.Status is not (BpmImprovementStatus.Accepted or BpmImprovementStatus.InProgress))
            throw new ValidationException("Завершить можно только принятое или находящееся в работе предложение.");

        if (!isAdmin && improvement.AssignedUserId != executorId)
            throw new ForbiddenException("Завершить выполнение может только назначенный исполнитель или администратор.");

        var now = DateTimeOffset.UtcNow;
        improvement.Status = BpmImprovementStatus.Completed;
        improvement.Resolution = request.Resolution.Trim();
        improvement.CompletedAt = now;
        improvement.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(improvement, improvement.Process.Name, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ImprovementMonitorItemDto>> GetMonitorMyAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var monitorProcessIds = await _db.BpmProcessRoleConfigs
            .AsNoTracking()
            .Where(r => r.AssigneeType == BpmAssigneeType.User && r.AssigneeId == userId.ToString())
            .Select(r => r.ProcessId)
            .Distinct()
            .ToListAsync(ct);

        return await BuildMonitorListAsync(
            q => q.Where(p => monitorProcessIds.Contains(p.Id)),
            ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ImprovementMonitorItemDto>> GetMonitorFullAsync(
        CancellationToken ct = default)
    {
        return await BuildMonitorListAsync(q => q, ct);
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private async Task<ImprovementDto> BuildDtoAsync(
        BpmImprovement improvement,
        string processName,
        CancellationToken ct)
    {
        var userIds = new List<Guid> { improvement.InitiatorUserId };
        if (improvement.AssignedUserId.HasValue)
            userIds.Add(improvement.AssignedUserId.Value);

        var userNames = await _db.OrgUsers.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(ct);

        string GetName(Guid? id) => userNames.FirstOrDefault(u => u.Id == id)?.DisplayName ?? "";

        return new ImprovementDto(
            Id: improvement.Id,
            ProcessId: improvement.ProcessId,
            ProcessName: processName,
            Subject: improvement.Subject,
            Description: improvement.Description,
            Status: improvement.Status,
            InitiatorUserId: improvement.InitiatorUserId,
            InitiatorDisplayName: GetName(improvement.InitiatorUserId),
            AssignedUserId: improvement.AssignedUserId,
            AssignedDisplayName: improvement.AssignedUserId.HasValue ? GetName(improvement.AssignedUserId) : null,
            DueDate: improvement.DueDate,
            ReviewComment: improvement.ReviewComment,
            Resolution: improvement.Resolution,
            SourceInstanceId: improvement.SourceInstanceId,
            SourceTaskElementId: improvement.SourceTaskElementId,
            CreatedAt: improvement.CreatedAt,
            UpdatedAt: improvement.UpdatedAt,
            ReviewedAt: improvement.ReviewedAt,
            CompletedAt: improvement.CompletedAt
        );
    }

    private async Task<IReadOnlyList<ImprovementDto>> MapToDtosAsync(
        IReadOnlyList<BpmImprovement> improvements,
        CancellationToken ct)
    {
        if (improvements.Count == 0) return [];

        var processIds = improvements.Select(i => i.ProcessId).Distinct().ToList();
        var userIds = improvements
            .SelectMany(i => new[] { (Guid?)i.InitiatorUserId, i.AssignedUserId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var processes = await _db.BpmProcesses.AsNoTracking()
            .Where(p => processIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);

        var userNames = await _db.OrgUsers.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(ct);

        string GetName(Guid? id) => userNames.FirstOrDefault(u => u.Id == id)?.DisplayName ?? "";
        string GetProcess(Guid id) => processes.FirstOrDefault(p => p.Id == id)?.Name ?? "";

        return improvements.Select(i => new ImprovementDto(
            Id: i.Id,
            ProcessId: i.ProcessId,
            ProcessName: GetProcess(i.ProcessId),
            Subject: i.Subject,
            Description: i.Description,
            Status: i.Status,
            InitiatorUserId: i.InitiatorUserId,
            InitiatorDisplayName: GetName(i.InitiatorUserId),
            AssignedUserId: i.AssignedUserId,
            AssignedDisplayName: i.AssignedUserId.HasValue ? GetName(i.AssignedUserId) : null,
            DueDate: i.DueDate,
            ReviewComment: i.ReviewComment,
            Resolution: i.Resolution,
            SourceInstanceId: i.SourceInstanceId,
            SourceTaskElementId: i.SourceTaskElementId,
            CreatedAt: i.CreatedAt,
            UpdatedAt: i.UpdatedAt,
            ReviewedAt: i.ReviewedAt,
            CompletedAt: i.CompletedAt
        )).ToList();
    }

    private async Task<IReadOnlyList<ImprovementMonitorItemDto>> BuildMonitorListAsync(
        Func<IQueryable<BpmProcess>, IQueryable<BpmProcess>> filter,
        CancellationToken ct)
    {
        var processes = await filter(_db.BpmProcesses
            .AsNoTracking()
            .Where(p => !p.IsTemplate)
            .OrderBy(p => p.Name))
            .ToListAsync(ct);

        if (processes.Count == 0) return [];

        var processIds = processes.Select(p => p.Id).ToList();

        var statusCounts = await _db.BpmImprovements
            .AsNoTracking()
            .Where(i => processIds.Contains(i.ProcessId))
            .GroupBy(i => new { i.ProcessId, i.Status })
            .Select(g => new { g.Key.ProcessId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var roles = await _db.BpmProcessRoleConfigs
            .AsNoTracking()
            .Where(r => processIds.Contains(r.ProcessId))
            .ToListAsync(ct);

        return processes.Select(p =>
        {
            var counts = statusCounts.Where(s => s.ProcessId == p.Id).ToList();
            var processRoles = roles.Where(r => r.ProcessId == p.Id).ToList();

            int Count(BpmImprovementStatus s) => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;

            return new ImprovementMonitorItemDto(
                ProcessId: p.Id,
                ProcessName: p.Name,
                PendingCount: Count(BpmImprovementStatus.Pending),
                AcceptedCount: Count(BpmImprovementStatus.Accepted),
                InProgressCount: Count(BpmImprovementStatus.InProgress),
                CompletedCount: Count(BpmImprovementStatus.Completed),
                RejectedCount: Count(BpmImprovementStatus.Rejected),
                TotalCount: counts.Sum(c => c.Count),
                Owners: processRoles.Where(r => r.RoleType == BpmProcessRoleType.Owner).Select(r => r.DisplayName).ToList(),
                Curators: processRoles.Where(r => r.RoleType == BpmProcessRoleType.Curator).Select(r => r.DisplayName).ToList()
            );
        }).ToList();
    }

    /// <summary>
    /// Проверяет, что пользователь является владельцем или куратором процесса (либо администратором).
    /// Бросает ForbiddenException при отсутствии прав.
    /// </summary>
    private async Task EnsureIsProcessOwnerAsync(Guid processId, Guid userId, CancellationToken ct)
    {
        var isOwner = await _db.BpmProcessRoleConfigs.AsNoTracking()
            .AnyAsync(r =>
                r.ProcessId == processId &&
                r.RoleType == BpmProcessRoleType.Owner &&
                r.AssigneeType == BpmAssigneeType.User &&
                r.AssigneeId == userId.ToString(),
                ct);

        if (!isOwner)
            throw new ForbiddenException("Рассматривать предложения может только владелец процесса или администратор.");
    }
}
