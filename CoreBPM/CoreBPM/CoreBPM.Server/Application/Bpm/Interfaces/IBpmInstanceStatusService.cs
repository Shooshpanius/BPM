using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис управления пользовательскими статусами экземпляров бизнес-процесса.</summary>
public interface IBpmInstanceStatusService
{
    /// <summary>Возвращает конфигурацию статусов процесса вместе со списком вариантов.</summary>
    Task<InstanceStatusConfigDto> GetConfigAsync(Guid processId, CancellationToken ct = default);

    /// <summary>
    /// Обновляет конфигурацию статусов.
    /// Если <see cref="UpdateStatusConfigRequest.CreateVariable"/> равен true — создаёт новую переменную
    /// типа List с именем <see cref="UpdateStatusConfigRequest.NewVariableName"/> и привязывает её к конфигу.
    /// </summary>
    Task<InstanceStatusConfigDto> UpdateConfigAsync(Guid processId, UpdateStatusConfigRequest request, CancellationToken ct = default);

    /// <summary>Добавляет новый вариант статуса. Код генерируется транслитерацией, если не передан.</summary>
    Task<InstanceStatusOptionDto> CreateOptionAsync(Guid processId, CreateStatusOptionRequest request, CancellationToken ct = default);

    /// <summary>Обновляет название и/или код варианта статуса.</summary>
    Task<InstanceStatusOptionDto> UpdateOptionAsync(Guid processId, Guid optionId, UpdateStatusOptionRequest request, CancellationToken ct = default);

    /// <summary>Удаляет вариант статуса.</summary>
    Task DeleteOptionAsync(Guid processId, Guid optionId, CancellationToken ct = default);

    /// <summary>Пересчитывает SortOrder вариантов статусов согласно переданному порядку идентификаторов.</summary>
    Task ReorderOptionsAsync(Guid processId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default);
}
