using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Notify.DTOs;
using CoreBPM.Server.Application.Notify.Interfaces;
using CoreBPM.Server.Domain.Notify;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Notify.Services;

/// <summary>Сервис управления персистентными in-app уведомлениями (FR-MSG-02.1).</summary>
public class InAppNotificationService : IInAppNotificationService
{
    private readonly AppDbContext _db;

    public InAppNotificationService(AppDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task SaveAsync(SaveInboxEntryRequest request, CancellationToken ct = default)
    {
        var entry = new NotifyInboxEntry
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Type = request.Type,
            Title = request.Title,
            Body = request.Body,
            Link = request.Link,
            PayloadJson = request.PayloadJson,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.NotifyInboxEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<InboxEntryDto> Items, int Total)> GetPagedAsync(
        Guid userId,
        bool? isRead,
        string? type,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.NotifyInboxEntries
            .Where(e => e.UserId == userId);

        if (isRead.HasValue)
            query = query.Where(e => e.IsRead == isRead.Value);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(e => e.Type == type);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new InboxEntryDto(
                e.Id, e.Type, e.Title, e.Body, e.Link, e.IsRead, e.CreatedAt, e.ReadAt))
            .ToListAsync(ct);

        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
        => await _db.NotifyInboxEntries
            .CountAsync(e => e.UserId == userId && !e.IsRead, ct);

    /// <inheritdoc/>
    public async Task MarkReadAsync(Guid userId, Guid entryId, CancellationToken ct = default)
    {
        var entry = await _db.NotifyInboxEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId, ct);
        if (entry is null) return;

        entry.IsRead = true;
        entry.ReadAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        await _db.NotifyInboxEntries
            .Where(e => e.UserId == userId && !e.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.IsRead, true)
                .SetProperty(e => e.ReadAt, now),
            ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid userId, Guid entryId, CancellationToken ct = default)
    {
        await _db.NotifyInboxEntries
            .Where(e => e.Id == entryId && e.UserId == userId)
            .ExecuteDeleteAsync(ct);
    }
}
