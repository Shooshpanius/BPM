using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Portal.DTOs;
using CoreBPM.Server.Application.Portal.Interfaces;
using CoreBPM.Server.Domain.Portal;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Portal.Services;

/// <summary>Сервис брендинга портала (FR-PORTAL-01.4).</summary>
public class PortalBrandingService : IPortalBrandingService
{
    private readonly AppDbContext _db;
    public PortalBrandingService(AppDbContext db) { _db = db; }

    public async Task<PortalBrandingDto> GetBrandingAsync(CancellationToken ct = default)
    {
        var branding = await _db.PortalBrandings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (branding is null)
        {
            branding = new PortalBranding { Id = Guid.NewGuid(), UpdatedAt = DateTimeOffset.UtcNow };
            _db.PortalBrandings.Add(branding);
            await _db.SaveChangesAsync(ct);
        }
        return Map(branding);
    }

    public async Task<PortalBrandingDto> UpdateBrandingAsync(UpdateBrandingRequest req, CancellationToken ct = default)
    {
        var branding = await _db.PortalBrandings.FirstOrDefaultAsync(ct);
        if (branding is null)
        {
            branding = new PortalBranding { Id = Guid.NewGuid() };
            _db.PortalBrandings.Add(branding);
        }
        if (req.SystemName is not null) branding.SystemName = req.SystemName;
        if (req.LogoUrl is not null) branding.LogoUrl = req.LogoUrl;
        if (req.FaviconUrl is not null) branding.FaviconUrl = req.FaviconUrl;
        if (req.PrimaryColor is not null) branding.PrimaryColor = req.PrimaryColor;
        if (req.AccentColor is not null) branding.AccentColor = req.AccentColor;
        if (req.GlobalTheme is not null) branding.GlobalTheme = req.GlobalTheme;
        branding.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Map(branding);
    }

    private static PortalBrandingDto Map(PortalBranding b) =>
        new(b.Id, b.SystemName, b.LogoUrl, b.FaviconUrl, b.PrimaryColor, b.AccentColor, b.GlobalTheme);
}
