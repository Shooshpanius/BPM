using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса аналитики и KPI бизнес-процессов (FR-BPM-03.2).</summary>
public class BpmAnalyticsService : IBpmAnalyticsService
{
    private readonly AppDbContext _db;

    public BpmAnalyticsService(AppDbContext db)
    {
        _db = db;
    }

    // ─── Аналитика процесса ──────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ProcessAnalyticsDto> GetProcessAnalyticsAsync(
        Guid processId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var process = await _db.BpmProcesses.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == processId && !p.IsDeleted, ct)
            ?? throw new NotFoundException("Процесс не найден");

        var query = _db.BpmInstances.AsNoTracking()
            .Where(i => i.ProcessId == processId);
        if (from.HasValue) query = query.Where(i => i.StartedAt >= from.Value);
        if (to.HasValue)   query = query.Where(i => i.StartedAt <= to.Value);

        var instances = await query.Select(i => new
        {
            i.State,
            i.StartedAt,
            i.CompletedAt,
        }).ToListAsync(ct);

        int total      = instances.Count;
        int completed  = instances.Count(i => i.State == BpmInstanceState.Completed);
        int faulted    = instances.Count(i => i.State == BpmInstanceState.Faulted);

        // Время цикла — только у завершённых экземпляров
        var cycleTimes = instances
            .Where(i => i.State == BpmInstanceState.Completed && i.CompletedAt.HasValue)
            .Select(i => (i.CompletedAt!.Value - i.StartedAt).TotalMinutes)
            .OrderBy(t => t)
            .ToList();

        double avg    = cycleTimes.Count > 0 ? cycleTimes.Average()             : 0;
        double median = CalculatePercentile(cycleTimes, 50);
        double p95    = CalculatePercentile(cycleTimes, 95);

        double onTimePercent = cycleTimes.Count > 0 && process.TargetCycleTimeMinutes.HasValue
            ? cycleTimes.Count(t => t <= process.TargetCycleTimeMinutes.Value) * 100.0 / cycleTimes.Count
            : 0;
        double faultedPercent = total > 0 ? faulted * 100.0 / total : 0;

        var histogram = BuildHistogram(cycleTimes);

        return new ProcessAnalyticsDto(
            process.Id,
            process.Name,
            total,
            completed,
            faulted,
            Math.Round(onTimePercent, 1),
            Math.Round(faultedPercent, 1),
            Math.Round(avg, 2),
            Math.Round(median, 2),
            Math.Round(p95, 2),
            histogram,
            process.TargetCycleTimeMinutes,
            process.TargetOnTimePercent);
    }

    // ─── Тепловая карта ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<NodeHeatMapDto>> GetNodeHeatMapAsync(
        Guid processId, Guid? versionId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        _ = await _db.BpmProcesses.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == processId && !p.IsDeleted, ct)
            ?? throw new NotFoundException("Процесс не найден");

        var query = _db.BpmInstanceHistoryEntries.AsNoTracking()
            .Where(h => h.Instance.ProcessId == processId
                     && h.EventType == BpmHistoryEventType.NodeExecuted
                     && h.ElementId != null
                     && h.DurationMs != null);

        if (versionId.HasValue)
            query = query.Where(h => h.Instance.ProcessVersionId == versionId.Value);
        if (from.HasValue)
            query = query.Where(h => h.OccurredAt >= from.Value);
        if (to.HasValue)
            query = query.Where(h => h.OccurredAt <= to.Value);

        var rows = await query
            .GroupBy(h => new { h.ElementId, h.ElementName })
            .Select(g => new
            {
                ElementId   = g.Key.ElementId!,
                ElementName = g.Key.ElementName ?? g.Key.ElementId!,
                AvgMs       = g.Average(h => (double)h.DurationMs!),
                PassCount   = g.Count(),
            })
            .ToListAsync(ct);

        if (rows.Count == 0) return [];

        double maxAvg = rows.Max(r => r.AvgMs);

        return rows
            .OrderByDescending(r => r.AvgMs)
            .Select(r => new NodeHeatMapDto(
                r.ElementId,
                r.ElementName,
                Math.Round(r.AvgMs, 0),
                r.PassCount,
                maxAvg > 0 ? Math.Round(r.AvgMs / maxAvg, 3) : 0))
            .ToList();
    }

    // ─── Воронка ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessFunnelStepDto>> GetProcessFunnelAsync(
        Guid processId, Guid? versionId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        _ = await _db.BpmProcesses.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == processId && !p.IsDeleted, ct)
            ?? throw new NotFoundException("Процесс не найден");

        var query = _db.BpmInstanceHistoryEntries.AsNoTracking()
            .Where(h => h.Instance.ProcessId == processId
                     && (h.EventType == BpmHistoryEventType.NodeExecuted
                      || h.EventType == BpmHistoryEventType.NodeFailed)
                     && h.ElementId != null);

        if (versionId.HasValue)
            query = query.Where(h => h.Instance.ProcessVersionId == versionId.Value);
        if (from.HasValue)
            query = query.Where(h => h.OccurredAt >= from.Value);
        if (to.HasValue)
            query = query.Where(h => h.OccurredAt <= to.Value);

        // Считаем уникальных экземпляров, достигших узел (reached)
        // и прошедших его успешно (passed = NodeExecuted)
        var reachedRaw = await query
            .GroupBy(h => new { h.ElementId, h.ElementName })
            .Select(g => new
            {
                ElementId   = g.Key.ElementId!,
                ElementName = g.Key.ElementName ?? g.Key.ElementId!,
                Reached     = g.Select(h => h.InstanceId).Distinct().Count(),
                Passed      = g.Where(h => h.EventType == BpmHistoryEventType.NodeExecuted)
                               .Select(h => h.InstanceId).Distinct().Count(),
            })
            .OrderByDescending(g => g.Reached)
            .ToListAsync(ct);

        return reachedRaw.Select(r =>
        {
            double dropOff = r.Reached > 0
                ? Math.Round((r.Reached - r.Passed) * 100.0 / r.Reached, 1)
                : 0;
            return new ProcessFunnelStepDto(r.ElementId, r.ElementName, r.Reached, r.Passed, dropOff);
        }).ToList();
    }

    // ─── Сравнение версий ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ProcessVersionComparisonDto> GetVersionComparisonAsync(
        Guid processId, Guid versionAId, Guid versionBId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var process = await _db.BpmProcesses.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == processId && !p.IsDeleted, ct)
            ?? throw new NotFoundException("Процесс не найден");

        var versionA = await _db.BpmProcessVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionAId && v.ProcessId == processId, ct)
            ?? throw new NotFoundException("Версия A не найдена");

        var versionB = await _db.BpmProcessVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionBId && v.ProcessId == processId, ct)
            ?? throw new NotFoundException("Версия B не найдена");

        var analyticsA = await GetAnalyticsForVersionAsync(process, versionAId, from, to, ct);
        var analyticsB = await GetAnalyticsForVersionAsync(process, versionBId, from, to, ct);

        return new ProcessVersionComparisonDto(
            process.Id,
            process.Name,
            versionAId,
            versionA.VersionNumber,
            versionBId,
            versionB.VersionNumber,
            analyticsA,
            analyticsB);
    }

    // ─── Сводный отчёт ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessAnalyticsSummaryItemDto>> GetAnalyticsSummaryAsync(
        DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var processes = await _db.BpmProcesses.AsNoTracking()
            .Where(p => !p.IsDeleted && !p.IsTemplate)
            .Select(p => new { p.Id, p.Name, p.TargetCycleTimeMinutes, p.TargetOnTimePercent })
            .ToListAsync(ct);

        var result = new List<ProcessAnalyticsSummaryItemDto>(processes.Count);
        foreach (var proc in processes)
        {
            var query = _db.BpmInstances.AsNoTracking()
                .Where(i => i.ProcessId == proc.Id);
            if (from.HasValue) query = query.Where(i => i.StartedAt >= from.Value);
            if (to.HasValue)   query = query.Where(i => i.StartedAt <= to.Value);

            var instances = await query.Select(i => new
            {
                i.State,
                i.StartedAt,
                i.CompletedAt,
            }).ToListAsync(ct);

            int total    = instances.Count;
            int completed = instances.Count(i => i.State == BpmInstanceState.Completed);
            int faulted   = instances.Count(i => i.State == BpmInstanceState.Faulted);

            var cycleTimes = instances
                .Where(i => i.State == BpmInstanceState.Completed && i.CompletedAt.HasValue)
                .Select(i => (i.CompletedAt!.Value - i.StartedAt).TotalMinutes)
                .ToList();

            double avg = cycleTimes.Count > 0 ? cycleTimes.Average() : 0;
            double onTime = cycleTimes.Count > 0 && proc.TargetCycleTimeMinutes.HasValue
                ? cycleTimes.Count(t => t <= proc.TargetCycleTimeMinutes.Value) * 100.0 / cycleTimes.Count
                : 0;
            double faultedPct = total > 0 ? faulted * 100.0 / total : 0;

            result.Add(new ProcessAnalyticsSummaryItemDto(
                proc.Id,
                proc.Name,
                total,
                Math.Round(avg, 2),
                Math.Round(onTime, 1),
                Math.Round(faultedPct, 1),
                proc.TargetCycleTimeMinutes,
                proc.TargetOnTimePercent));
        }

        return result.OrderByDescending(r => r.TotalInstances).ToList();
    }

    // ─── Экспорт в Excel ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<byte[]> ExportSummaryToExcelAsync(
        DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var items = await GetAnalyticsSummaryAsync(from, to, ct);

        using var workbook  = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Аналитика процессов");

        // Заголовки
        var headers = new[]
        {
            "Процесс",
            "Экземпляров",
            "Avg цикл (мин)",
            "% в срок",
            "% ошибок",
            "Целевое время цикла (мин)",
            "Целевой % в срок",
            "Откл. avg от цели (мин)",
        };

        for (int col = 1; col <= headers.Length; col++)
        {
            var cell = ws.Cell(1, col);
            cell.Value = headers[col - 1];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
            cell.Style.Font.FontColor = XLColor.White;
        }

        // Данные
        int row = 2;
        foreach (var item in items)
        {
            double? deltaAvg = item.TargetCycleTimeMinutes.HasValue
                ? item.AvgCycleTimeMinutes - item.TargetCycleTimeMinutes.Value
                : null;

            ws.Cell(row, 1).Value = item.ProcessName;
            ws.Cell(row, 2).Value = item.TotalInstances;
            ws.Cell(row, 3).Value = Math.Round(item.AvgCycleTimeMinutes, 2);
            ws.Cell(row, 4).Value = item.OnTimePercent;
            ws.Cell(row, 5).Value = item.FaultedPercent;
            ws.Cell(row, 6).Value = item.TargetCycleTimeMinutes.HasValue
                ? (XLCellValue)item.TargetCycleTimeMinutes.Value : Blank.Value;
            ws.Cell(row, 7).Value = item.TargetOnTimePercent.HasValue
                ? (XLCellValue)item.TargetOnTimePercent.Value : Blank.Value;
            ws.Cell(row, 8).Value = deltaAvg.HasValue
                ? (XLCellValue)Math.Round(deltaAvg.Value, 2) : Blank.Value;

            // Подсветка отклонения: красный если avg > цель, зелёный если <= цель
            if (deltaAvg.HasValue)
            {
                var deltaCell = ws.Cell(row, 8);
                deltaCell.Style.Font.FontColor = deltaAvg.Value > 0 ? XLColor.Red : XLColor.Green;
            }

            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ─── Вспомогательные методы ──────────────────────────────────────────────

    private async Task<ProcessAnalyticsDto> GetAnalyticsForVersionAsync(
        BpmProcess process, Guid versionId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var query = _db.BpmInstances.AsNoTracking()
            .Where(i => i.ProcessId == process.Id && i.ProcessVersionId == versionId);
        if (from.HasValue) query = query.Where(i => i.StartedAt >= from.Value);
        if (to.HasValue)   query = query.Where(i => i.StartedAt <= to.Value);

        var instances = await query.Select(i => new
        {
            i.State,
            i.StartedAt,
            i.CompletedAt,
        }).ToListAsync(ct);

        int total     = instances.Count;
        int completed = instances.Count(i => i.State == BpmInstanceState.Completed);
        int faulted   = instances.Count(i => i.State == BpmInstanceState.Faulted);

        var cycleTimes = instances
            .Where(i => i.State == BpmInstanceState.Completed && i.CompletedAt.HasValue)
            .Select(i => (i.CompletedAt!.Value - i.StartedAt).TotalMinutes)
            .OrderBy(t => t)
            .ToList();

        double avg    = cycleTimes.Count > 0 ? cycleTimes.Average() : 0;
        double median = CalculatePercentile(cycleTimes, 50);
        double p95    = CalculatePercentile(cycleTimes, 95);

        double onTime = cycleTimes.Count > 0 && process.TargetCycleTimeMinutes.HasValue
            ? cycleTimes.Count(t => t <= process.TargetCycleTimeMinutes.Value) * 100.0 / cycleTimes.Count
            : 0;
        double faultedPct = total > 0 ? faulted * 100.0 / total : 0;

        return new ProcessAnalyticsDto(
            process.Id,
            process.Name,
            total,
            completed,
            faulted,
            Math.Round(onTime, 1),
            Math.Round(faultedPct, 1),
            Math.Round(avg, 2),
            Math.Round(median, 2),
            Math.Round(p95, 2),
            BuildHistogram(cycleTimes),
            process.TargetCycleTimeMinutes,
            process.TargetOnTimePercent);
    }

    /// <summary>
    /// Вычисляет перцентиль методом линейной интерполяции.
    /// </summary>
    /// <param name="sorted">Отсортированный список значений.</param>
    /// <param name="percentile">Процентиль (0–100).</param>
    private static double CalculatePercentile(List<double> sorted, int percentile)
    {
        if (sorted.Count == 0) return 0;
        double index = (percentile / 100.0) * (sorted.Count - 1);
        int lower = (int)Math.Floor(index);
        int upper = (int)Math.Ceiling(index);
        if (lower == upper) return sorted[lower];
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (index - lower);
    }

    private static IReadOnlyList<CycleTimeHistogramBucketDto> BuildHistogram(List<double> sorted)
    {
        if (sorted.Count == 0) return [];
        const int buckets = 10;
        double min = sorted[0];
        double max = sorted[^1];
        if (max <= min) return [new CycleTimeHistogramBucketDto(min, max, sorted.Count)];

        double step = (max - min) / buckets;
        var result = new List<CycleTimeHistogramBucketDto>(buckets);
        for (int i = 0; i < buckets; i++)
        {
            double from = min + i * step;
            double to   = i == buckets - 1 ? max + 0.001 : min + (i + 1) * step;
            int count   = sorted.Count(v => v >= from && v < to);
            result.Add(new CycleTimeHistogramBucketDto(
                Math.Round(from, 2),
                Math.Round(to, 2),
                count));
        }
        return result;
    }
}
