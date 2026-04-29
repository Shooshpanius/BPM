using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис управления бизнес-процессами (CRUD + версии + диаграммы).</summary>
public interface IBpmProcessService
{
    /// <summary>Возвращает список процессов организации.</summary>
    Task<IReadOnlyList<BpmProcessListItemDto>> GetProcessesAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Возвращает процесс по идентификатору.</summary>
    Task<BpmProcessDto> GetProcessByIdAsync(Guid processId, CancellationToken ct = default);

    /// <summary>Создаёт новый процесс с пустым черновиком версии 1.</summary>
    Task<BpmProcessDto> CreateProcessAsync(CreateBpmProcessRequest request, Guid createdByUserId, CancellationToken ct = default);

    /// <summary>Обновляет метаданные процесса (название, описание).</summary>
    Task<BpmProcessDto> UpdateProcessAsync(Guid processId, UpdateBpmProcessRequest request, CancellationToken ct = default);

    /// <summary>Мягко удаляет процесс.</summary>
    Task DeleteProcessAsync(Guid processId, CancellationToken ct = default);

    /// <summary>Возвращает список версий процесса (без XML).</summary>
    Task<IReadOnlyList<BpmProcessVersionInfoDto>> GetVersionsAsync(Guid processId, CancellationToken ct = default);

    /// <summary>Возвращает текущую редактируемую диаграмму (последний черновик или активную версию).</summary>
    Task<BpmDiagramDto> GetDiagramAsync(Guid processId, CancellationToken ct = default);

    /// <summary>
    /// Сохраняет XML-диаграмму в текущий черновик.
    /// Если черновика нет — создаёт новый с инкрементальным номером.
    /// </summary>
    Task<BpmDiagramDto> SaveDiagramAsync(Guid processId, SaveDiagramRequest request, Guid savedByUserId, CancellationToken ct = default);
}
