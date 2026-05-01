using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса сохранённых фильтров раздела «Мои процессы».</summary>
public class BpmSavedFilterService : IBpmSavedFilterService
{
    private readonly AppDbContext _db;

    public BpmSavedFilterService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmSavedFilterDto>> GetFiltersAsync(Guid userId, CancellationToken ct = default)
    {
        var filters = await _db.BpmSavedFilters
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.CreatedAt)
            .ToListAsync(ct);

        return filters.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<BpmSavedFilterDto> CreateFilterAsync(
        Guid userId,
        SaveFilterRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Название фильтра обязательно");

        var now = DateTimeOffset.UtcNow;
        var filter = new BpmSavedFilter
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            FiltersJson = request.FiltersJson ?? "{}",
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.BpmSavedFilters.Add(filter);
        await _db.SaveChangesAsync(ct);
        return MapToDto(filter);
    }

    /// <inheritdoc />
    public async Task<BpmSavedFilterDto> UpdateFilterAsync(
        Guid filterId,
        Guid userId,
        SaveFilterRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Название фильтра обязательно");

        var filter = await _db.BpmSavedFilters
            .FirstOrDefaultAsync(f => f.Id == filterId, ct)
            ?? throw new NotFoundException($"Фильтр {filterId} не найден");

        if (filter.UserId != userId)
            throw new ForbiddenException("Нет доступа к этому фильтру");

        filter.Name = request.Name.Trim();
        filter.FiltersJson = request.FiltersJson ?? "{}";
        filter.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(filter);
    }

    /// <inheritdoc />
    public async Task DeleteFilterAsync(Guid filterId, Guid userId, CancellationToken ct = default)
    {
        var filter = await _db.BpmSavedFilters
            .FirstOrDefaultAsync(f => f.Id == filterId, ct)
            ?? throw new NotFoundException($"Фильтр {filterId} не найден");

        if (filter.UserId != userId)
            throw new ForbiddenException("Нет доступа к этому фильтру");

        _db.BpmSavedFilters.Remove(filter);
        await _db.SaveChangesAsync(ct);
    }

    private static BpmSavedFilterDto MapToDto(BpmSavedFilter f) =>
        new(f.Id, f.Name, f.FiltersJson, f.CreatedAt);
}
