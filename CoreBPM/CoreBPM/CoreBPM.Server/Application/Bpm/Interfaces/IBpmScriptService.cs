using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис управления C#-сценариями версий бизнес-процессов (FR-BPM-01.7).</summary>
public interface IBpmScriptService
{
    /// <summary>Возвращает модуль сценариев версии процесса. Создаёт пустой модуль, если он ещё не существует.</summary>
    Task<BpmScriptModuleDto> GetScriptAsync(Guid processId, Guid versionId, CancellationToken ct = default);

    /// <summary>Сохраняет тело сценария (черновик).</summary>
    Task<BpmScriptModuleDto> SaveScriptAsync(Guid processId, Guid versionId, SaveScriptModuleRequest request, CancellationToken ct = default);

    /// <summary>Публикует сценарий — изменения вступают в силу во всех экземплярах этой версии.</summary>
    Task<BpmScriptModuleDto> PublishScriptAsync(Guid processId, Guid versionId, CancellationToken ct = default);

    /// <summary>Возвращает список процессов с версиями и информацией о статусе сценариев.</summary>
    Task<IReadOnlyList<BpmProcessVersionScriptInfoDto>> ListProcessVersionScriptsAsync(Guid organizationId, CancellationToken ct = default);
}
