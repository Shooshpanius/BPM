using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Portal.DTOs;
using CoreBPM.Server.Application.Portal.Interfaces;
using CoreBPM.Server.Domain.Portal;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Portal.Services;

/// <summary>Сервис управления дашбордом пользователя (FR-PORTAL-01.1).</summary>
public class PortalDashboardService : IPortalDashboardService
{
    private readonly AppDbContext _db;

    // Виджеты по умолчанию для нового пользователя
    private static readonly IReadOnlyList<(string Type, int Col, int Row, int ColSpan, int RowSpan)> DefaultWidgets =
    [
        ("my-tasks",        0, 0, 2, 2),
        ("my-processes",    2, 0, 2, 2),
        ("awaiting-action", 0, 2, 2, 1),
        ("notifications",   2, 2, 2, 1),
        ("quick-actions",   0, 3, 4, 1),
    ];

    public PortalDashboardService(AppDbContext db) { _db = db; }

    public async Task<IReadOnlyList<PortalDashboardWidgetDto>> GetDashboardAsync(Guid userId, CancellationToken ct = default)
    {
        var widgets = await _db.PortalDashboardWidgets
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.SortOrder)
            .ToListAsync(ct);

        if (widgets.Count == 0)
            widgets = await CreateDefaultWidgets(userId, ct);

        return widgets.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<PortalDashboardWidgetDto>> SaveDashboardAsync(Guid userId, SaveDashboardRequest req, CancellationToken ct = default)
    {
        var existing = await _db.PortalDashboardWidgets
            .Where(w => w.UserId == userId)
            .ToListAsync(ct);

        _db.PortalDashboardWidgets.RemoveRange(existing);

        var newWidgets = req.Widgets.Select((w, i) => new PortalDashboardWidget
        {
            Id = w.Id ?? Guid.NewGuid(),
            UserId = userId,
            WidgetType = w.WidgetType,
            Col = w.Col,
            Row = w.Row,
            ColSpan = w.ColSpan,
            RowSpan = w.RowSpan,
            Title = w.Title,
            ConfigJson = w.ConfigJson,
            IsCollapsed = w.IsCollapsed,
            SortOrder = w.SortOrder == 0 ? i : w.SortOrder
        }).ToList();

        _db.PortalDashboardWidgets.AddRange(newWidgets);
        await _db.SaveChangesAsync(ct);
        return newWidgets.OrderBy(w => w.SortOrder).Select(Map).ToList();
    }

    public async Task<PortalDashboardWidgetDto> AddWidgetAsync(Guid userId, AddWidgetRequest req, CancellationToken ct = default)
    {
        var maxOrder = await _db.PortalDashboardWidgets
            .Where(w => w.UserId == userId)
            .Select(w => (int?)w.SortOrder)
            .MaxAsync(ct) ?? -1;

        var widget = new PortalDashboardWidget
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WidgetType = req.WidgetType,
            Col = req.Col,
            Row = req.Row,
            ColSpan = req.ColSpan,
            RowSpan = req.RowSpan,
            Title = req.Title,
            ConfigJson = req.ConfigJson,
            SortOrder = maxOrder + 1
        };
        _db.PortalDashboardWidgets.Add(widget);
        await _db.SaveChangesAsync(ct);
        return Map(widget);
    }

    public async Task<PortalDashboardWidgetDto> UpdateWidgetAsync(Guid userId, Guid widgetId, UpdateWidgetRequest req, CancellationToken ct = default)
    {
        var widget = await _db.PortalDashboardWidgets
            .FirstOrDefaultAsync(w => w.Id == widgetId && w.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Виджет не найден.");

        if (req.Col.HasValue) widget.Col = req.Col.Value;
        if (req.Row.HasValue) widget.Row = req.Row.Value;
        if (req.ColSpan.HasValue) widget.ColSpan = req.ColSpan.Value;
        if (req.RowSpan.HasValue) widget.RowSpan = req.RowSpan.Value;
        if (req.Title is not null) widget.Title = req.Title;
        if (req.ConfigJson is not null) widget.ConfigJson = req.ConfigJson;
        if (req.IsCollapsed.HasValue) widget.IsCollapsed = req.IsCollapsed.Value;

        await _db.SaveChangesAsync(ct);
        return Map(widget);
    }

    public async Task DeleteWidgetAsync(Guid userId, Guid widgetId, CancellationToken ct = default)
    {
        var widget = await _db.PortalDashboardWidgets
            .FirstOrDefaultAsync(w => w.Id == widgetId && w.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Виджет не найден.");
        _db.PortalDashboardWidgets.Remove(widget);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ResetToDefaultAsync(Guid userId, CancellationToken ct = default)
    {
        var existing = await _db.PortalDashboardWidgets
            .Where(w => w.UserId == userId)
            .ToListAsync(ct);
        _db.PortalDashboardWidgets.RemoveRange(existing);
        await _db.SaveChangesAsync(ct);
        await CreateDefaultWidgets(userId, ct);
    }

    private async Task<List<PortalDashboardWidget>> CreateDefaultWidgets(Guid userId, CancellationToken ct)
    {
        var widgets = DefaultWidgets.Select((d, i) => new PortalDashboardWidget
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WidgetType = d.Type,
            Col = d.Col,
            Row = d.Row,
            ColSpan = d.ColSpan,
            RowSpan = d.RowSpan,
            SortOrder = i
        }).ToList();
        _db.PortalDashboardWidgets.AddRange(widgets);
        await _db.SaveChangesAsync(ct);
        return widgets;
    }

    private static PortalDashboardWidgetDto Map(PortalDashboardWidget w) =>
        new(w.Id, w.WidgetType, w.Col, w.Row, w.ColSpan, w.RowSpan, w.Title, w.ConfigJson, w.IsCollapsed, w.SortOrder);
}
