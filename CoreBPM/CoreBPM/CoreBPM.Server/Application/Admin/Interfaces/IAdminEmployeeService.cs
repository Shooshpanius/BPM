using CoreBPM.Server.Application.Admin.DTOs;

namespace CoreBPM.Server.Application.Admin.Interfaces;

/// <summary>Сервис управления сотрудниками в административной панели.</summary>
public interface IAdminEmployeeService
{
    /// <summary>Возвращает список сотрудников (опционально — только для указанной организации).</summary>
    Task<IReadOnlyList<EmployeeDto>> GetAllAsync(Guid? organizationId = null, CancellationToken ct = default);

    /// <summary>Возвращает список сотрудников конкретного пользователя.</summary>
    Task<IReadOnlyList<EmployeeDto>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Создаёт сотрудника: привязывает пользователя к организации.
    /// Связка UserId–OrganizationId уникальна.
    /// </summary>
    Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, CancellationToken ct = default);

    /// <summary>Обновляет данные сотрудника (должность, активность).</summary>
    Task<EmployeeDto> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken ct = default);

    /// <summary>Удаляет запись сотрудника.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
