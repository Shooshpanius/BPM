using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;
using CoreBPM.Server.Domain.Org;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Org.Services;

/// <summary>Реализация сервиса страницы компании (FR-ORG-03).</summary>
public class CompanyService : ICompanyService
{
    private readonly AppDbContext _db;

    public CompanyService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<CompanyInfoDto> GetInfoAsync(CancellationToken ct = default)
    {
        var info = await _db.OrgCompanyInfos.AsNoTracking().FirstOrDefaultAsync(ct);
        if (info is null)
        {
            // Создаём запись по умолчанию при первом обращении
            info = new OrgCompanyInfo
            {
                Id = Guid.NewGuid(),
                Name = "Наша компания",
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.OrgCompanyInfos.Add(info);
            await _db.SaveChangesAsync(ct);
        }
        return MapInfoToDto(info);
    }

    /// <inheritdoc />
    public async Task<CompanyInfoDto> UpdateInfoAsync(UpdateCompanyInfoRequest req, CancellationToken ct = default)
    {
        var info = await _db.OrgCompanyInfos.FirstOrDefaultAsync(ct);
        if (info is null)
        {
            info = new OrgCompanyInfo { Id = Guid.NewGuid() };
            _db.OrgCompanyInfos.Add(info);
        }

        if (req.Name is not null) info.Name = req.Name;
        if (req.Description is not null) info.Description = req.Description;
        if (req.Phone is not null) info.Phone = req.Phone;
        if (req.Email is not null) info.Email = req.Email;
        if (req.Address is not null) info.Address = req.Address;
        if (req.Website is not null) info.Website = req.Website;
        info.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapInfoToDto(info);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompanyNewsDto>> GetNewsAsync(CancellationToken ct = default)
    {
        return await _db.OrgCompanyNews
            .AsNoTracking()
            .Where(n => n.IsPublished)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => MapNewsToDto(n))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<CompanyNewsDto> CreateNewsAsync(CreateNewsRequest req, Guid actorId, CancellationToken ct = default)
    {
        var news = new OrgCompanyNews
        {
            Id = Guid.NewGuid(),
            Title = req.Title,
            Content = req.Content,
            AuthorId = actorId,
            IsPublished = req.IsPublished,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.OrgCompanyNews.Add(news);
        await _db.SaveChangesAsync(ct);
        return MapNewsToDto(news);
    }

    /// <inheritdoc />
    public async Task<CompanyNewsDto> UpdateNewsAsync(Guid id, UpdateNewsRequest req, CancellationToken ct = default)
    {
        var news = await _db.OrgCompanyNews.FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new KeyNotFoundException($"Новость {id} не найдена.");

        if (req.Title is not null) news.Title = req.Title;
        if (req.Content is not null) news.Content = req.Content;
        if (req.IsPublished.HasValue) news.IsPublished = req.IsPublished.Value;
        news.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapNewsToDto(news);
    }

    /// <inheritdoc />
    public async Task DeleteNewsAsync(Guid id, CancellationToken ct = default)
    {
        var news = await _db.OrgCompanyNews.FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new KeyNotFoundException($"Новость {id} не найдена.");
        _db.OrgCompanyNews.Remove(news);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompanyLinkDto>> GetLinksAsync(CancellationToken ct = default)
    {
        return await _db.OrgCompanyLinks
            .AsNoTracking()
            .OrderBy(l => l.SortOrder)
            .Select(l => MapLinkToDto(l))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompanyLinkDto>> UpdateLinksAsync(UpdateCompanyLinksRequest req, CancellationToken ct = default)
    {
        // Полная замена: удаляем старые и добавляем новые
        var old = await _db.OrgCompanyLinks.ToListAsync(ct);
        _db.OrgCompanyLinks.RemoveRange(old);

        var newLinks = req.Links.Select((l, idx) => new OrgCompanyLink
        {
            Id = l.Id == Guid.Empty ? Guid.NewGuid() : l.Id,
            Title = l.Title,
            Url = l.Url,
            SortOrder = l.SortOrder == 0 ? idx : l.SortOrder
        }).ToList();

        _db.OrgCompanyLinks.AddRange(newLinks);
        await _db.SaveChangesAsync(ct);

        return newLinks.OrderBy(l => l.SortOrder).Select(MapLinkToDto).ToList();
    }

    // ── Вспомогательные методы ─────────────────────────────────────────────────

    private static CompanyInfoDto MapInfoToDto(OrgCompanyInfo info) => new()
    {
        Id = info.Id,
        Name = info.Name,
        Description = info.Description,
        Phone = info.Phone,
        Email = info.Email,
        Address = info.Address,
        Website = info.Website,
        LogoUrl = info.LogoUrl
    };

    private static CompanyNewsDto MapNewsToDto(OrgCompanyNews n) => new()
    {
        Id = n.Id,
        Title = n.Title,
        Content = n.Content,
        AuthorId = n.AuthorId,
        IsPublished = n.IsPublished,
        CreatedAt = n.CreatedAt,
        UpdatedAt = n.UpdatedAt
    };

    private static CompanyLinkDto MapLinkToDto(OrgCompanyLink l) => new()
    {
        Id = l.Id,
        Title = l.Title,
        Url = l.Url,
        SortOrder = l.SortOrder
    };
}
