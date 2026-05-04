using CoreBPM.Server.Application.Portal.DTOs;

namespace CoreBPM.Server.Application.Portal.Interfaces;

public interface IPortalDashboardService
{
    Task<IReadOnlyList<PortalDashboardWidgetDto>> GetDashboardAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<PortalDashboardWidgetDto>> SaveDashboardAsync(Guid userId, SaveDashboardRequest req, CancellationToken ct = default);
    Task<PortalDashboardWidgetDto> AddWidgetAsync(Guid userId, AddWidgetRequest req, CancellationToken ct = default);
    Task<PortalDashboardWidgetDto> UpdateWidgetAsync(Guid userId, Guid widgetId, UpdateWidgetRequest req, CancellationToken ct = default);
    Task DeleteWidgetAsync(Guid userId, Guid widgetId, CancellationToken ct = default);
    Task ResetToDefaultAsync(Guid userId, CancellationToken ct = default);
}
