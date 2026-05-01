using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис аналитики и KPI бизнес-процессов (FR-BPM-03.2).</summary>
public interface IBpmAnalyticsService
{
    /// <summary>Возвращает агрегированную аналитику процесса за указанный период.</summary>
    Task<ProcessAnalyticsDto> GetProcessAnalyticsAsync(Guid processId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);

    /// <summary>Возвращает тепловую карту узлов процесса по среднему времени выполнения.</summary>
    Task<IReadOnlyList<NodeHeatMapDto>> GetNodeHeatMapAsync(Guid processId, Guid? versionId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);

    /// <summary>Возвращает воронку процесса — количество достигших и прошедших каждый узел.</summary>
    Task<IReadOnlyList<ProcessFunnelStepDto>> GetProcessFunnelAsync(Guid processId, Guid? versionId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);

    /// <summary>Сравнивает KPI двух версий процесса.</summary>
    Task<ProcessVersionComparisonDto> GetVersionComparisonAsync(Guid processId, Guid versionAId, Guid versionBId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);

    /// <summary>Возвращает сводный отчёт по всем процессам организации за период.</summary>
    Task<IReadOnlyList<ProcessAnalyticsSummaryItemDto>> GetAnalyticsSummaryAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);

    /// <summary>Генерирует файл Excel со сводным отчётом по всем процессам за период.</summary>
    Task<byte[]> ExportSummaryToExcelAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);
}
