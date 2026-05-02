using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Workers;

namespace CoreBPM.Server.Application.Bpm.Services;

public partial class BpmProcessService
{
    public async Task<BpmDiagramDto> GetVersionAsync(Guid processId, Guid versionId, CancellationToken ct = default)
        => MapVersionToDto(await GetVersionEntityAsync(processId, versionId, ct, asNoTracking: true));

    public async Task<BpmProcessVersionInfoDto> PublishVersionAsync(Guid processId, Guid versionId, string? releaseNotes, Guid userId, CancellationToken ct = default)
    {
        var version = await GetVersionEntityAsync(processId, versionId, ct);
        var validation = await ValidateProcessAsync(processId, versionId, ct);
        var blockingErrors = validation.Issues.Where(i => string.Equals(i.Severity, "Error", StringComparison.OrdinalIgnoreCase)).ToList();
        if (blockingErrors.Count > 0)
            throw new ValidationException(string.Join(Environment.NewLine, blockingErrors.Select(i => i.Message)));

        var currentPublished = await _db.BpmProcessVersions
            .Where(v => v.ProcessId == processId && v.Status == BpmProcessVersionStatus.Active)
            .ToListAsync(ct);

        foreach (var prev in currentPublished)
        {
            prev.Status = BpmProcessVersionStatus.Obsolete;
            prev.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var now = DateTimeOffset.UtcNow;
        version.Status = BpmProcessVersionStatus.Active;
        version.PublishedAt = now;
        version.UpdatedAt = now;
        version.ReleaseNotes = releaseNotes?.Trim();

        var process = await _db.BpmProcesses.FindAsync(new object[] { processId }, ct);
        if (process is not null)
            process.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        // Синхронизируем задания планировщика для таймерных стартовых событий
        try
        {
            await SyncSchedulerJobsForVersionAsync(processId, versionId, version.DiagramXml, ct);
        }
        catch
        {
            // Не блокируем публикацию при ошибке синхронизации заданий
        }

        // Генерируем HTML-снапшот документации при публикации версии
        try
        {
            await _documentation.GenerateAndSaveSnapshotAsync(processId, versionId, userId, ct);
        }
        catch
        {
            // Не блокируем публикацию при ошибке генерации документации
        }

        return MapVersionInfo(version);
    }

    public async Task<BpmDiagramDto> RollbackVersionAsync(Guid processId, Guid versionId, Guid userId, CancellationToken ct = default)
    {
        var source = await GetVersionEntityAsync(processId, versionId, ct, asNoTracking: true);
        var maxVersion = await _db.BpmProcessVersions
            .Where(v => v.ProcessId == processId)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;

        var now = DateTimeOffset.UtcNow;
        var rollback = new BpmProcessVersion
        {
            Id = Guid.NewGuid(),
            ProcessId = processId,
            VersionNumber = maxVersion + 1,
            Status = BpmProcessVersionStatus.Draft,
            DiagramXml = source.DiagramXml,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.BpmProcessVersions.Add(rollback);

        var process = await _db.BpmProcesses.FindAsync(new object[] { processId }, ct);
        if (process is not null)
            process.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return MapVersionToDto(rollback);
    }

    public async Task<BpmVersionDiffDto> DiffVersionsAsync(Guid processId, Guid leftVersionId, Guid rightVersionId, CancellationToken ct = default)
    {
        var left = await GetVersionEntityAsync(processId, leftVersionId, ct, asNoTracking: true);
        var right = await GetVersionEntityAsync(processId, rightVersionId, ct, asNoTracking: true);

        var leftModel = ParseProcessModel(left.DiagramXml ?? string.Empty);
        var rightModel = ParseProcessModel(right.DiagramXml ?? string.Empty);
        var changes = new List<BpmVersionDiffElementDto>();
        var properties = new List<BpmVersionDiffPropertyDto>();

        foreach (var element in rightModel.Nodes.Values.Where(n => !leftModel.Nodes.ContainsKey(n.Id)))
            changes.Add(new BpmVersionDiffElementDto("Added", element.Id, element.ElementType, element.Name));

        foreach (var element in leftModel.Nodes.Values.Where(n => !rightModel.Nodes.ContainsKey(n.Id)))
            changes.Add(new BpmVersionDiffElementDto("Removed", element.Id, element.ElementType, element.Name));

        foreach (var elementId in leftModel.Nodes.Keys.Intersect(rightModel.Nodes.Keys).OrderBy(id => id))
        {
            var leftNode = leftModel.Nodes[elementId];
            var rightNode = rightModel.Nodes[elementId];
            var changed = false;

            if (!string.Equals(leftNode.ElementType, rightNode.ElementType, StringComparison.Ordinal))
            {
                properties.Add(new BpmVersionDiffPropertyDto("Element", elementId, "type", leftNode.ElementType, rightNode.ElementType));
                changed = true;
            }

            if (!string.Equals(leftNode.Name, rightNode.Name, StringComparison.Ordinal))
            {
                properties.Add(new BpmVersionDiffPropertyDto("Element", elementId, "name", leftNode.Name, rightNode.Name));
                changed = true;
            }

            if (!string.Equals(leftNode.Documentation, rightNode.Documentation, StringComparison.Ordinal))
            {
                properties.Add(new BpmVersionDiffPropertyDto("Element", elementId, "documentation", leftNode.Documentation, rightNode.Documentation));
                changed = true;
            }

            if (changed)
                changes.Add(new BpmVersionDiffElementDto("Changed", elementId, rightNode.ElementType, rightNode.Name));
        }

        foreach (var flowId in leftModel.Flows.Keys.Union(rightModel.Flows.Keys).OrderBy(id => id))
        {
            leftModel.Flows.TryGetValue(flowId, out var leftFlow);
            rightModel.Flows.TryGetValue(flowId, out var rightFlow);
            if (leftFlow is null || rightFlow is null)
                continue;

            if (!string.Equals(leftFlow.SourceRef, rightFlow.SourceRef, StringComparison.Ordinal))
                properties.Add(new BpmVersionDiffPropertyDto("SequenceFlow", flowId, "sourceRef", leftFlow.SourceRef, rightFlow.SourceRef));
            if (!string.Equals(leftFlow.TargetRef, rightFlow.TargetRef, StringComparison.Ordinal))
                properties.Add(new BpmVersionDiffPropertyDto("SequenceFlow", flowId, "targetRef", leftFlow.TargetRef, rightFlow.TargetRef));
            if (!string.Equals(leftFlow.ConditionExpression, rightFlow.ConditionExpression, StringComparison.Ordinal))
                properties.Add(new BpmVersionDiffPropertyDto("SequenceFlow", flowId, "conditionExpression", leftFlow.ConditionExpression, rightFlow.ConditionExpression));
        }

        return new BpmVersionDiffDto(leftVersionId, rightVersionId, changes, properties);
    }

    public async Task<BpmDiagramDto> ImportDiagramAsync(Guid processId, string diagramXml, Guid userId, CancellationToken ct = default)
    {
        // Валидируем, что XML корректен
        try { System.Xml.Linq.XDocument.Parse(diagramXml); }
        catch (Exception ex) { throw new ValidationException($"Невалидный XML BPMN: {ex.Message}"); }

        var maxVersion = await _db.BpmProcessVersions
            .Where(v => v.ProcessId == processId)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;

        var now = DateTimeOffset.UtcNow;
        var version = new BpmProcessVersion
        {
            Id = Guid.NewGuid(),
            ProcessId = processId,
            VersionNumber = maxVersion + 1,
            Status = BpmProcessVersionStatus.Draft,
            DiagramXml = diagramXml,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.BpmProcessVersions.Add(version);
        var process = await _db.BpmProcesses.FindAsync(new object[] { processId }, ct);
        if (process is not null) process.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return MapVersionToDto(version);
    }

    /// <summary>
    /// Разбирает BPMN-диаграмму опубликованной версии и создаёт/обновляет задания планировщика
    /// для таймерных стартовых событий. Деактивирует задания предыдущих версий процесса.
    /// </summary>
    private async Task SyncSchedulerJobsForVersionAsync(
        Guid processId,
        Guid versionId,
        string? diagramXml,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(diagramXml)) return;

        // Парсим XML и находим таймерные стартовые события
        var timerStartElements = ParseTimerStartEvents(diagramXml);

        // Деактивируем задания предыдущих версий этого процесса
        var oldJobs = await _db.BpmSchedulerJobs
            .Where(j => j.ProcessId == processId && j.ProcessVersionId != versionId)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var old in oldJobs)
        {
            old.IsActive = false;
            old.UpdatedAt = now;
        }

        // Upsert заданий для новой версии
        var existingJobs = await _db.BpmSchedulerJobs
            .Where(j => j.ProcessId == processId && j.ProcessVersionId == versionId)
            .ToDictionaryAsync(j => j.ElementId, ct);

        foreach (var el in timerStartElements)
        {
            if (existingJobs.TryGetValue(el.ElementId, out var job))
            {
                // Обновляем существующее задание
                job.TimerType = el.TimerType;
                job.TimerValue = el.TimerValue;
                job.IsActive = true;
                job.NextFireAt = ComputeSchedulerNextFireAt(el.TimerType, el.TimerValue, now);
                job.UpdatedAt = now;
            }
            else
            {
                // Создаём новое задание
                _db.BpmSchedulerJobs.Add(new BpmSchedulerJob
                {
                    Id = Guid.NewGuid(),
                    ProcessId = processId,
                    ProcessVersionId = versionId,
                    ElementId = el.ElementId,
                    TimerType = el.TimerType,
                    TimerValue = el.TimerValue,
                    IsActive = true,
                    NextFireAt = ComputeSchedulerNextFireAt(el.TimerType, el.TimerValue, now),
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Вспомогательная запись с информацией о таймерном стартовом событии из BPMN-диаграммы.</summary>
    private record TimerStartEventInfo(string ElementId, string TimerType, string TimerValue);

    /// <summary>Извлекает таймерные стартовые события из XML диаграммы.</summary>
    private static List<TimerStartEventInfo> ParseTimerStartEvents(string xml)
    {
        var result = new List<TimerStartEventInfo>();
        try
        {
            XNamespace ns = "http://www.omg.org/spec/BPMN/20100524/MODEL";
            var doc = XDocument.Parse(xml);

            var startEvents = doc.Descendants(ns + "startEvent")
                .Concat(doc.Descendants("startEvent"));

            foreach (var startEvent in startEvents)
            {
                var id = startEvent.Attribute("id")?.Value;
                if (string.IsNullOrWhiteSpace(id)) continue;

                var timerDef = startEvent.Descendants(ns + "timerEventDefinition")
                    .Concat(startEvent.Descendants("timerEventDefinition"))
                    .FirstOrDefault();

                if (timerDef == null) continue;

                // Определяем тип и значение таймера
                var duration = timerDef.Element(ns + "timeDuration")?.Value?.Trim()
                            ?? timerDef.Element("timeDuration")?.Value?.Trim();
                var cycle = timerDef.Element(ns + "timeCycle")?.Value?.Trim()
                         ?? timerDef.Element("timeCycle")?.Value?.Trim();
                var date = timerDef.Element(ns + "timeDate")?.Value?.Trim()
                        ?? timerDef.Element("timeDate")?.Value?.Trim();

                string timerType;
                string timerValue;

                if (!string.IsNullOrWhiteSpace(cycle))
                {
                    timerType = "timeCycle";
                    timerValue = cycle;
                }
                else if (!string.IsNullOrWhiteSpace(date))
                {
                    timerType = "timeDate";
                    timerValue = date;
                }
                else if (!string.IsNullOrWhiteSpace(duration))
                {
                    timerType = "timeDuration";
                    timerValue = duration;
                }
                else
                {
                    continue;
                }

                result.Add(new TimerStartEventInfo(id, timerType, timerValue));
            }
        }
        catch
        {
            // При ошибке разбора XML — возвращаем пустой список
        }

        return result;
    }

    /// <summary>Вычисляет начальное NextFireAt при синхронизации задания из диаграммы.</summary>
    private static DateTimeOffset? ComputeSchedulerNextFireAt(string timerType, string timerValue, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(timerValue)) return null;

        switch (timerType)
        {
            case "timeDate":
                if (DateTimeOffset.TryParse(timerValue, out var date) && date > now)
                    return date;
                return null;

            case "timeDuration":
            case "timeCycle":
                return BpmSchedulerWorker.ComputeNextFireAt(timerValue, now);

            default:
                return null;
        }
    }

    private async Task<BpmProcessVersion> GetCurrentVersionEntityAsync(Guid processId, CancellationToken ct)
        => await _db.BpmProcessVersions
            .AsNoTracking()
            .Where(v => v.ProcessId == processId)
            .OrderBy(v => v.Status == BpmProcessVersionStatus.Draft ? 0 : 1)
            .ThenByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Процесс {processId} не имеет версий");

    private async Task<BpmProcessVersion> GetVersionEntityAsync(Guid processId, Guid versionId, CancellationToken ct, bool asNoTracking = false)
    {
        var query = asNoTracking ? _db.BpmProcessVersions.AsNoTracking() : _db.BpmProcessVersions.AsQueryable();
        return await query.FirstOrDefaultAsync(v => v.Id == versionId && v.ProcessId == processId, ct)
            ?? throw new NotFoundException($"Версия {versionId} процесса {processId} не найдена");
    }
}
