using CoreBPM.Server.Application.Portal.DTOs;

namespace CoreBPM.Server.Application.Portal.Interfaces;

public interface IPortalBrandingService
{
    Task<PortalBrandingDto> GetBrandingAsync(CancellationToken ct = default);
    Task<PortalBrandingDto> UpdateBrandingAsync(UpdateBrandingRequest req, CancellationToken ct = default);
}
