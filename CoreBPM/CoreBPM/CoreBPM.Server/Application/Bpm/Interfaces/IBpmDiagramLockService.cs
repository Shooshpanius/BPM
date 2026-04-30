using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис управления мягкими блокировками BPMN-диаграмм.</summary>
public interface IBpmDiagramLockService
{
    /// <summary>
    /// Возвращает активную блокировку для процесса, или null если диаграмма не заблокирована
    /// (либо блокировка истекла).
    /// </summary>
    Task<DiagramLockDto?> GetLockAsync(Guid processId, CancellationToken ct = default);

    /// <summary>
    /// Попытка захватить блокировку.
    /// Если блокировка уже принадлежит этому же пользователю — продлевает её и возвращает IsAcquired=true.
    /// Если заблокировано другим активным пользователем — возвращает IsAcquired=false с информацией о нём.
    /// </summary>
    Task<AcquireLockResponse> AcquireAsync(Guid processId, Guid userId, string userDisplayName, CancellationToken ct = default);

    /// <summary>
    /// Продлевает блокировку (heartbeat).
    /// Только владелец блокировки может её продлить.
    /// </summary>
    Task<bool> RefreshAsync(Guid processId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Снимает блокировку.
    /// Возвращает true если блокировка была снята, false если она не принадлежит данному пользователю.
    /// </summary>
    Task<bool> ReleaseAsync(Guid processId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Принудительно снимает блокировку (только для администраторов).
    /// </summary>
    Task ForceReleaseAsync(Guid processId, CancellationToken ct = default);
}
