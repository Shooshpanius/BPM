using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис работы с кастомными конфигурациями BPMN-элементов процесса.</summary>
public interface IBpmElementConfigService
{
    /// <summary>Возвращает все конфигурации элементов процесса.</summary>
    Task<IReadOnlyList<BpmElementConfigDto>> GetConfigsAsync(Guid processId, CancellationToken ct = default);

    /// <summary>Возвращает конфигурацию конкретного элемента (или null).</summary>
    Task<BpmElementConfigDto?> GetConfigAsync(Guid processId, string elementId, CancellationToken ct = default);

    /// <summary>Создаёт или обновляет конфигурацию элемента.</summary>
    Task<BpmElementConfigDto> UpsertConfigAsync(Guid processId, string elementId, UpsertElementConfigRequest request, CancellationToken ct = default);

    /// <summary>Удаляет конфигурацию элемента.</summary>
    Task DeleteConfigAsync(Guid processId, string elementId, CancellationToken ct = default);
}
