using CoreBPM.Server.Application.Org.DTOs;

namespace CoreBPM.Server.Application.Org.Interfaces;

/// <summary>Сервис страницы компании (FR-ORG-03).</summary>
public interface ICompanyService
{
    /// <summary>Возвращает сведения о компании (создаёт запись по умолчанию при первом обращении).</summary>
    Task<CompanyInfoDto> GetInfoAsync(CancellationToken ct = default);

    /// <summary>Обновляет сведения о компании.</summary>
    Task<CompanyInfoDto> UpdateInfoAsync(UpdateCompanyInfoRequest req, CancellationToken ct = default);

    /// <summary>Возвращает список опубликованных новостей компании.</summary>
    Task<IReadOnlyList<CompanyNewsDto>> GetNewsAsync(CancellationToken ct = default);

    /// <summary>Создаёт новость компании.</summary>
    Task<CompanyNewsDto> CreateNewsAsync(CreateNewsRequest req, Guid actorId, CancellationToken ct = default);

    /// <summary>Обновляет новость компании.</summary>
    Task<CompanyNewsDto> UpdateNewsAsync(Guid id, UpdateNewsRequest req, CancellationToken ct = default);

    /// <summary>Удаляет новость компании.</summary>
    Task DeleteNewsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Возвращает список внутренних ссылок компании.</summary>
    Task<IReadOnlyList<CompanyLinkDto>> GetLinksAsync(CancellationToken ct = default);

    /// <summary>Заменяет список внутренних ссылок компании.</summary>
    Task<IReadOnlyList<CompanyLinkDto>> UpdateLinksAsync(UpdateCompanyLinksRequest req, CancellationToken ct = default);
}
