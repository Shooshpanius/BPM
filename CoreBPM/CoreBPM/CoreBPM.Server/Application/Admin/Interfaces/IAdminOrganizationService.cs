using CoreBPM.Server.Application.Admin.DTOs;

namespace CoreBPM.Server.Application.Admin.Interfaces;

/// <summary>Сервис управления организациями в административной панели.</summary>
public interface IAdminOrganizationService
{
    /// <summary>Возвращает список всех организаций.</summary>
    Task<IReadOnlyList<OrganizationDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Возвращает организацию по идентификатору.</summary>
    Task<OrganizationDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Создаёт новую организацию.</summary>
    Task<OrganizationDto> CreateAsync(CreateOrganizationRequest request, CancellationToken ct = default);

    /// <summary>Обновляет данные организации.</summary>
    Task<OrganizationDto> UpdateAsync(Guid id, UpdateOrganizationRequest request, CancellationToken ct = default);

    /// <summary>Удаляет организацию (только если нет активных сотрудников).</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Устанавливает указанную организацию как основную, снимает флаг с остальных.</summary>
    Task SetPrimaryAsync(Guid id, CancellationToken ct = default);
}
