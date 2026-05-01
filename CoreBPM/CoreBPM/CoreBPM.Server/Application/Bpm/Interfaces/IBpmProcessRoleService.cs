using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис управления ролями (Владелец/Куратор) в определении бизнес-процесса.</summary>
public interface IBpmProcessRoleService
{
    /// <summary>Возвращает все настройки ролей для указанного процесса.</summary>
    Task<IReadOnlyList<BpmProcessRoleConfigDto>> GetRolesAsync(Guid processId, CancellationToken ct = default);

    /// <summary>Полностью заменяет набор ролей процесса. Возвращает обновлённый список.</summary>
    Task<IReadOnlyList<BpmProcessRoleConfigDto>> ReplaceRolesAsync(
        Guid processId,
        UpsertProcessRoleConfigsRequest request,
        CancellationToken ct = default);
}
