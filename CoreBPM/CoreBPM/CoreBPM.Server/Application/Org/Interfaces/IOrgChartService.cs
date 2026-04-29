using CoreBPM.Server.Application.Org.DTOs;

namespace CoreBPM.Server.Application.Org.Interfaces;

/// <summary>
/// Сервис визуализации оргструктуры (FR-ORG-01.4).
/// Возвращает иерархическое дерево подразделений с сотрудниками и (в расширенном виде) вакансиями.
/// </summary>
public interface IOrgChartService
{
    /// <summary>
    /// Возвращает дерево оргструктуры для указанной организации.
    /// </summary>
    /// <param name="organizationId">Идентификатор организации.</param>
    /// <param name="search">Текстовый поиск по имени сотрудника, должности или подразделению.</param>
    /// <param name="extended">
    /// Если true — включает данные о ставках и вакансиях (только для Admin/HR).
    /// </param>
    Task<OrgChartDto> GetChartAsync(
        Guid organizationId,
        string? search = null,
        bool extended = false,
        CancellationToken ct = default);
}
