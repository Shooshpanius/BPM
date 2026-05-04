using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;
using CoreBPM.Server.Domain.Org;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Org.Services;

/// <summary>Реализация сервиса настроек пользователя (FR-ORG-02.3).</summary>
public class UserPreferencesService : IUserPreferencesService
{
    private readonly AppDbContext _db;

    public UserPreferencesService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<UserPreferencesDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var pref = await _db.OrgUserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (pref is null)
        {
            // Создаём запись с умолчаниями при первом обращении
            pref = new OrgUserPreference
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.OrgUserPreferences.Add(pref);
            await _db.SaveChangesAsync(ct);
        }

        return MapToDto(pref);
    }

    /// <inheritdoc />
    public async Task<UserPreferencesDto> UpdateAsync(Guid userId, UpdatePreferencesRequest req, CancellationToken ct = default)
    {
        var pref = await _db.OrgUserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (pref is null)
        {
            pref = new OrgUserPreference
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.OrgUserPreferences.Add(pref);
        }

        if (req.Language is not null) pref.Language = req.Language;
        if (req.TimeZone is not null) pref.TimeZone = req.TimeZone;
        if (req.Theme is not null) pref.Theme = req.Theme;
        if (req.DateFormat is not null) pref.DateFormat = req.DateFormat;
        if (req.PageSize.HasValue && req.PageSize.Value > 0) pref.PageSize = req.PageSize.Value;
        pref.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(pref);
    }

    private static UserPreferencesDto MapToDto(OrgUserPreference pref) => new()
    {
        Language = pref.Language,
        TimeZone = pref.TimeZone,
        Theme = pref.Theme,
        DateFormat = pref.DateFormat,
        PageSize = pref.PageSize
    };
}
