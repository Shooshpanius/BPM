using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Portal.DTOs;
using CoreBPM.Server.Application.Portal.Interfaces;
using CoreBPM.Server.Domain.Portal;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Portal.Services;

/// <summary>Сервис навигационного меню портала (FR-PORTAL-01.4).</summary>
public class PortalMenuService : IPortalMenuService
{
    private readonly AppDbContext _db;
    public PortalMenuService(AppDbContext db) { _db = db; }

    public async Task<IReadOnlyList<PortalMenuItemDto>> GetMenuAsync(string? userRole, CancellationToken ct = default)
    {
        var items = await _db.PortalMenuItems
            .AsNoTracking()
            .Where(m => m.IsVisible)
            .Where(m => m.RequiredRole == null || m.RequiredRole == userRole)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<PortalMenuItemDto>> SaveMenuAsync(SaveMenuRequest req, CancellationToken ct = default)
    {
        var existing = await _db.PortalMenuItems.ToListAsync(ct);
        _db.PortalMenuItems.RemoveRange(existing);

        var newItems = req.Items.Select((item, i) => new PortalMenuItem
        {
            Id = item.Id ?? Guid.NewGuid(),
            ParentId = item.ParentId,
            Label = item.Label,
            Icon = item.Icon,
            SectionId = item.SectionId,
            ExternalUrl = item.ExternalUrl,
            SortOrder = item.SortOrder == 0 ? i : item.SortOrder,
            RequiredRole = item.RequiredRole,
            IsVisible = item.IsVisible
        }).ToList();

        _db.PortalMenuItems.AddRange(newItems);
        await _db.SaveChangesAsync(ct);
        return newItems.OrderBy(m => m.SortOrder).Select(Map).ToList();
    }

    private static PortalMenuItemDto Map(PortalMenuItem m) =>
        new(m.Id, m.ParentId, m.Label, m.Icon, m.SectionId, m.ExternalUrl, m.SortOrder, m.RequiredRole, m.IsVisible);
}
