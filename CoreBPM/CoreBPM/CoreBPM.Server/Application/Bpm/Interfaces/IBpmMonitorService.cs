using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис монитора бизнес-процессов (FR-BPM-02.4).</summary>
public interface IBpmMonitorService
{
    /// <summary>
    /// Возвращает процессы, доступные пользователю для мониторинга (Владелец или Куратор),
    /// с агрегированной статистикой экземпляров.
    /// </summary>
    Task<IReadOnlyList<BpmProcessMonitorItemDto>> GetMyMonitorProcessesAsync(
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Возвращает все процессы системы с агрегированной статистикой экземпляров.
    /// Только для пользователей с ролью Admin.
    /// </summary>
    Task<IReadOnlyList<BpmProcessMonitorItemDto>> GetFullMonitorProcessesAsync(
        CancellationToken ct = default);

    /// <summary>Возвращает детальную статистику экземпляров для конкретного процесса.</summary>
    Task<BpmProcessStatsDto> GetProcessStatsAsync(
        Guid processId,
        CancellationToken ct = default);

    /// <summary>Экспортирует список экземпляров процесса в CSV-файл (UTF-8 BOM).</summary>
    Task<byte[]> ExportProcessInstancesToCsvAsync(
        Guid processId,
        CancellationToken ct = default);

    /// <summary>Возвращает сводную статистику для дашборда мониторинга.</summary>
    Task<BpmDashboardDto> GetDashboardAsync(
        Guid? userId,
        bool isAdmin,
        CancellationToken ct = default);
}
