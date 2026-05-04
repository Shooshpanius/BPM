using CoreBPM.Server.Application.Portal.DTOs;

namespace CoreBPM.Server.Application.Portal.Interfaces;

public interface IPortalMenuService
{
    Task<IReadOnlyList<PortalMenuItemDto>> GetMenuAsync(string? userRole, CancellationToken ct = default);
    Task<IReadOnlyList<PortalMenuItemDto>> SaveMenuAsync(SaveMenuRequest req, CancellationToken ct = default);
}
